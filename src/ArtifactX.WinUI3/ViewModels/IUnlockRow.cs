using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A row UnlockListViewModel can tick or untick: an account-unlock row or a
/// Catalog page tile. ListIds are the list entries it stands for - its own id, or for
/// a Recipes tile every recipe that makes that result.</summary>
public interface IUnlockRow
{
    string GameId { get; }
    bool IsUnlocked { get; set; }
    IReadOnlyList<string> ListIds => [GameId];
}
