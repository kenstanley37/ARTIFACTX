using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Source of a multi-tool pool in a star system.
/// </summary>
public enum MultiToolPoolSource
{
    SpaceStation,
    Planet,
}

/// <summary>
/// A native multi-tool in a system's or planet's procedural pool.
/// </summary>
/// <param name="Index">1-based index within its source pool.</param>
/// <param name="Seed">The 64-bit seed determining model parts, accessories, colors and kind.</param>
/// <param name="Source">The pool source (Space Station or Planet).</param>
/// <param name="SourceLabel">Label of the source (e.g. "Space Station" or "Planet 1 (Irskya)").</param>
/// <param name="DefaultName">The procedural name the game assigns to this tool.</param>
/// <param name="ModelPath">The scene MBIN model file.</param>
public sealed record SystemMultiTool(
    int Index,
    ulong Seed,
    MultiToolPoolSource Source,
    string SourceLabel,
    string DefaultName,
    string ModelPath,
    string PoolType = "Standard")
{
    public string SeedHex => $"0x{Seed:X16}";
    public bool IsStationPool => Source == MultiToolPoolSource.SpaceStation;
    public bool IsRoyal => ModelPath.Contains("ROYAL", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reproduces the pool construction at NMS build 180836 address 0x1416d1750.
/// Input is the full packed universe address (including galaxy and reload-body nibble),
/// not a portal code or a procedural planet seed. Pool definitions come from
/// GcSimulationGlobals.MultitoolPool. This produces pool members, not located stock.
/// Cabinet selection at 0x1416d2470 requires a separate site seed and pool type.
/// Verified 2026-10-24: address 0x980906A7A427 produces Geometric Angle Focuser
/// (0x3D4A291F239C0CBA), then Mass Needler Z-C11 (0x4101B7E8CB7DAFD9);
/// the latter matches the purchased Slot 8 SuJ[4] model seed and generated name.
/// </summary>
public static class SystemMultiToolGenerator
{
    public const string MainModel = "MODELS/COMMON/WEAPONS/MULTITOOL/MULTITOOL.SCENE.MBIN";
    public const string RoyalModel = "MODELS/COMMON/WEAPONS/MULTITOOL/ROYALMULTITOOL.SCENE.MBIN";

    private const ulong ConstA = 0x64DD81482CBD31D7UL;
    private const ulong ConstB = 0xE36AA5C613612997UL;

    internal static ulong Mix64(ulong register)
    {
        register = ((register >> 33) ^ register) * ConstA;
        register = ((register >> 33) ^ register) * ConstB;
        return (register >> 33) ^ register;
    }

    private static UniverseRandom RandomFor(ulong seed)
    {
        uint lo = (uint)seed;
        uint carry = ((lo >> 16) | (lo << 16)) ^ (uint)(seed >> 32) ^ lo;
        return new UniverseRandom(((ulong)carry << 32) | (lo == 0 ? 1u : lo));
    }

    /// <summary>
    /// Generates the actual native pool members. Missing catalog definitions produce no
    /// candidates. Rotational settlement stock requires its UTC whole-day number
    /// (Unix time in seconds divided by 86400), not the day-start timestamp.
    /// Neither this result nor the source label identifies a cabinet's item or class.
    /// </summary>
    public static IReadOnlyList<SystemMultiTool> GeneratePool(
        ulong universeAddress,
        MultiToolPoolSource source,
        string sourceLabel,
        Func<string, IReadOnlyList<string>?>? words = null,
        IReadOnlyList<MultiToolPoolDefinition>? definitions = null,
        ulong? rotationDay = null)
    {
        if (definitions is null) return [];
        ulong mixed = Mix64(universeAddress);
        var probabilities = RandomFor(mixed);
        var counts = RandomFor(mixed);
        var models = RandomFor(mixed);
        models.Step(); // The constructor consumes one word before drawing model pairs.
        UniverseRandom? rotational = rotationDay is { } day
            ? RandomFor(RotationHash(universeAddress, day)) : null;
        var draws = new List<(MultiToolPoolDefinition Definition, int Count)>();
        foreach (var definition in definitions)
        {
            float roll = (float)(probabilities.NextWord() * 2.3283064370807974e-10);
            int count = roll >= 1f - definition.Probability
                ? definition.MinDraw + (int)counts.Next((uint)(definition.MaxDraw - definition.MinDraw + 1))
                : 0;
            draws.Add((definition, count));
        }

        var result = new List<SystemMultiTool>();
        foreach (var (definition, count) in draws)
        {
            var rng = definition.PoolType == "SettlementRotational" ? rotational : models;
            if (rng is null) continue;
            for (int i = 0; i < count; i++)
            {
                uint low = rng.NextWord(), high = rng.NextWord();
                ulong seed = Mix64(((ulong)high << 32) | low);
                string name = words is null ? $"Multi-Tool {seed:X16}"
                    : MultiToolNames.Generate(seed, false, words) ?? $"Multi-Tool {seed:X16}";
                result.Add(new SystemMultiTool(result.Count + 1, seed, source, sourceLabel,
                    name, definition.ModelPath, definition.PoolType));
            }
        }
        return result;
    }

    private static ulong RotationHash(ulong address, ulong day)
    {
        const ulong multiplier = 0x9DDFEA08EB382D69UL;
        ulong a = (address ^ day) * multiplier;
        a ^= a >> 47;
        ulong b = (a ^ day) * multiplier;
        b ^= b >> 47;
        return b * multiplier;
    }

    /// <summary>
    /// The selection step at 0x1416d2470: filter native members by type, then take
    /// one MWC draw seeded by the cabinet/site seed. Does not determine class/stats.
    /// </summary>
    public static SystemMultiTool? SelectForSite(
        IReadOnlyList<SystemMultiTool> pool, ulong siteSeed, string poolType)
    {
        var matching = pool.Where(t => t.PoolType == poolType).ToArray();
        return matching.Length == 0 ? null : matching[RandomFor(siteSeed).Next((uint)matching.Length)];
    }

    /// <summary>Pool members for each reload body, using packed addresses throughout.</summary>
    public static IReadOnlyList<SystemMultiTool> GenerateSystem(
        ulong systemAddress,
        IReadOnlyList<(ulong PlanetSeed, int PlanetIndex, string Name)> planets,
        Func<string, IReadOnlyList<string>?>? words = null,
        bool hasSpaceStationCabinet = true,
        IReadOnlyList<MultiToolPoolDefinition>? definitions = null)
    {
        systemAddress &= ~(0xFUL << 52);
        var result = new List<SystemMultiTool>();
        if (hasSpaceStationCabinet)
            result.AddRange(GeneratePool(systemAddress, MultiToolPoolSource.SpaceStation, "Space Station", words, definitions));
        foreach (var planet in planets)
            result.AddRange(GeneratePool(systemAddress | ((ulong)(planet.PlanetIndex & 15) << 52),
                MultiToolPoolSource.Planet, planet.Name, words, definitions));
        return result;
    }
}
