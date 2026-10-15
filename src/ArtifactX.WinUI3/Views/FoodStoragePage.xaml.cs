using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The fishing/cooking update's two stores (2026-10-15), each at a fixed key
/// (NmsInventoryContainer): Cold Storage (fish and bait, 60 slots) and the
/// cooking Ingredient Storage (50 slots). Like Corvette Cache, each grid has
/// the items it can take listed beside it. Which items fit comes from the
/// user's in-game tests (Cold Storage: fish + bait) and the game's own
/// CookingIngredient flag (Ingredient Storage) - see
/// CatalogService.GetFoodStorageItemsAsync.
/// </summary>
public sealed partial class FoodStoragePage : Page
{
    private readonly InventoryGridViewModel _cold = new(NmsInventoryContainer.ColdStoragePath,
        NmsInventoryContainer.ColdStorageColumns, NmsInventoryContainer.ColdStorageRows);
    private readonly InventoryGridViewModel _ingredient = new(NmsInventoryContainer.IngredientStoragePath,
        NmsInventoryContainer.IngredientStorageColumns, NmsInventoryContainer.IngredientStorageRows);

    public FoodStoragePage()
    {
        InitializeComponent();
        // Stack sizes from the game's own screens: Cold Storage fish 100 / bait
        // 1,000 (20x the catalog), Ingredient Storage fish 25 / food 50 (5x).
        Setup(ColdGrid, _cold, 20);
        Setup(IngredientGrid, _ingredient, 5);

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
            SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        };
        LoadGrids();

        _ = ColdList.LoadAsync(ColdGrid, async () =>
        {
            var items = await CatalogService.GetFoodStorageItemsAsync(coldStorage: true);
            ColdGrid.AllowedItemIds = items.Select(p => p.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return items;
        }, "item", "items", "Product", p => p.Group);
        _ = IngredientList.LoadAsync(IngredientGrid, async () =>
        {
            var items = await CatalogService.GetFoodStorageItemsAsync(coldStorage: false);
            IngredientGrid.AllowedItemIds = items.Select(p => p.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return items;
        }, "ingredient", "ingredients", "Product", p => p.Group);
    }

    private void Setup(InventorySlotGrid grid, InventoryGridViewModel model, int multiplier)
    {
        grid.ViewModel = model;
        grid.AllowedCategories = new[] { "Product", "Substance" };
        grid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        grid.SupportsSupercharge = false;
        grid.SupportsRepair = false;
        grid.ProductStorageMultiplier = multiplier;
        grid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(LoadGrids);

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        _cold.Load();
        _ingredient.Load();
        var ids = _cold.Cells.Concat(_ingredient.Cells).Where(c => c.IsOccupied).Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(ids);
        ColdGrid.Refresh();
        IngredientGrid.Refresh();
        PageResetBtn.Visibility = _cold.HasLocalChanges || _ingredient.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    private (InventoryGridViewModel Model, InventorySlotGrid Grid, TextBlock Result) Target(object sender) =>
        (sender as FrameworkElement)?.Tag as string == "Cold" ? (_cold, ColdGrid, ColdResultTxt) : (_ingredient, IngredientGrid, IngredientResultTxt);

    private void MaxAllQtyBtn_Click(object sender, RoutedEventArgs e)
    {
        var (model, grid, _) = Target(sender);
        model.MaxAllQuantities();
        grid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MergeDuplicatesBtn_Click(object sender, RoutedEventArgs e)
    {
        var (model, grid, result) = Target(sender);
        int freed = model.MergeDuplicates();
        result.Text = freed == 0 ? "No duplicates found." : $"Merged duplicates - {freed} slot{(freed == 1 ? "" : "s")} freed.";
        if (freed == 0) return;
        grid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SortBtn_Click(object sender, RoutedEventArgs e)
    {
        var (model, grid, result) = Target(sender);
        bool moved = model.SortItems();
        result.Text = moved ? "Sorted." : "Already sorted.";
        if (!moved) return;
        grid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _cold.Revert();
        _ingredient.Revert();
        ColdGrid.Refresh();
        IngredientGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
