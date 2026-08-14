using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One category card on an account-wide-unlock page (Catalog/
/// Expedition Rewards/Twitch Rewards) - the same "group by category into
/// cards" idea as GuideCategoryGroupViewModel, but for real-world per-category
/// counts far more uneven than Guide's (Guide never exceeds ~9 topics), so
/// each card's item list is a real virtualizing ListView, not a plain
/// unbounded ItemsControl.</summary>
public sealed class CatalogCategorySectionViewModel
{
    public required string Header { get; init; }
    public required List<AccountItemRowViewModel> Items { get; init; }

    /// <summary>The card's internal ListView height in pixels - settable, not
    /// computed from Items.Count alone, since each page that builds these
    /// decides its own sizing rule at construction time (see each page's own
    /// card-building code): Catalog/Twitch cap every card at a fixed max
    /// (their real per-card counts are always well past that cap anyway, so
    /// there's no reason to try to fully fit them), while Expedition Rewards'
    /// per-expedition cards are small enough that EVERY card gets sized to the
    /// tallest real group currently visible, so none of them ever needs to
    /// scroll internally at all (user feedback 2026-08-13: "make all the cards
    /// the same height and the height needs to be enough so the scrollbar
    /// doesn't show").</summary>
    public required double ListHeight { get; init; }
}
