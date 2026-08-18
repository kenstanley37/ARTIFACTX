using System;
using System.Collections.Generic;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Exocraft live in a FIXED 7-slot array (vLc/6f=/P;m), one slot per
/// libMBIN's GcVehicleType.VehicleTypeEnum member - confirmed via
/// GcVehicleGlobals.VehicleDataTable/VehicleWeaponMuzzleFlash both being
/// declared as fixed Size=0x7 arrays keyed by that same enum, and
/// cross-checked stable (identical order/shape, including on completely
/// unowned/never-built slots) across 3 independent real saves spanning 2
/// different platform folders. This enum's member ORDER must match
/// VehicleTypeEnum exactly (Buggy=0 ... Mech=6) - the array index IS the
/// save-file position, there's no separate id field to look up.
///
/// Real display names confirmed via the game's own English loc strings
/// (DataCataloger locid VEHICLE -> VEHICLE_BUGGY_TITLE="ROAMER" etc.) rather
/// than community naming, except Minotaur - no single VEHICLE_MECH_TITLE key
/// exists, but every one of its ~35 tech/upgrade loc strings names it
/// "Minotaur" (e.g. "Minotaur Heavy Exocraft Hybrid"), so that's used as
/// its real name too.
/// </summary>
public enum ExocraftType
{
    Roamer,     // Buggy
    Nomad,      // Bike
    Colossus,   // Truck
    Pilgrim,    // WheeledBike
    Dragonfly,  // Hovercraft
    Nautilon,   // Submarine
    Minotaur,   // Mech
}

/// <summary>
/// Per-type Technology/Cargo grid sizing and tech-search filtering. Unlike
/// Ships (class-gated, purchasably grown via GcInventoryTable.
/// ShipInventoryMaxUpgradeSize), exocraft have no such per-class array on
/// GcInventoryTable at all - just a single non-array VehicleBaseStatsData/
/// VehicleCostData, and no known in-game mechanic to grow an exocraft's
/// slot count. So a completely fresh, never-built slot's own hl?
/// (UnlockedPositions) count IS the real fixed max for that type - read
/// directly from real save data (not a wiki/community number) and confirmed
/// identical across all 3 sampled saves above.
/// </summary>
public static class ExocraftCapacity
{
    public const int Columns = 10;

    private static readonly Dictionary<ExocraftType, (int Tech, int Cargo)> Slots = new()
    {
        [ExocraftType.Roamer] = (28, 40),
        [ExocraftType.Nomad] = (26, 30),
        [ExocraftType.Colossus] = (30, 50),
        [ExocraftType.Pilgrim] = (26, 30),
        [ExocraftType.Dragonfly] = (26, 30),
        [ExocraftType.Nautilon] = (28, 40),
        [ExocraftType.Minotaur] = (28, 40),
    };

    public static int TechSlots(ExocraftType type) => Slots[type].Tech;
    public static int CargoSlots(ExocraftType type) => Slots[type].Cargo;

    public static int TechRows(ExocraftType type) => (int)Math.Ceiling(TechSlots(type) / (double)Columns);
    public static int CargoRows(ExocraftType type) => (int)Math.Ceiling(CargoSlots(type) / (double)Columns);

    /// <summary>Real display name, confirmed via the game's own loc strings -
    /// see the ExocraftType doc comment.</summary>
    public static string DisplayName(ExocraftType type) => type switch
    {
        ExocraftType.Roamer => "Roamer",
        ExocraftType.Nomad => "Nomad",
        ExocraftType.Colossus => "Colossus",
        ExocraftType.Pilgrim => "Pilgrim",
        ExocraftType.Dragonfly => "Dragonfly",
        ExocraftType.Nautilon => "Nautilon",
        ExocraftType.Minotaur => "Minotaur",
        _ => type.ToString(),
    };

    /// <summary>Tech grid search-filter buckets, confirmed against the real
    /// catalog DB's UsageCategory column: the 4 "standard" ground/hover
    /// craft (Roamer/Nomad/Pilgrim/Dragonfly) share a generic "Exocraft"
    /// bucket with no per-type exclusives of their own (no "Roamer"/"Nomad"/
    /// etc. value exists anywhere in the catalog). Colossus, Nautilon
    /// (Submarine) and Minotaur (Mech) each have their own additional
    /// exclusive bucket on top of/instead of "Exocraft" - "AllVehicles" is
    /// the one bucket every type shares (e.g. the Exocraft Signal Booster).</summary>
    public static string[] UsageCategories(ExocraftType type) => type switch
    {
        ExocraftType.Colossus => new[] { "Exocraft", "Colossus", "AllVehicles" },
        ExocraftType.Nautilon => new[] { "Submarine", "AllVehicles" },
        ExocraftType.Minotaur => new[] { "Mech", "AllVehicles" },
        _ => new[] { "Exocraft", "AllVehicles" },
    };
}
