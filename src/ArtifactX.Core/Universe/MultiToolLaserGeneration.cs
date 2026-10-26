namespace ArtifactX.Core.Universe;

/// <summary>PLAYER laser data and the standard multi-tool OVERHEAT animation, from the catalog.</summary>
public sealed record MultiToolLaserRules(int BaseStat, int DamageStat, int HeatStat, int ReloadStat,
    int DefaultDamage, float HitInterval, float OverheatDuration);

/// <summary>The standard mining-laser damage mode; this is not the maximum across weapon modes.</summary>
public static class MultiToolLaserGeneration
{
    /// <summary>
    /// Computes neutral-difficulty laser potential for a supported standard tool inventory.
    /// The caller supplies the intrinsic bonus applicable to laser damage (not mining yield).
    /// Other model animation sets, active-special effects and overloaded groups are unsupported.
    /// Verified 2026-10-25 against 88 executions of the native laser potential and
    /// per-hit routines, with native equipped-stat evaluation and explicit beam/cache
    /// inputs. These cases use zero intrinsic adjustment; nonzero intrinsic mapping
    /// must be resolved independently by the caller. This is one weapon mode only.
    /// </summary>
    public static float? StandardPotential(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        float intrinsicLaserDamageBonusPercent, bool hasActiveSpecialTechnology = false)
    {
        if (tables.StandardLaserRules is not { } rules || rules.DefaultDamage < 0
            || !float.IsFinite(intrinsicLaserDamageBonusPercent)) return null;
        float? damage = MultiToolStatGeneration.Evaluate(inventory, tables, rules.DamageStat,
            MultiToolStatCombination.Add, rules.BaseStat, hasActiveSpecialTechnology);
        float? heat = MultiToolStatGeneration.Evaluate(inventory, tables, rules.HeatStat,
            MultiToolStatCombination.Multiply, rules.BaseStat, hasActiveSpecialTechnology);
        float? reload = MultiToolStatGeneration.Evaluate(inventory, tables, rules.ReloadStat,
            MultiToolStatCombination.Multiply, rules.BaseStat, hasActiveSpecialTechnology);
        if (damage is not { } d || heat is not { } h || reload is not { } r || d <= 0f) return null;
        d += (intrinsicLaserDamageBonusPercent * d) * .01f;
        if (!float.IsFinite(d) || d >= int.MaxValue || d < 0f) return null;
        float perHit = Math.Max(1, (int)d) * (float)rules.DefaultDamage;
        return MultiToolEquippedStats.LaserPotential(perHit, h, r, rules.OverheatDuration, rules.HitInterval);
    }
}
