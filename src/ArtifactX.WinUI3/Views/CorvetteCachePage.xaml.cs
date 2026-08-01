using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views;

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
public sealed partial class CorvetteCachePage : Page
{
    // Matches the ScrollViewer's own XAML-declared MaxHeight (10 rows) -
    // restored when the section is collapsed again, same pattern as every
    // other page's Tech/Cargo expand toggle.
    private const double CollapsedHeight = 708;

    private static readonly string ChevronDownGlyph = char.ConvertFromUtf32(0xE70D);
    private static readonly string ChevronUpGlyph = char.ConvertFromUtf32(0xE70E);

    private readonly InventoryGridViewModel _viewModel =
        new(NmsInventoryContainer.CorvetteCachePath, CorvetteCacheCapacity.Columns, CorvetteCacheCapacity.Rows);

    public CorvetteCachePage()
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

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        LoadGrid();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrid);

    /// <summary>Without this, the constructor's subscriptions above never
    /// get released across page navigation (Frame.Navigate makes a fresh
    /// Page instance every visit, no NavigationCacheMode set anywhere in
    /// this app) - every past visit leaves a dead instance permanently
    /// subscribed, re-running its full reload on every future edit
    /// anywhere in the app. Root cause of a reported slowdown where
    /// editing any page became multi-second after enough navigation.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private async void LoadGrid()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _viewModel.Load();

        var itemIds = _viewModel.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        CacheGrid.Refresh();

        PageResetBtn.Visibility = _viewModel.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MaxAllQtyBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.MaxAllQuantities();
        CacheGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Revert();
        CacheGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    /// <summary>Toggles between the compact, ~10-row preview height and
    /// showing every row at once - same pattern as ExosuitPage/etc.</summary>
    private void CacheExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        bool isCollapsed = CacheScrollViewer.MaxHeight < double.PositiveInfinity;
        CacheScrollViewer.MaxHeight = isCollapsed ? double.PositiveInfinity : CollapsedHeight;
        CacheExpandIcon.Glyph = isCollapsed ? ChevronUpGlyph : ChevronDownGlyph;
        ToolTipService.SetToolTip(CacheExpandBtn, isCollapsed ? "Collapse" : "Expand");
    }
}
