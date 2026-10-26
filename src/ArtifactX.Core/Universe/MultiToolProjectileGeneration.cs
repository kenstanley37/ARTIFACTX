namespace ArtifactX.Core.Universe;

/// <summary>Structural stat IDs for a catalog-confirmed ordinary Bolt projectile mode.</summary>
public sealed record MultiToolProjectileRules(int BaseStat, int DamageStat, int RateStat,
    int BulletsPerShotStat, int ClipStat, int ReloadStat, string DamageBonusId);

/// <summary>Boltcaster potential from a supported intact generated inventory.</summary>
public static class MultiToolProjectileGeneration
{
    /// <summary>Uses the catalog-confirmed intrinsic damage source from generated base stats.</summary>
    public static float? BoltPotential(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        IReadOnlyDictionary<string, float> intrinsicBonuses, bool hasActiveSpecialTechnology = false)
    {
        if (tables.BoltRules is not { } rules || !intrinsicBonuses.TryGetValue(rules.DamageBonusId, out float bonus)) return null;
        return BoltPotential(inventory, tables, bonus, hasActiveSpecialTechnology);
    }

    /// <summary>
    /// Resolves equipped values and applies neutral-difficulty per-hit integer truncation.
    /// The caller must establish the applicable intrinsic damage percentage. Custom-biome
    /// stat substitutions and other projectile modes require separate resolution.
    /// Verified 2026-10-25 against 300 native potential/per-hit/equipped executions,
    /// including 150 nonzero intrinsic bonuses, plus 750 equipped-stat comparisons.
    /// Runtime lookup/cache inputs are supplied; neutral difficulty is selected.
    /// The generated Iridescent inventory and base-stat dictionary reproduce 1362.7.
    /// </summary>
    public static float? BoltPotential(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        float intrinsicDamageBonusPercent, bool hasActiveSpecialTechnology = false)
    {
        if (tables.BoltRules is not { } rules || !float.IsFinite(intrinsicDamageBonusPercent)) return null;
        float? Stat(int stat, MultiToolStatCombination combination) => MultiToolStatGeneration.Evaluate(
            inventory, tables, stat, combination, rules.BaseStat, hasActiveSpecialTechnology);
        float? damage = Stat(rules.DamageStat, MultiToolStatCombination.Add);
        float? rate = Stat(rules.RateStat, MultiToolStatCombination.Multiply);
        float? bullets = Stat(rules.BulletsPerShotStat, MultiToolStatCombination.Maximum);
        float? clip = Stat(rules.ClipStat, MultiToolStatCombination.Add);
        float? reload = Stat(rules.ReloadStat, MultiToolStatCombination.Multiply);
        if (damage is not { } d || rate is not { } f || bullets is not { } b
            || clip is not { } c || reload is not { } r || d <= 0f) return null;
        d += (intrinsicDamageBonusPercent * d) * .01f;
        if (!float.IsFinite(d) || d < 0f || d >= int.MaxValue) return null;
        return MultiToolEquippedStats.ProjectilePotential((int)d, f, b, c, r);
    }
}
