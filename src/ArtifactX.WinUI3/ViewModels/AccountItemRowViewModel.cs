using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on an account-wide-unlock page (Catalog / Expedition Rewards /
/// Twitch Rewards) - a static catalog item (GameId/DisplayName/CategoryLabel, from
/// CatalogService.GetAllUnlockableItemsAsync) paired with whether the active account
/// has it unlocked. IsUnlocked is written only by UnlockListViewModel.Set, which
/// keeps AccountSessionManager's staged edit in sync.</summary>
public partial class AccountItemRowViewModel : ObservableObject, IUnlockRow
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string CategoryLabel { get; init; }

    /// <summary>The catalog's top-level group for this row (CatalogUnlockableItem.CatalogGroup),
    /// "Uncategorized" when it has none. Never null.</summary>
    public required string CatalogGroup { get; init; }

    /// <summary>Set only on Expedition Rewards: the expedition a reward is from (the
    /// catalog's season-rewards table, or EXPD_ banner/decal/title naming on an
    /// older catalog). Groups the cards and feeds the Expedition filter.</summary>
    public int? ExpeditionNumber { get; init; }
    public string? ExpeditionName { get; init; }

    [ObservableProperty] public partial bool IsUnlocked { get; set; }
}
