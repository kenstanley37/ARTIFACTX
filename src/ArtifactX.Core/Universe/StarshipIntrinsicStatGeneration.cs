namespace ArtifactX.Core.Universe;

/// <summary>One ordered target of an INVENTORYTABLE base-stat bonus. Repeated targets are significant.</summary>
public sealed record StarshipIntrinsicStatTarget(int StatId, bool LessIsBetter);

/// <summary>
/// Applies intrinsic ship bonuses after technology evaluation. Native 0x1405B1940,
/// Steam build 26295378, inspected 2026-10-30. The mapping and UseMultiplierBonus
/// flag come from game data. SHIP_AGILE's repeated boost target is intentionally
/// retained; reducing the mapping to a set changes displayed maneuverability.
/// This does not select technology or resolve the comparison resource seed.
/// </summary>
public static class StarshipIntrinsicStatGeneration
{
    public static float? Apply(float technologyValue, int statId,
        IReadOnlyList<KeyValuePair<string, float>> orderedBonuses,
        IReadOnlyDictionary<string, StarshipIntrinsicStatTarget[]> mappings,
        bool useMultiplierBonus)
    {
        if (!float.IsFinite(technologyValue)) return null;
        float value = technologyValue;
        foreach (var bonus in orderedBonuses)
        {
            if (!float.IsFinite(bonus.Value) || !mappings.TryGetValue(bonus.Key, out var targets)
                || targets is null) return null;
            foreach (var target in targets)
            {
                if (target.StatId != statId) continue;
                float adjustment = bonus.Value;
                if (useMultiplierBonus) adjustment = (adjustment * value) * .01f;
                value = target.LessIsBetter ? value - adjustment : value + adjustment;
            }
        }
        return float.IsFinite(value) ? value : null;
    }
}
