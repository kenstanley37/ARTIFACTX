using System;
using System.Collections.Generic;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Ship Technology/Cargo grid sizing rules. The actual max-slot NUMBERS are
/// NOT hardcoded here - they live in the catalog database, extracted by
/// DataCataloger from the game's own metadata/reality/tables/inventorytable.mbin
/// (GcInventoryTable.ShipInventoryMaxUpgradeSize) via CatalogService.GetShipCapacityAsync.
/// Re-running the cataloger after a game update picks up changed numbers or a
/// brand-new ship type automatically - nothing here needs to change for that.
///
/// What DOES live here are the two things that are genuinely rules, not data:
/// - Detecting a ship's type from its model scene path (NTx.93M), the same
///   folder-name-as-truth approach already used for Multi-Tool Types.
/// - The GameId key format ShipCapacity catalog rows use, so callers can build
///   the right lookup key without needing to know the raw game enum names.
///
/// Six ship types were confidently mapped by cross-referencing real .SCENE.MBIN
/// folder names against the table's index order: Dropship=Hauler, Fighter,
/// Scientific=Explorer, Shuttle, Sail=Solar, Corvette. Two more table entries
/// (Royal, Alien) are flat across all four classes - almost certainly Sentinel
/// Interceptor and/or Exotic ships, which folder evidence (a single unclaimed
/// "sentinelship" folder) couldn't fully disambiguate between - so they're
/// deliberately left out of ShipType rather than guessed at. A "Robot" entry
/// also exists in the table but matched no real folder at all and mirrored
/// Hauler's numbers exactly, suggesting it's unused/vestigial.
/// </summary>
public static class StarshipCapacity
{
    public const int Columns = 10;

    public enum ShipType
    {
        Hauler,
        Fighter,
        Explorer,
        Shuttle,
        Solar,
        Corvette,

        // Not a @Cs entry - a player only ever manages one, so FreighterPage
        // passes this directly rather than going through DetectShipType. Still
        // sourced from the same real GcInventoryTable.ShipInventoryMaxUpgradeSize
        // - GcSpaceshipClasses.ShipClassEnum includes "Freighter" as its own
        // index, confirmed correct against real save data (see
        // NmsInventoryContainer.FreighterTechnologyPath).
        Freighter,
    }

    // Our friendly ShipType name -> the raw GcSpaceshipClasses.ShipClassEnum
    // name CatalogBuildService uses as the GameId prefix for ShipCapacity rows.
    private static readonly Dictionary<ShipType, string> RawTypeNames = new()
    {
        [ShipType.Hauler] = "DROPSHIP",
        [ShipType.Fighter] = "FIGHTER",
        [ShipType.Explorer] = "SCIENTIFIC",
        [ShipType.Shuttle] = "SHUTTLE",
        [ShipType.Solar] = "SAIL",
        [ShipType.Corvette] = "CORVETTE",
        [ShipType.Freighter] = "FREIGHTER",
    };

    // Safe fallback when the catalog has no ShipCapacity data yet (e.g. an
    // older catalog file built before this feature existed) or a ship's type
    // couldn't be detected - the largest confirmed real value across all six
    // known types, so an unmapped/missing case never clips a real slot.
    public const int FallbackCargoSlots = 120;
    public const int FallbackTechSlots = 60;

    /// <summary>Maps a ship's model scene path (NTx.93M) to a known ShipType by
    /// its real folder name under models/common/spacecraft/.</summary>
    public static ShipType? DetectShipType(string? scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return null;

        if (scenePath.Contains("SPACECRAFT/DROPSHIPS/", StringComparison.OrdinalIgnoreCase)) return ShipType.Hauler;
        if (scenePath.Contains("SPACECRAFT/FIGHTERS/", StringComparison.OrdinalIgnoreCase)) return ShipType.Fighter;
        if (scenePath.Contains("SPACECRAFT/SCIENTIFIC/", StringComparison.OrdinalIgnoreCase)) return ShipType.Explorer;
        if (scenePath.Contains("SPACECRAFT/SHUTTLE/", StringComparison.OrdinalIgnoreCase)) return ShipType.Shuttle;
        if (scenePath.Contains("SPACECRAFT/SAILSHIP/", StringComparison.OrdinalIgnoreCase)) return ShipType.Solar;
        if (scenePath.Contains("SPACECRAFT/CORVETTE/", StringComparison.OrdinalIgnoreCase)) return ShipType.Corvette;

        return null;
    }

    private static string NormalizeClassLetter(string? classLetter)
    {
        string upper = classLetter?.Trim().ToUpperInvariant() ?? "";
        return upper is "C" or "B" or "A" or "S" ? upper : "S"; // unknown class - default to the largest size
    }

    /// <summary>Builds the ShipCapacity catalog GameId for this ship's Cargo max
    /// slots, e.g. "DROPSHIP_C_CARGO". Look this up via a dictionary returned
    /// from CatalogService.GetShipCapacityAsync().</summary>
    public static string CargoCapacityKey(ShipType type, string? classLetter) =>
        $"{RawTypeNames[type]}_{NormalizeClassLetter(classLetter)}_CARGO";

    /// <summary>Same as CargoCapacityKey but for Technology max slots.</summary>
    public static string TechCapacityKey(ShipType type, string? classLetter) =>
        $"{RawTypeNames[type]}_{NormalizeClassLetter(classLetter)}_TECH";

    /// <summary>Total slots to grid rows - the grid itself is always 10 columns
    /// wide (confirmed for Exosuit, assumed the same fixed UI width for ships),
    /// so rows is just however many are needed to fit every slot.</summary>
    public static int SlotsToRows(int slots) => (int)Math.Ceiling(slots / (double)Columns);
}
