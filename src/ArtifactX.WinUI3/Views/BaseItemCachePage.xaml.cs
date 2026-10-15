using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The Previous Base Item Cache (2026-10-15) - what dismantling a base gives
/// back (NmsInventoryContainer.BaseItemCachePath). Display only, as the user
/// asked: the game only lets the player take items out.
/// </summary>
public sealed partial class BaseItemCachePage : Page
{
    private readonly InventoryGridViewModel _model = new(NmsInventoryContainer.BaseItemCachePath,
        NmsInventoryContainer.BaseItemCacheColumns, NmsInventoryContainer.BaseItemCacheRows);

    public BaseItemCachePage()
    {
        InitializeComponent();
        CacheGrid.ViewModel = _model;
        CacheGrid.IsReadOnly = true;
        // Same stack sizes as a base storage container (Casing 135/200 in game).
        CacheGrid.ProductStorageMultiplier = 10;

        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        Unloaded += (_, _) => SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
        Load();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Load);

    private async void Load()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        _model.Load();
        await CatalogService.WarmCacheAsync(_model.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId));
        CacheGrid.Refresh();
    }
}
