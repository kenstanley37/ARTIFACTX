namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A row UnlockListViewModel can tick or untick: an account-unlock row or a
/// Catalog page tile.</summary>
public interface IUnlockRow
{
    string GameId { get; }
    bool IsUnlocked { get; set; }
}
