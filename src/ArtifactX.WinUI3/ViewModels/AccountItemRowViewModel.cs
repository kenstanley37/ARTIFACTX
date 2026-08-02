using CommunityToolkit.Mvvm.ComponentModel;

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

    [ObservableProperty]
    private bool isUnlocked;
}
