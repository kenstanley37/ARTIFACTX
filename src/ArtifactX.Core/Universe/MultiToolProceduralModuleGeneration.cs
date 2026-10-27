using System.Globalization;

namespace ArtifactX.Core.Universe;

public sealed record ProceduralModuleStat(int Stat, float Minimum, float Maximum, int Curve, bool AlwaysChoose);
public sealed record ProceduralModuleDefinition(string Id, int Quality, int MinimumStats, int MaximumStats,
    int CountCurve, ProceduralModuleStat[] Stats);
public sealed record GeneratedModuleStat(int Stat, float Value, int Level);

/// <summary>
/// Seeded procedural-module identity, separate from the module's rolled stats.
/// NMS build 180836, 0x140ec38c0: a fresh inventory-seed stream supplies one
/// draw bounded by 100000. Verified against 300 native suffix captures, including
/// zero and seeds with a zero low word. Existing suffixed IDs and the special
/// random-module dispatcher require different paths and are not accepted here.
/// </summary>
public static class MultiToolProceduralModuleGeneration
{
    /// <summary>
    /// Ordinary quality 0..3, SeaTrash UP_SCAN0 and illegal UP_SCANX stat rolls from a module's decimal suffix. Curves are
    /// extracted TkCurveType values, rather than the seven weighting-curve indices.
    /// Illegal rolls require the extracted high-grade chance; the located-record caller
    /// must separately establish that the network/perk override is inactive. Other special-quality overrides are unsupported. Inventory selection can use
    /// these bonuses; the full native materializer still needs independent validation.
    /// Verified 2026-10-25 against 3600 isolated native rolls (36 definitions x 100
    /// suffixes): counts, order, levels and float values matched, with zero ULP
    /// differences. The oracle substitutes powf with a rounded host power function.
    /// Assumes default runtime flags: no forced best stats or materializer override.
    /// UP_SCANX consumes an extra chance draw at 0x140ec4847. A successful draw
    /// maximizes count and consumes another uniform value per stat at 0x140ec4c3e,
    /// mapped to the native high-grade interval according to weighting-curve index.
    /// </summary>
    public static IReadOnlyList<GeneratedModuleStat>? RollOrdinaryStats(uint suffix,
        ProceduralModuleDefinition definition, IReadOnlyList<int> weightingCurves, int? illegalHighGradeChance = null)
    {
        bool Supported(int index) => index >= 0 && index < weightingCurves.Count
            && weightingCurves[index] is 0 or 16 or 17 or 19 or 20 or 25 or 26;
        bool illegalScanner = definition.Quality == 4 && definition.Id == "UP_SCANX"
            && illegalHighGradeChance is >= 0 and <= 100;
        if (suffix > 99999 || (definition.Quality is < 0 or > 3
                && !(definition.Quality == 7 && definition.Id == "UP_SCAN0") && !illegalScanner) || definition.MinimumStats < 0
            || definition.MaximumStats < definition.MinimumStats || !Supported(definition.CountCurve)
            || definition.Stats.Any(s => !Supported(s.Curve) || !float.IsFinite(s.Minimum)
                || !float.IsFinite(s.Maximum))) return null;

        uint mixed = ((suffix ^ 0x3d0000u) >> 16) ^ suffix;
        mixed = unchecked(mixed * 9);
        mixed ^= mixed >> 4;
        mixed = unchecked(mixed * 0x1b873593);
        mixed ^= mixed >> 15;
        uint carry = ((mixed >> 16) | (mixed << 16)) ^ mixed;
        var rng = new UniverseRandom(((ulong)carry << 32) | (mixed == 0 ? 1u : mixed));
        rng.NextWord(); rng.NextWord(); // Independent name seed pair.
        bool highGrade = illegalScanner && rng.Next(100) < illegalHighGradeChance!.Value;
        float Draw(int curve) => Weight((float)(rng.NextWord() * 2.3283064370807974e-10), weightingCurves[curve]);
        float countDraw = Draw(definition.CountCurve);
        float wanted = (definition.MaximumStats - definition.MinimumStats) * (highGrade ? 1f : countDraw)
            + definition.MinimumStats;
        int count = (int)(wanted + .5f);
        var mandatory = definition.Stats.Where(s => s.AlwaysChoose).ToList();
        var optional = definition.Stats.Where(s => !s.AlwaysChoose).ToList();
        for (int i = optional.Count - 1; i > 0; i--)
        {
            int j = (int)rng.Next((uint)(i + 1));
            (optional[i], optional[j]) = (optional[j], optional[i]);
        }
        count = Math.Min(4, Math.Min(mandatory.Count + optional.Count, count + mandatory.Count));
        return mandatory.Concat(optional).Take(count).Select(s =>
        {
            float value = Draw(s.Curve);
            if (highGrade)
            {
                float extra = (float)(rng.NextWord() * 2.3283064370807974e-10);
                value = s.Curve > 4 ? extra * .14f + .01f : extra * (1f - .85f) + .85f;
            }
            return new GeneratedModuleStat(s.Stat, (s.Maximum - s.Minimum) * value + s.Minimum, definition.Quality + 1);
        }).ToArray();
    }

    private static float Weight(float x, int curve)
    {
        switch (curve)
        {
            case 0: return x;
            case 16: return x * x;
            case 17: return -((x - 2f) * x);
            case 19: return ((x * x) * x) * x;
            case 20:
                float d = x - 1f;
                return ((d * d) * d) * (1f - x) + 1f;
            case 25: return x == 0 ? x : MathF.Pow(2f, (x - 1f) * 10f);
            case 26: return x == 1 ? x : 1f - MathF.Pow(2f, x * -10f);
            default: throw new ArgumentOutOfRangeException(nameof(curve));
        }
    }

    /// <summary>
    /// Creates an ordinary module ID from its catalog ID and inventory seed.
    /// The caller must resolve the catalog entry first; this does not roll stats.
    /// </summary>
    public static string? InitialId(string proceduralId, ulong inventorySeed)
    {
        if (string.IsNullOrEmpty(proceduralId) || proceduralId.Length > 9 ||
            !proceduralId.StartsWith("UP_", StringComparison.Ordinal)) return null;
        foreach (char ch in proceduralId)
            if (!(ch is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')) return null;

        uint suffix = SolarLayoutGeneration.RandomFor(inventorySeed).Next(100000);
        return proceduralId + "#" + suffix.ToString("D5", CultureInfo.InvariantCulture);
    }
}
