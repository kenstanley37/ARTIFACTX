using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// One of the fishing/cooking update's stores (2026-10-15), each at a fixed
/// key (NmsInventoryContainer): Cold Storage (fish and bait, 60 slots) or the
/// cooking Ingredient Storage (50 slots), with the items it can take listed
/// beside it. Which items fit comes from the user's in-game tests (Cold
/// Storage: fish + bait) and the game's own CookingIngredient flag
/// (Ingredient Storage) - see CatalogService.GetFoodStorageItemsAsync.
/// Hosted by ColdStoragePage and IngredientStoragePage.
/// </summary>
public sealed partial class FoodStoreControl : UserControl
{
    private InventoryGridViewModel? _model;

    /// <summary>Raised when this store is edited (the page shows its Reset button).</summary>
    public event EventHandler? Changed;

    public FoodStoreControl() => InitializeComponent();

    public bool HasLocalChanges => _model?.HasLocalChanges == true;

    /// <summary>Sets the store up; call once from the hosting page.</summary>
    public void Init(bool coldStorage)
    {
        _model = coldStorage
            ? new InventoryGridViewModel(NmsInventoryContainer.ColdStoragePath, NmsInventoryContainer.ColdStorageColumns, NmsInventoryContainer.ColdStorageRows)
            : new InventoryGridViewModel(NmsInventoryContainer.IngredientStoragePath, NmsInventoryContainer.IngredientStorageColumns, NmsInventoryContainer.IngredientStorageRows);
        GridTitleTxt.Text = coldStorage ? "COLD STORAGE" : "INGREDIENT STORAGE";
        ListTitleTxt.Text = coldStorage ? "FISH AND BAIT" : "COOKING INGREDIENTS";

        Grid.ViewModel = _model;
        Grid.AllowedCategories = new[] { "Product", "Substance" };
        Grid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        Grid.SupportsSupercharge = false;
        Grid.SupportsRepair = false;
        // Stack sizes from the game's own screens: Cold Storage fish 100 / bait
        // 1,000 (20x the catalog), Ingredient Storage fish 25 / food 50 (5x).
        Grid.ProductStorageMultiplier = coldStorage ? 20 : 5;
        Grid.CellChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

        Reload();
        _ = List.LoadAsync(Grid, async () =>
        {
            var items = await CatalogService.GetFoodStorageItemsAsync(coldStorage);
            Grid.AllowedItemIds = items.Select(p => p.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return items;
        }, coldStorage ? "item" : "ingredient", coldStorage ? "items" : "ingredients", "Product", p => p.Group);
    }

    public async void Reload()
    {
        if (_model is null || !SaveSessionManager.IsSaveLoaded) return;
        _model.Load();
        await CatalogService.WarmCacheAsync(_model.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId));
        Grid.Refresh();
    }

    public void Revert()
    {
        _model?.Revert();
        Grid.Refresh();
    }

    private void MaxAllQtyBtn_Click(object sender, RoutedEventArgs e)
    {
        _model?.MaxAllQuantities();
        Grid.Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void MergeDuplicatesBtn_Click(object sender, RoutedEventArgs e)
    {
        int freed = _model?.MergeDuplicates() ?? 0;
        ResultTxt.Text = freed == 0 ? "No duplicates found." : $"Merged duplicates - {freed} slot{(freed == 1 ? "" : "s")} freed.";
        if (freed == 0) return;
        Grid.Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SortBtn_Click(object sender, RoutedEventArgs e)
    {
        bool moved = _model?.SortItems() ?? false;
        ResultTxt.Text = moved ? "Sorted." : "Already sorted.";
        if (!moved) return;
        Grid.Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
