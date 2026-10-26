using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Site-dependent steps traced in NMS build 180836 (2026-10-24).
/// 0x140504eb0 hashes the interaction entity's world position, rounding each float
/// away from zero at half units before two Hash128to64 combines. This is not a
/// planet's latitude/longitude or its building-marker position: the actual cabinet
/// transform is required. 0x1404cfda0 rolls inventory class from the site seed and
/// the current system's GcInventoryTable.ClassProbabilityData row.
/// </summary>
public static class MultiToolSiteGeneration
{
    private const ulong HashMultiplier = 0x9DDFEA08EB382D69UL;

    public static ulong SeedForPosition(Vector3 worldPosition)
    {
        static ulong Rounded(float value) => unchecked((ulong)(long)(int)(value + MathF.CopySign(0.5f, value)));
        return Combine(Combine(Rounded(worldPosition.X), Rounded(worldPosition.Y)), Rounded(worldPosition.Z));
    }

    private static ulong Combine(ulong low, ulong high)
    {
        ulong a = (low ^ high) * HashMultiplier;
        a ^= a >> 47;
        ulong b = (a ^ low) * HashMultiplier;
        b ^= b >> 47;
        return b * HashMultiplier;
    }

    /// <summary>
    /// Returns C=0/B=1/A=2/S=3 for a known site seed and catalog probability row.
    /// Does not locate the site or assume its seed. Missing tables return null.
    /// The game's pirate row is used as stored: leftover probability stays C.
    /// </summary>
    public static int? InventoryClass(ulong siteSeed, int probabilityRow, InventoryGenerationTables? tables)
    {
        if (tables?.ClassProbabilities is not { } rows || probabilityRow < 0 || probabilityRow >= rows.Length
            || rows[probabilityRow].Length != 4) return null;
        uint lo = (uint)siteSeed;
        uint carry = ((lo >> 16) | (lo << 16)) ^ (uint)(siteSeed >> 32) ^ lo;
        var rng = new UniverseRandom(((ulong)carry << 32) | (lo == 0 ? 1u : lo));
        float roll = (float)(rng.NextWord() * 2.3283064370807974e-10);
        float cumulative = 0;
        for (int rating = 0; rating < 4; rating++)
        {
            cumulative += rows[probabilityRow][rating] * 0.01f;
            if (cumulative >= roll) return rating;
        }
        return 0;
    }
}
