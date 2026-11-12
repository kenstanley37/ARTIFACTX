using System.IO.Hashing;
using System.Text;

namespace ArtifactX.Core.Companions;

/// <summary>
/// A creature's pet-battle classes as the analysis visor's "Trait Potential" panel shows them (NMS.exe 184271,
/// Ghidra 2026-11-12). The visor builds pet data for the wild creature (0x1412da090 via 0x1405110b0) and rolls its
/// core stat classes (0x1413cfa80 -> 0x1413d0270):
///   1. seed = Hash128to64(low = genus seed, high = species seed) - the species' name seeds, bEr[3] and bEr[2];
///   2. (a, b) = StartState(seed); h = XXH3-64 (seed 0, default secret) of a, b (4 bytes each, little-endian) and
///      "CORE_STAT_CLASSES";
///   3. for each stat in GcPetBattlerCoreStat order, with names COMBAT, SPEED, HEALTH (16 bytes, zero padded):
///      XXH3-64 of StartState(h) and the name, one generator step, then n = (draw * total &gt;&gt; 32) + 1 picks the
///      class (C, B, A, S) by GcGameTableGlobals CoreStatClassRollTable weights (30/30/25/15 in build 185034).
/// The overall class is the highest whose threshold (0, 1, 4, 7, static data 0x144baa7a0) the three class numbers'
/// sum reaches.
/// </summary>
public static class PetBattlerClasses
{
    private static readonly string[] StatNames = ["COMBAT", "SPEED", "HEALTH"];
    private static readonly int[] PotentialThresholds = [0, 1, 4, 7];
    private const ulong K = 0x9DDFEA08EB382D69UL;

    /// <summary>The three core stat classes (0 = C ... 3 = S, GcPetBattlerCoreStat order: MaxHealth, Speed,
    /// CombatPotential) and the overall class, from the species' name seeds and the class weights per stat.</summary>
    public static (int[] Classes, int Potential) Roll(ulong speciesSeed, ulong genusSeed, IReadOnlyList<int[]> weights)
    {
        ulong seed = Hash128(genusSeed, speciesSeed);
        var (a, b) = StartState(seed);
        ulong h = Xxh3(a, b, Encoding.ASCII.GetBytes("CORE_STAT_CLASSES"));
        var (ha, hb) = StartState(h);
        var classes = new int[3];
        for (int i = 0; i < 3; i++)
        {
            var name = new byte[16];
            Encoding.ASCII.GetBytes(StatNames[i]).CopyTo(name, 0);
            ulong hs = Xxh3(ha, hb, name);
            var (sa, sb) = StartState(hs);
            uint draw = unchecked(sb + sa * 0x5A76F899u);
            int[] row = i < weights.Count ? weights[i] : [];
            int total = row.Sum();
            if (total <= 0) continue;
            int n = (int)(((ulong)draw * (ulong)total) >> 32) + 1, cumulative = 0;
            classes[i] = row.Length - 1;
            for (int c = 0; c < row.Length; c++)
            {
                cumulative += row[c];
                if (n <= cumulative) { classes[i] = c; break; }
            }
        }
        int sum = classes.Sum(), potential = 0;
        for (int p = PotentialThresholds.Length - 1; p >= 0; p--)
            if (PotentialThresholds[p] <= sum) { potential = p; break; }
        return (classes, potential);
    }

    private static ulong Xxh3(uint a, uint b, byte[] tail)
    {
        var data = new byte[8 + tail.Length];
        BitConverter.GetBytes(a).CopyTo(data, 0);
        BitConverter.GetBytes(b).CopyTo(data, 4);
        tail.CopyTo(data, 8);
        return XxHash3.HashToUInt64(data);
    }

    private static (uint A, uint B) StartState(ulong seed)
    {
        uint lo = (uint)seed, hi = (uint)(seed >> 32);
        return (lo == 0 ? 1u : lo, ((lo >> 16) | (lo << 16)) ^ hi ^ lo);
    }

    private static ulong Hash128(ulong low, ulong high)
    {
        ulong x = (low ^ high) * K;
        x ^= x >> 47;
        ulong y = (high ^ x) * K;
        y ^= y >> 47;
        return y * K;
    }
}
