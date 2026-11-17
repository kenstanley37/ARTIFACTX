namespace ArtifactX.Core.Inventory;

/// <summary>One stack-limit setting's sizes (GcDifficultyInventoryStackSizeOptionData): per inventory stack group, the
/// product stack size (multiplied by each product's StackMultiplier) and the substance stack size, then the caps.</summary>
public sealed record InventoryStackLimitOption(string Name, int[] Products, int[] Substances, int ProductLimit, int SubstanceLimit);

/// <summary>The game's stack-limit tables (catalog format 86, GcDifficultyConfig.InventoryStackLimitsOptionData): the
/// stack group names in GcInventoryStackSizeGroup order and one option per setting (High, Normal, Low).</summary>
public sealed record InventoryStackLimitTables(string[] Groups, InventoryStackLimitOption[] Options);

/// <summary>
/// A full stack in an inventory, as the game sets it (Bug 78, 2026-11-12). Every inventory in the save names its
/// stack group (WA4.rri: Personal, Ship, Freighter, Chest, BaseCapsule...), and the save's difficulty settings pick a
/// stack-limit option (<see cref="NmsModels.NmsDifficultySettings.InventoryStackLimitsPath"/>: High, Normal or Low).
/// A product's full stack is its StackMultiplier times the group's product size (at least 1, at most the product
/// limit); a substance's is the group's substance size. Evidence: every product and substance slot in the owner's
/// seven Steam saves (all on High): MaxAmount = multiplier x {Personal 10, Ship 10, Vehicle 10, MaintenanceObject 10,
/// Chest 20, Freighter 20, BaseCapsule 100}, substances 9999 - Metal Plating (multiplier 2) is 40 in a storage
/// container, where the app had used 20. Exceptions seen in the saves, left to the caller: the Default group (special
/// containers with their own sizes) and substances in MaintenanceObject inventories (refiners and other machines).
/// </summary>
public static class InventoryStackLimits
{
    /// <summary>The group's product and substance stack sizes for a setting; null when the group or setting is
    /// unknown, or the group is one whose inventories set their own sizes (Default).</summary>
    public static (int Product, int ProductLimit, int? Substance)? Sizes(InventoryStackLimitTables tables, string? group, string? setting)
    {
        if (string.IsNullOrEmpty(group) || group == "Default") return null;
        int g = Array.IndexOf(tables.Groups, group);
        var option = Array.Find(tables.Options, o => string.Equals(o.Name, setting, StringComparison.OrdinalIgnoreCase));
        if (g < 0 || option is null || g >= option.Products.Length || g >= option.Substances.Length) return null;
        int? substance = group == "MaintenanceObject" ? null : Math.Min(option.Substances[g], option.SubstanceLimit);
        return (option.Products[g], option.ProductLimit, substance);
    }

    /// <summary>A product's full stack: its StackMultiplier times the group's product size, at least 1, at most the
    /// product limit.</summary>
    public static int ProductStack(int stackMultiplier, int groupSize, int productLimit) =>
        Math.Clamp(stackMultiplier * groupSize, 1, productLimit);
}
