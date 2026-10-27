using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>Model-component facts resolved for this seed, not inferred from a pool or appearance.</summary>
public sealed record ResolvedStandardMultiTool(ulong ModelSeed, string ModelPath, string Kind,
    string InventoryTag, bool HasTechnologyOverrides);

/// <summary>Native inputs, either explicit or resolved for standard game-state initialization.</summary>
public sealed record MultiToolGenerationRuntime(int NativeWealth, int RequiredLevel,
    IReadOnlySet<string> KnownTechnologies, bool CycleInventoryRows, bool HasActiveSpecialTechnology,
    float? PreviewOverheatDuration = null, bool HasVerifiedStandardIllegalModuleRoll = false,
    string? ActiveFoodUnitItemId = null)
{
    /// <summary>
    /// Build 180836 initializes row cycling to true at 0x140487e0e. The normal
    /// constructor receives manager+0xe70, adds 0xaad0, and writes player+0x1aa40
    /// (manager+0x26380). Checked by native execution from prior bytes 0/1/255.
    /// Debug/test overrides can still use the explicit record constructor.
    /// </summary>
    public static MultiToolGenerationRuntime ForStandardGameState(int nativeWealth, int progressionLevel,
        IReadOnlySet<string> knownTechnologies, bool hasActiveSpecialTechnology)
        => new(nativeWealth, progressionLevel, knownTechnologies, true, hasActiveSpecialTechnology);
}

/// <summary>A located cabinet item with resolved class, inventory and scanner range.</summary>
/// <remarks>Consumers must omit DamagePotential when HasSupportedDamagePotential is false.</remarks>
public sealed record LocatedMultiTool(StationMultiToolContents Contents, string Kind,
    GeneratedMultiToolInventory Inventory, IReadOnlyDictionary<string, float> IntrinsicBonuses,
    float DamagePotential, float ScannerRange, bool HasSupportedDamagePotential = true, bool UsesLivePreviewState = false);

/// <summary>Composes the independently verified stages for a standard station's ordinary multi-tool.</summary>
public static class LocatedMultiToolGeneration
{
    /// <summary>
    /// The caller must establish the station/site, reload-body pool and every selected
    /// model-component fact before calling. Only main-model Pistol/Rifle variants with
    /// no technology overrides are supported. Unresolved stages yield no partial record.
    /// This does not infer the station type, reload state or planetary cabinet stock.
    /// </summary>
    public static LocatedMultiTool? Generate(StationMultiToolContents contents, ResolvedStandardMultiTool model,
        MultiToolGenerationRuntime runtime, InventoryGenerationTables inventoryTables,
        MultiToolTechnologyTables technologyTables)
    {
        if (runtime.NativeWealth is < 0 or > 3 || runtime.RequiredLevel < 0 || runtime.KnownTechnologies is null
            || (runtime.HasActiveSpecialTechnology
                && !MultiToolFoodGuard.HasNoDisplayedStatEffect(runtime.ActiveFoodUnitItemId, technologyTables))
            || contents.RatingIndex is < 0 or > 3 || model.ModelSeed != contents.Tool.Seed
            || model.HasTechnologyOverrides || contents.Location.Definition.ReseedAfterRewardSuccess
            || contents.Location.Definition.InteractionType != "WeaponTerminal"
            || contents.Location.Definition.PoolType != "Standard" || contents.Tool.PoolType != "Standard"
            || !string.Equals(model.ModelPath, SystemMultiToolGenerator.MainModel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(contents.Tool.ModelPath, model.ModelPath, StringComparison.OrdinalIgnoreCase)
            || !Finite(contents.Location.WorldPosition)
            || MultiToolSiteGeneration.SeedForPosition(contents.Location.WorldPosition) != contents.Location.SiteSeed)
            return null;
        if (contents.ReloadContext is { } reload)
        {
            var selected = StationMultiToolContents.SelectForReload(contents.Location, reload,
                runtime.NativeWealth, inventoryTables);
            if (selected is null || selected.RatingIndex != contents.RatingIndex
                || selected.Tool.Seed != contents.Tool.Seed || selected.Tool.Index != contents.Tool.Index
                || selected.Tool.Source != contents.Tool.Source || selected.Tool.PoolType != contents.Tool.PoolType
                || !string.Equals(selected.Tool.ModelPath, contents.Tool.ModelPath, StringComparison.OrdinalIgnoreCase))
                return null;
        }
        string? expectedKind = model.InventoryTag switch
        {
            "WEAPON_SMALL" => "Pistol",
            "WEAPON_MEDIUM" or "WEAPON_LARGE" => "Rifle",
            _ => null,
        };
        if (expectedKind is null || model.Kind != expectedKind) return null;
        if (MultiToolSiteGeneration.InventoryClass(contents.Location.SiteSeed, runtime.NativeWealth, inventoryTables)
            != contents.RatingIndex) return null;
        var intrinsic = EquipmentGeneration.CalculateMultiToolBaseStats(model.Kind, contents.Location.SiteSeed,
            contents.RatingIndex, inventoryTables);
        var inventory = MultiToolInventoryGeneration.InitialInventory(contents.Location.SiteSeed, contents.RatingIndex,
            model.InventoryTag, inventoryTables, technologyTables, runtime.NativeWealth, runtime.RequiredLevel,
            runtime.KnownTechnologies, runtime.CycleInventoryRows, model.HasTechnologyOverrides);
        if (intrinsic is null || inventory is null || !intrinsic.TryGetValue("WEAPON_SCAN", out float scanBonus)) return null;
        if (!runtime.HasVerifiedStandardIllegalModuleRoll
            && inventory.Technologies.Any(t => t.Id.StartsWith("UP_SCANX#", StringComparison.Ordinal))) return null;
        if (runtime.PreviewOverheatDuration is { } duration && (!float.IsFinite(duration) || duration < 0)) return null;
        var damageTables = runtime.PreviewOverheatDuration is { } actualDuration && technologyTables.DamageRules is { } rules
            ? technologyTables with { DamageRules = rules with { OverheatDuration = actualDuration } } : technologyTables;
        var damage = MultiToolDamageGeneration.StandardPotential(inventory, damageTables, intrinsic);
        var scan = MultiToolScannerGeneration.Range(inventory, technologyTables, scanBonus);
        return damage is { } d && scan is { } s
            ? new LocatedMultiTool(contents, model.Kind, inventory, intrinsic, d, s, UsesLivePreviewState: runtime.PreviewOverheatDuration is not null) : null;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
