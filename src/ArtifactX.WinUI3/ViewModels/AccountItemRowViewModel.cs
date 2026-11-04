using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on an account-wide-unlock page (Catalog / Expedition Rewards /
/// Twitch Rewards) - a static catalog item (GameId/DisplayName/CategoryLabel, from
/// CatalogService.GetAllUnlockableItemsAsync) paired with whether the active account
/// has it unlocked. IsUnlocked is written only by AccountUnlocksViewModel.Set, which
/// keeps AccountSessionManager's staged edit in sync.</summary>
public partial class AccountItemRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string CategoryLabel { get; init; }

    /// <summary>The reference-tool-style 9-way Catalog grouping (e.g. "Raw Materials",
    /// "Crafted Products", "Equipment"), from CatalogUnlockableItem.CatalogGroup.
    /// Always a real string here, never null - AccountDataPage substitutes
    /// "Uncategorized" for rows the catalog DB couldn't map (mainly Product rows
    /// whose own WikiCategory is "NotEnabled"), so every row still has SOME
    /// category checkbox that shows it and nothing silently disappears.</summary>
    public required string CatalogGroup { get; init; }

    /// <summary>Set only on Expedition Rewards: the expedition a reward is from (the
    /// catalog's season-rewards table, or EXPD_ banner/decal/title naming on an
    /// older catalog). Groups the cards and feeds the Expedition filter.</summary>
    public int? ExpeditionNumber { get; init; }
    public string? ExpeditionName { get; init; }

    [ObservableProperty] public partial bool IsUnlocked { get; set; }
}
