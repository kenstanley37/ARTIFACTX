using System;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Multi-Tool Technology grid sizing rule. The actual max-slot NUMBERS are NOT
/// hardcoded here - they live in the catalog database, extracted by
/// DataCataloger from the game's own metadata/reality/tables/inventorytable.mbin
/// (GcInventoryTable.WeaponInventoryMaxUpgradeSize) via
/// CatalogService.GetMultiToolCapacityAsync. This replaces the old flat "8x3,
/// best guess, not verified" constant - confirmed real per-class numbers now
/// come straight from the game's own data (C=21, B=30, A=45, S=60 as of the
/// version this was last extracted against).
///
/// Unlike ships, multi-tool capacity does NOT vary by Type/shape (Pistol/Rifle/
/// Staff/etc.) - only by Class - confirmed by GcWeaponInventoryMaxUpgradeCapacity
/// being a single class-indexed array, not an array-per-type like ships'
/// GcShipInventoryMaxUpgradeCapacity. So there's no type-detection rule needed
/// here, just the class-keyed lookup format.
/// </summary>
public static class MultiToolCapacity
{
    public const int Columns = 10;

    // Safe fallback when the catalog has no MultiToolCapacity data yet (e.g. an
    // older catalog file built before this feature existed) - the largest
    // confirmed real value (S-class), so a missing lookup never clips a slot.
    public const int FallbackTechSlots = 60;

    private static string NormalizeClassLetter(string? classLetter)
    {
        string upper = classLetter?.Trim().ToUpperInvariant() ?? "";
        return upper is "C" or "B" or "A" or "S" ? upper : "S"; // unknown class - default to the largest size
    }

    /// <summary>Builds the MultiToolCapacity catalog GameId for this class's
    /// Technology max slots, e.g. "MULTITOOL_C_TECH". Look this up via a
    /// dictionary returned from CatalogService.GetMultiToolCapacityAsync().</summary>
    public static string TechCapacityKey(string? classLetter) =>
        $"MULTITOOL_{NormalizeClassLetter(classLetter)}_TECH";

    /// <summary>Total slots to grid rows - the grid itself is always 10 columns
    /// wide (confirmed for Exosuit, assumed the same fixed UI width for
    /// multi-tools), so rows is just however many are needed to fit every slot.</summary>
    public static int SlotsToRows(int slots) => (int)Math.Ceiling(slots / (double)Columns);
}
