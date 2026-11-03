using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using System;
using System.Linq;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The Corvette Workshop Cache - a single fixed 10x16 (160-slot) container
/// holding the proc-gen building parts used to construct/upgrade a player's
/// Corvette. Unlike Ships/Multi-Tool/Freighter there's only ever one of
/// these (no per-item selector needed) and unlike Base Storage it lives at a
/// known, fixed path rather than needing runtime discovery - see
/// NmsInventoryContainer.CorvetteCachePath for how it was confirmed. No
/// "Unlock All Slots" button here (unlike every other page) - the player has
/// no in-game way to expand this cache's slot count, so there's nothing to
/// unlock.
/// </summary>
public sealed partial class CorvetteCacheControl : UserControl
{
    private readonly CargoInventoryViewModel _editor =
        new(NmsInventoryContainer.CorvetteCachePath, CorvetteCacheCapacity.Columns, CorvetteCacheCapacity.Rows);

    private InventoryGridViewModel _viewModel => _editor.Inventory;

    public CorvetteCacheControl()
    {
        InitializeComponent();
        CacheGrid.ViewModel = _viewModel;

        CacheGrid.AllowedCategories = new[] { "Product" };
        CacheGrid.AllowedTemplateTypes = new[] { "GcProductTable" };
        CacheGrid.AllowedUsageCategories = new[] { "BuildingPart" };
        CacheGrid.SupportsSupercharge = false;
        CacheGrid.SupportsRepair = false;

        // Confirmed via real save data: every occupied slot's max amount
        // (F9q) is 500, while the catalog's raw MaxStackSize for all 13
        // known building parts is 5 - a 100x multiplier, not the 10x every
        // other Product-holding grid in the app uses.
        CacheGrid.ProductStorageMultiplier = 100;

        CacheGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        _ = new CargoStorageFeed(this, () => [_editor.Path], LoadGrid);

        // Every part a corvette is built from, to click into the cache - and
        // the same list limits the right-click search.
        _ = PartsPanel.LoadAsync(CacheGrid, async () =>
        {
            var parts = await CatalogService.GetCorvettePartsAsync();
            CacheGrid.AllowedItemIds = parts.Select(p => p.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return parts;
        }, "part", "parts", "Product", _ => "");
    }

    private async void LoadGrid()
    {
        if (await _editor.RefreshAsync())
        {
            CacheGrid.Refresh();
            PartsPanel.RefreshOwned();
        }
        PageResetBtn.Visibility = _editor.HasChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MaxAllQtyBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.MaxAllQuantities();
        CacheGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MergeDuplicatesBtn_Click(object sender, RoutedEventArgs e)
    {
        int freed = _viewModel.MergeDuplicates();
        TidyResultTxt.Text = freed == 0 ? "No duplicates found." : $"Merged duplicates - {freed} slot{(freed == 1 ? "" : "s")} freed.";
        if (freed == 0) return;
        CacheGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SortBtn_Click(object sender, RoutedEventArgs e)
    {
        bool moved = _viewModel.SortItems();
        TidyResultTxt.Text = moved ? "Sorted." : "Already sorted.";
        if (!moved) return;
        CacheGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _editor.Revert();
        LoadGrid();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
