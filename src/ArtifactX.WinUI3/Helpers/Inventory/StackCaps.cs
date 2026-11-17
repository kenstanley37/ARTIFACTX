using System;

namespace ArtifactX.WinUI3.Helpers.Inventory;

/// <summary>
/// A full stack of an item in a container: technology is 100 (durability, not a
/// count), products their catalog stack size times the container's product size,
/// substances the container's substance size (InventoryStackLimitService works both
/// out from the game's tables, Bug 78; 9999 when unknown). The
/// catalog's StackMultiplier is meaningless for substances (73 of 79 rows say 1;
/// a Polished Stone added with it capped at 1 instead of 9999, fixed 2026-07-30),
/// so they always take the game's 9999 ceiling.
/// </summary>
public static class StackCaps
{
    /// <summary>The product multiplier of Exosuit, starship and freighter cargo.</summary>
    public const int CargoProductMultiplier = 10;

    public static int For(string? categoryLabel, int? maxStackSize, int productMultiplier, int productLimit = 9999, int substanceStack = 9999) =>
        categoryLabel switch
        {
            "Technology" => 100,
            // A multiplier of 0 still stacks to 1 (every such slot in the owner's saves); unknown items keep the limit.
            "Product" => maxStackSize is int m ? ArtifactX.Core.Inventory.InventoryStackLimits.ProductStack(m, productMultiplier, productLimit) : productLimit,
            _ => substanceStack,
        };
}
