using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in AccountDataPage's catalog list - a static catalog item
/// (GameId/DisplayName/CategoryLabel, from CatalogService.GetAllUnlockableItemsAsync)
/// paired with whether the currently active account has it unlocked. IsUnlocked
/// is local, editable UI state; AccountDataPage is the only thing that writes it
/// and is responsible for keeping AccountSessionManager's staged edit in sync.</summary>
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

    /// <summary>Null for the vast majority of rows - only set for Banner/Decal/Title
    /// Expedition Reward items, whose own names spell out which expedition they're
    /// from (see CatalogService.BuildExpeditionNameLookup). Used both for the
    /// Expedition column and the Expedition filter dropdown.</summary>
    public int? ExpeditionNumber { get; init; }
    public string? ExpeditionName { get; init; }

    public string ExpeditionLabel => ExpeditionNumber is int n ? $"#{n}: {ExpeditionName}" : "";
    public Visibility ExpeditionVisibility => ExpeditionNumber is int ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private bool isUnlocked;
}
