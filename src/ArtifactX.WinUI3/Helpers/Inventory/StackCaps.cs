using System;

namespace ArtifactX.WinUI3.Helpers.Inventory;

/// <summary>
/// A full stack of an item in a container: technology is 100 (durability, not a
/// count), products their catalog stack size times the container's product
/// multiplier (10 for Exosuit, ship and freighter cargo), substances 9999. The
/// catalog's StackMultiplier is meaningless for substances (73 of 79 rows say 1;
/// a Polished Stone added with it capped at 1 instead of 9999, fixed 2026-07-30),
/// so they always take the game's 9999 ceiling.
/// </summary>
public static class StackCaps
{
    /// <summary>The product multiplier of Exosuit, starship and freighter cargo.</summary>
    public const int CargoProductMultiplier = 10;

    public static int For(string? categoryLabel, int? maxStackSize, int productMultiplier) => categoryLabel switch
    {
        "Technology" => 100,
        "Product" => maxStackSize is > 0 ? Math.Min(maxStackSize.Value * productMultiplier, 9999) : 9999,
        _ => 9999,
    };
}
