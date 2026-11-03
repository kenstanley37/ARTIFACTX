using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The Previous Base Item Cache (2026-10-15) - what dismantling a base gives
/// back (NmsInventoryContainer.BaseItemCachePath). Display only, as the user
/// asked: the game only lets the player take items out.
/// </summary>
public sealed partial class BaseItemCacheControl : UserControl
{
    private readonly CargoInventoryViewModel _editor = new(NmsInventoryContainer.BaseItemCachePath,
        NmsInventoryContainer.BaseItemCacheColumns, NmsInventoryContainer.BaseItemCacheRows);

    public BaseItemCacheControl()
    {
        InitializeComponent();
        CacheGrid.ViewModel = _editor.Inventory;
        CacheGrid.IsReadOnly = true;
        // Same stack sizes as a base storage container (Casing 135/200 in game).
        CacheGrid.ProductStorageMultiplier = 10;

        _ = new CargoStorageFeed(this, () => [_editor.Path], Load);
    }

    private async void Load()
    {
        if (await _editor.RefreshAsync()) CacheGrid.Refresh();
    }
}
