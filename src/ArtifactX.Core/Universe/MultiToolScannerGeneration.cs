namespace ArtifactX.Core.Universe;

/// <summary>Scanner metadata from the scan table, stat enum and gameplay globals.</summary>
public sealed record MultiToolScannerRules(int RadiusStat, float PulseRange, float SpecialSlotBonus,
    float SameTypeMultiplier, float ChildTypeMultiplier, int MaximumGroupTechnologies,
    float? SameTypeAdd = null, float? ChildTypeAdd = null);

/// <summary>Scanner evaluation for intact initial tool inventories with default runtime flags.</summary>
public static class MultiToolScannerGeneration
{
    /// <summary>
    /// Resolves regular/module bonuses, supercharged slots and orthogonal adjacency,
    /// then applies the site's intrinsic scan percentage and the TOOL pulse range.
    /// Active special technology and overloaded groups are unsupported.
    /// Verified 2026-10-25 against 300 native equipped-stat evaluations, including
    /// modules, varied positions, charged cells and unequal adjacency coefficients.
    /// Native lookup/cache inputs were supplied explicitly; default runtime flags
    /// and an empty secondary inventory were used. Four screenshot ranges match.
    /// </summary>
    public static float? Range(GeneratedMultiToolInventory inventory, MultiToolTechnologyTables tables,
        float intrinsicBonusPercent, bool hasActiveSpecialTechnology = false)
    {
        if (tables.ScannerRules is not { } rules) return null;
        float? factor = MultiToolStatGeneration.Evaluate(inventory, tables, rules.RadiusStat,
            MultiToolStatCombination.Multiply, hasActiveSpecialTechnology: hasActiveSpecialTechnology);
        return factor is { } value
            ? MultiToolEquippedStats.ScannerRange(value, intrinsicBonusPercent, rules.PulseRange) : null;
    }
}
