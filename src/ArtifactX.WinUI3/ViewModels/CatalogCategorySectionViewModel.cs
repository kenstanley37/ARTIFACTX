using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One category card on AccountDataPage's Catalog section - the same
/// "group by category into cards" idea as GuideCategoryGroupViewModel, but for
/// CatalogGroup's much larger, uneven per-category counts (43 Trade Goods rows
/// vs 1,718 Construction Parts rows in the real catalog), so each card's item
/// list is a real virtualizing ListView with a bounded/scrollable height rather
/// than a plain unbounded ItemsControl - Guide's cards could get away with the
/// latter because no category there ever exceeds ~9 topics.</summary>
public sealed class CatalogCategorySectionViewModel
{
    public required string Header { get; init; }
    public required List<AccountItemRowViewModel> Items { get; init; }

    /// <summary>Caps the card's internal ListView height so a huge category
    /// (Construction Parts) scrolls within its own card instead of growing the
    /// whole card to thousands of pixels tall, while a small category (Trade
    /// Goods) gets a card sized to its actual content instead of a mostly-empty
    /// fixed-height box. ~36px/row is ListView's real rendered row height for
    /// AccountItemRowTemplate (two stacked TextBlocks + padding), confirmed by
    /// eye against the shipped app rather than measured precisely - close
    /// enough that the cap only ever kicks in for genuinely long lists.</summary>
    public double ListHeight => Math.Min(Items.Count * 36 + 8, 420);
}
