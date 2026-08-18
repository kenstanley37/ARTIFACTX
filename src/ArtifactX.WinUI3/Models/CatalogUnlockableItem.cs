namespace ArtifactX.WinUI3.Models;

/// <summary>One row from CatalogService.GetAllUnlockableItemsAsync - every
/// catalog item that can plausibly appear in the account-wide "unlocked
/// items" list (see NmsAccountData), independent of whether this particular
/// account actually has it unlocked. CategoryLabel reuses the same "elv"
/// Technology/Product/Substance vocabulary as CatalogSearchResult, kept for
/// consistency with the rest of the app's other pages (Catalogue's own
/// device-category filter, etc.).</summary>
public sealed class CatalogUnlockableItem
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string CategoryLabel { get; init; }

    /// <summary>Technology-only slot signal (Suit/Ship/Weapon/Freighter/"All"
    /// for tech usable everywhere) - the same field the Exosuit/Ships/
    /// MultiTool/Freighter pages already filter their own tech grids by (see
    /// each page's TechGrid.AllowedUsageCategories). Null for Product/
    /// Substance rows, which don't carry this at all - see CatalogService's
    /// SearchAsync comment on why the usage-category join there is optional.</summary>
    public string? UsageCategory { get; init; }

    /// <summary>The reference-tool-style Catalog top-level grouping (e.g.
    /// "Raw Materials", "Crafted Products", "Constructed Technology",
    /// "Equipment") - CatalogItem.CatalogGroup, straight from the catalog DB.
    /// Null for row types with no such mapping (MultiToolTypes/ShipCapacity/
    /// etc. - non-item metadata rows this list already excludes - and Product
    /// rows whose own WikiCategory is "NotEnabled", i.e. not shown on the
    /// in-game Guide/Catalog screen at all).</summary>
    public string? CatalogGroup { get; init; }
}
