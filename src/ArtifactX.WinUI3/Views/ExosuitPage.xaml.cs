using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views;

public sealed partial class ExosuitPage : Page
{
    // Matches the ScrollViewers' own XAML-declared MaxHeight (~3 rows for
    // Technology, ~5 for Cargo) - restored when a section is collapsed again.
    private const double TechCollapsedHeight = 232;
    private const double CargoCollapsedHeight = 368;

    // Segoe MDL2 Assets: E70D = ChevronDown, E70E = ChevronUp.
    private static readonly string ChevronDownGlyph = char.ConvertFromUtf32(0xE70D);
    private static readonly string ChevronUpGlyph = char.ConvertFromUtf32(0xE70E);

    private readonly InventoryGridViewModel _techViewModel =
        new(NmsInventoryContainer.ExosuitTechnologyPath, ExosuitCapacity.TechnologyColumns, ExosuitCapacity.TechnologyRows);

    private readonly InventoryGridViewModel _cargoViewModel =
        new(NmsInventoryContainer.ExosuitCargoPath, ExosuitCapacity.CargoColumns, ExosuitCapacity.CargoRows);

    public ExosuitPage()
    {
        InitializeComponent();
        TechGrid.ViewModel = _techViewModel;
        CargoGrid.ViewModel = _cargoViewModel;

        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = new[] { "Suit", "All" };

        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10; // Substances stay at the 1x default

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        LoadGrids();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrids);

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.Refresh();
        CargoGrid.Refresh();

        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UnlockAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.UnlockAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnlockAllCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel.UnlockAll();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SuperchargeAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.SuperchargeAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void RepairAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.RepairAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Toggles between the compact, game-matching preview height and
    /// showing every row at once - the ScrollViewer itself already sizes to
    /// whatever content it's given, so removing the cap (rather than
    /// computing an exact "full" height) is all expanding needs. The chevron
    /// flips to point the opposite direction so it always reads as "click to
    /// do this next", not as a static state label.</summary>
    private void TechExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        bool isCollapsed = TechScrollViewer.MaxHeight < double.PositiveInfinity;
        TechScrollViewer.MaxHeight = isCollapsed ? double.PositiveInfinity : TechCollapsedHeight;
        TechExpandIcon.Glyph = isCollapsed ? ChevronUpGlyph : ChevronDownGlyph;
        ToolTipService.SetToolTip(TechExpandBtn, isCollapsed ? "Collapse" : "Expand");
    }

    private void CargoExpandBtn_Click(object sender, RoutedEventArgs e)
    {
        bool isCollapsed = CargoScrollViewer.MaxHeight < double.PositiveInfinity;
        CargoScrollViewer.MaxHeight = isCollapsed ? double.PositiveInfinity : CargoCollapsedHeight;
        CargoExpandIcon.Glyph = isCollapsed ? ChevronUpGlyph : ChevronDownGlyph;
        ToolTipService.SetToolTip(CargoExpandBtn, isCollapsed ? "Collapse" : "Expand");
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel.Revert();
        TechGrid.Refresh();
        _cargoViewModel.Revert();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
