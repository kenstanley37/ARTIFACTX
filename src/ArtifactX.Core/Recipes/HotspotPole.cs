using System.Numerics;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Recipes;

/// <summary>Caller-supplied spacing/count settings extracted from the hotspot table.</summary>
public sealed record HotspotPoleRules(float Spacing, float MinimumCount, float MaximumCount,
    float SameCategorySpacing, float DifferentCategoryOverlap);

/// <summary>Single-pole pass at native 0x1412F6110, inspected 2026-11-01.
/// Rejected proposals still consume all five random draws. Collision tests
/// group Power, Mineral1/2/3 and Gas1/2 separately and compare surface chord
/// distance with the two ranges. This is not a nearest-hotspot query.</summary>
public static class HotspotPole
{
    public static IReadOnlyList<HotspotCandidate> Generate(ulong address, float radius, int poleA, int poleB,
        HotspotPoleRules rules, IReadOnlyList<HotspotCategory> categories)
    {
        if (!float.IsFinite(rules.MinimumCount) || !float.IsFinite(rules.MaximumCount)
            || rules.MinimumCount < 0 || rules.MaximumCount < rules.MinimumCount || rules.MaximumCount > 256
            || !float.IsFinite(rules.SameCategorySpacing) || rules.SameCategorySpacing < 0
            || !float.IsFinite(rules.DifferentCategoryOverlap) || rules.DifferentCategoryOverlap < 0)
            throw new ArgumentException("Unsupported hotspot pole rules.", nameof(rules));
        const ulong multiplier = 0x9DDFEA08EB382D69UL;
        ulong key = ((ulong)(long)poleB << 32) | unchecked((ulong)(long)poleA);
        ulong hash = unchecked((key ^ address) * multiplier);
        hash = unchecked((hash ^ (hash >> 47) ^ address) * multiplier);
        hash = unchecked((hash ^ (hash >> 47)) * multiplier);
        uint low = (uint)hash;
        uint carry = BitOperations.RotateRight(low, 16) ^ (uint)(hash >> 32) ^ low;
        var random = new UniverseRandom(((ulong)carry << 32) | (low == 0 ? 1U : low));
        float fraction = (float)(random.NextWord() * (1.0 / 4294967295.0));
        float countRoll = fraction * (rules.MaximumCount - rules.MinimumCount);
        countRoll += rules.MinimumCount;
        int count = (int)MathF.Max(0f, countRoll);
        ulong state = random.Seed;
        var accepted = new List<HotspotCandidate>(count);
        for (int attempt = 0; accepted.Count < count; attempt++)
        {
            if (attempt >= 10000)
                throw new InvalidOperationException("Hotspot spacing did not converge within the supported attempt budget.");
            var proposal = HotspotCandidate.Generate(address, radius, poleA, poleB, rules.Spacing, state, categories);
            state = proposal.Attributes.FinalSeed;
            bool reject = accepted.Any(previous => Conflicts(previous, proposal, radius, rules));
            if (!reject) accepted.Add(proposal);
        }
        return accepted;
    }

    private static bool Conflicts(HotspotCandidate previous, HotspotCandidate proposal, float radius, HotspotPoleRules rules)
    {
        float distance = Vector3.Distance(previous.Direction * radius, proposal.Direction * radius);
        float overlap = proposal.Attributes.Range + previous.Attributes.Range - distance;
        return Category(previous.Attributes.Flag) == Category(proposal.Attributes.Flag)
            ? overlap > -rules.SameCategorySpacing : overlap > rules.DifferentCategoryOverlap;
    }

    private static int Category(int flag) => flag switch { 1 => 0, 2 or 4 or 8 => 1, 16 or 32 => 2, _ => -1 };
}
