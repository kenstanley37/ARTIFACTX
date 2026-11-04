using ArtifactX.Core.Unlocks;

namespace ArtifactX.WinUI3.Helpers.AccountData;

/// <summary>
/// The Catalog page's card for each item. The game's own Catalogue group comes
/// first (CatalogUnlockableItem.CatalogGroup); products the game leaves out of
/// its Catalogue (WikiCategory NotEnabled - 1,215 of them in build 184271) are
/// placed by their product type instead of one huge "Uncategorized" card (owner
/// feedback 2026-11-03). Page-only grouping: the catalog's CatalogGroup itself is
/// untouched, since inventory sorting and the recipe filter read it. Only products
/// are listed (see AccountDataPage), so the substance and technology groups - Raw
/// Materials, Equipment - have no card.
/// </summary>
public static class CatalogGroups
{
    public const string Uncategorized = "Uncategorized";

    /// <summary>Fixed card order, so the page reads the same way every time.</summary>
    public static readonly string[] Order =
    [
        "Crafted Products", "Constructed Technology", "Construction Parts", "Starship Parts", "Trade Goods", "Curiosities", "Cooking Products",
        "Consumables", "Cosmetics", "Companion Eggs", Uncategorized,
    ];

    public static string For(string? catalogGroup, ProductTypeEntry? type) => catalogGroup ?? type switch
    {
        { ShipBuilderPart: true } => "Starship Parts",
        { Type: "BuildingPart" } => "Construction Parts",
        { Type: "CustomisationPart" or "Emote" } => "Cosmetics",
        { Type: "Consumable" } => "Consumables",
        { Type: "CreatureEgg" } => "Companion Eggs",
        { Type: "Curiosity" or "Fish" or "ExhibitBone" } => "Curiosities",
        { Type: "Tradeable" or "SpaceSalvage" } => "Trade Goods",
        { Type: "Component" } => "Crafted Products",
        _ => Uncategorized,
    };

    /// <summary>A few product names are templates the game fills in when the item
    /// is made ("%NAME% IMPLANT"); "…" stands in so no placeholder reaches players.</summary>
    public static string DisplayName(string name) => name.Replace("%NAME%", "…");
}
