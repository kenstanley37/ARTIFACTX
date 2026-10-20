using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
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
        // Every tech on the 24 exosuits in the real saves is "Suit" (2026-10-13).
        TechGrid.AllowedUsageCategories = new[] { "Suit" };

        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        LoadGrids();
        LoadTechList();
    }

    /// <summary>Exosuit module families, from every suit in the real saves
    /// (2026-10-13): each tops out at 3 per suit. UP_HAZ only exists as
    /// class 0/X (no shop module) and UP_SNSUIT/UP_RBSUIT are one-off
    /// salvaged/anomalous finds, so they aren't offered.</summary>
    private static readonly string[] ModuleFamilies =
        { "UP_ENGY", "UP_JET", "UP_SHLD", "UP_HOT", "UP_COLD", "UP_TOX", "UP_RAD", "UP_UNW" };

    private static readonly string[] TechGroupOrder =
        { "Life support", "Hazard protection", "Movement", "Shields", "Translators", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    private static string TechGroup(CatalogPart tech) => (tech.Stat ?? "") switch
    {
        "Suit_Energy" => "Life support",
        "Suit_Protection" => "Hazard protection",
        "Suit_Jetpack" or "Suit_Underwater" => "Movement",
        "Suit_Armour_Shield" => "Shields",
        "Suit_AutoTranslator" => "Translators",
        _ => "Utilities",
    };

    private void LoadTechList()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        // In game only one personal refiner fits at a time (user, 2026-10-13).
        TechListPanel.OneOf = new[] { new[] { "SUIT_REFINER", "SUIT_REFINER2" } };
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(new[] { "Suit" });
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(ModuleFamilies);
            return ItemListPanel.SortByGroups(tech.Concat(modules), TechGroup, TechGroupOrder);
        }, "technology", "technologies", "Technology", TechGroup);
    }

    private void TechCargoStack_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutTechList();
    private void MainColumn_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutTechList();

    /// <summary>Tech list beside tech + cargo, as tall as they are, or
    /// underneath at a fixed height when the window is too narrow.</summary>
    private void LayoutTechList()
    {
        double inner = MainColumn.ActualWidth - MainColumn.Padding.Left - MainColumn.Padding.Right;
        bool beside = inner >= TechCargoStack.ActualWidth + TechLayoutGrid.ColumnSpacing + TechListCard.Width;
        Grid.SetRow(TechListCard, beside ? 0 : 1);
        Grid.SetColumn(TechListCard, beside ? 1 : 0);
        double height = 560;
        if (beside)
        {
            double chrome = TechListCard.ActualHeight - TechListPanel.ListHeight;
            if (TechListCard.ActualHeight <= 0 || chrome <= 0) chrome = 230;
            height = Math.Max(300, TechCargoStack.ActualHeight - chrome);
        }
        if (Math.Abs(height - TechListPanel.ListHeight) > 1) TechListPanel.ListHeight = height;
    }

    private void ArrangeTech_Click(object sender, RoutedEventArgs e)
    {
        int moved = TechGrid.ArrangeForAdjacency(TechListPanel.TypeOf);
        if (moved > 0) PageResetBtn.Visibility = Visibility.Visible;
        FlyoutBase.SetAttachedFlyout((FrameworkElement)sender, new Flyout
        {
            Content = new TextBlock
            {
                Text = moved > 0
                    ? $"Moved {moved} so each type sits together. Reset undoes it."
                    : "Already grouped - nothing to move.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260,
            },
        });
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrids);

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
        TechListPanel.RefreshOwned();

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

    private void MaxAllQtyCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel.MaxAllQuantities();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SortCargo_Click(object sender, RoutedEventArgs e)
    {
        if (_cargoViewModel.SortItems() != true) return;
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
        TechListPanel.RefreshOwned();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
