using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Recipes;

/// <summary>One category's weights and ranges, supplied from the game's hotspot table.</summary>
public sealed record HotspotCategory(float Weight, float[] Strengths, float[] ClassWeights,
    float MinimumRange, float MaximumRange);

/// <summary>Attributes only; this is not a located hotspot or an extractor resource.</summary>
public sealed record HotspotAttributeRoll(int Flag, int Class, float Strength, float HighestStrength,
    float Range, ulong FinalSeed);

/// <summary>Attribute draws from native 0x1412F6580 inspected 2026-11-01.
/// Two intervening draws belong to coordinate jitter. Keeping them preserves
/// the class draw and final random state. Geometry, spacing rejection and the
/// selection of a hotspot beneath an extractor are deliberately separate.</summary>
public static class HotspotAttributes
{
    public static HotspotAttributeRoll Roll(ulong seed, IReadOnlyList<HotspotCategory> categories)
    {
        if (categories.Count != 6 || categories.Any(c => !float.IsFinite(c.Weight) || c.Weight <= 0
            || c.Strengths.Length != 4 || c.ClassWeights.Length != 4
            || c.Strengths.Any(v => !float.IsFinite(v))
            || c.ClassWeights.Any(v => !float.IsFinite(v) || v <= 0)
            || !float.IsFinite(c.MinimumRange) || !float.IsFinite(c.MaximumRange)
            || c.MinimumRange < 0 || c.MaximumRange < c.MinimumRange))
            throw new ArgumentException("Six valid hotspot category definitions are required.", nameof(categories));
        var random = new UniverseRandom(seed);
        int category = Choose(categories.Select(c => c.Weight).ToArray(), random);
        var selected = categories[category];
        float unit = Unit(random);
        float width = selected.MaximumRange - selected.MinimumRange;
        float range = unit * width;
        range += selected.MinimumRange;
        random.Step();
        random.Step();
        int grade = Choose(selected.ClassWeights, random);
        return new(1 << category, grade, selected.Strengths[grade], selected.Strengths[3], range, random.Seed);
    }

    private static float Unit(UniverseRandom random) => (float)(random.NextWord() * (1.0 / 4294967295.0));

    private static int Choose(float[] weights, UniverseRandom random)
    {
        var cumulative = new float[weights.Length];
        float sum = 0;
        for (int i = 0; i < weights.Length; i++) cumulative[i] = sum += weights[i];
        float draw = Unit(random) * (sum - 1.17549435E-38f);
        int index = 0;
        while (index < weights.Length - 1 && draw > cumulative[index]) index++;
        return index;
    }
}
