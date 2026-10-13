using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// Shared UI/logic for one of the 7 fixed exocraft slots (Roamer/Nomad/
/// Colossus/Pilgrim/Dragonfly/Nautilon/Minotaur) - same "thin Page hosts one
/// shared control" split as GekLanguagePage/LanguageWordsControl, since all 7
/// pages are identical except which ExocraftType they target. No Templates
/// system here yet (unlike Ships/Multi-Tool/Freighter) - deliberately kept
/// out of this first pass to keep it shippable; LoadoutTemplateService/
/// NmsLoadoutTemplate are already fully generic (SourceKind is just a
/// string) so adding "Exocraft" support later needs no model changes.
/// </summary>
public sealed partial class ExocraftDetailControl : UserControl
{
    private const double TechCollapsedHeight = 232;
    private const double CargoCollapsedHeight = 368;

    private static readonly string ChevronDownGlyph = char.ConvertFromUtf32(0xE70D);
    private static readonly string ChevronUpGlyph = char.ConvertFromUtf32(0xE70E);

    private ExocraftType _type;
    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;

    public ExocraftDetailControl()
    {
        InitializeComponent();
    }

    public void Initialize(ExocraftType type)
    {
        _type = type;
        TitleTxt.Text = ExocraftCapacity.DisplayName(type);

        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = ExocraftCapacity.UsageCategories(type);
        // An exocraft's slots are fixed by the game (see SupportsUnlock).
        TechGrid.SupportsUnlock = false;
        CargoGrid.SupportsUnlock = false;

        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        TechGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;
        CargoGrid.CellChanged += (_, _) => PageResetBtn.Visibility = Visibility.Visible;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Control_Unloaded;

        LoadGrids();

        // The tech this vehicle can fit, to click in - the same list limits
        // the right-click search.
        TechListHeaderTxt.Text = $"{ExocraftCapacity.DisplayName(type).ToUpperInvariant()} TECHNOLOGY";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(ExocraftCapacity.UsageCategories(type));
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // The game names UP_EXSUB "Nautilon Cannon" too, but it's the
            // Humboldt Drive's: template T_SUB, the engine stat (2026-10-13).
            var modules = (await CatalogService.GetUpgradeModulesAsync(ModuleFamiliesFor(type)))
                .Select(m => m.CountKey == "UP_EXSUB" ? m with { Name = "Humboldt Drive upgrade (S)" } : m);
            return ItemListPanel.SortByGroups(tech.Concat(modules), TechGroup, TechGroupOrder);
        }, "technology", "technologies", "Technology", TechGroup);
    }

    /// <summary>Upgrade module families per vehicle, from every exocraft in
    /// the real saves (2026-10-13), each topping out at 3: the wheeled ones
    /// and Dragonfly share the exocraft modules, the Nautilon has two cannon
    /// families (both seen at 3 together), the Minotaur its own four.</summary>
    private static string[] ModuleFamiliesFor(ExocraftType type) => type switch
    {
        ExocraftType.Nautilon => new[] { "UP_SUGUN", "UP_EXSUB" },
        ExocraftType.Minotaur => new[] { "UP_MCENG", "UP_MFIRE", "UP_MCGUN", "UP_MCLAS" },
        _ => new[] { "UP_EXENG", "UP_BOOST", "UP_EXLAS", "UP_EXGUN" },
    };

    private static readonly string[] TechGroupOrder =
        { "Engine & handling", "Mining", "Weapons", "Scanning", "Hazard protection", "Minotaur parts", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats). Vehicle stats are loosely
    /// named in the game data (engines use Vehicle_Scan), so they're mapped
    /// to what the tech does; the Minotaur's swappable body parts get their
    /// own group.</summary>
    private static string TechGroup(CatalogPart tech)
    {
        if (tech.Name.StartsWith("HARDFRAME", StringComparison.OrdinalIgnoreCase)
            || tech.Name.StartsWith("LIQUIDATOR", StringComparison.OrdinalIgnoreCase)) return "Minotaur parts";
        return (tech.Stat ?? "") switch
        {
            "Vehicle_Scan" or "Vehicle_Engine" => "Engine & handling",
            "Vehicle_LaserDamage" or "Vehicle_FuelRegen" => "Mining",
            "Vehicle_GunDamage" or "Vehicle_FlameDamage" => "Weapons",
            "Vehicle_EngineFuelUse" or "Weapon_Scan_Binoculars" => "Scanning",
            var s when s.StartsWith("Suit_Protection") => "Hazard protection",
            _ => "Utilities",
        };
    }

    private void TechCargoStack_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutTechList();
    private void MainColumn_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutTechList();

    /// <summary>Tech list beside tech + cargo, as tall as they are, or
    /// underneath at a fixed height when the window is too narrow.</summary>
    private void LayoutTechList()
    {
        bool beside = MainColumn.ActualWidth >= TechCargoStack.ActualWidth + TechLayoutGrid.ColumnSpacing + TechListCard.Width;
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

    /// <summary>Same leak fix as every other device page in this app (see
    /// project_static_event_leak_fix) - Frame.Navigate creates a fresh Page
    /// (and therefore a fresh instance of this control) on every visit, so
    /// the subscriptions above must be released here or every past visit
    /// keeps reloading on every future edit anywhere in the app.</summary>
    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        _techViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ExocraftTechnologyPath(_type), ExocraftCapacity.Columns, ExocraftCapacity.TechRows(_type));
        _cargoViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ExocraftCargoPath(_type), ExocraftCapacity.Columns, ExocraftCapacity.CargoRows(_type));

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.ViewModel = _techViewModel;
        CargoGrid.ViewModel = _cargoViewModel;
        TechGrid.Refresh();
        CargoGrid.Refresh();
        TechListPanel.RefreshOwned();

        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;

        ModelSeedEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.ExocraftModelSeedPath(_type))?.Value<string>() ?? "";
    }

    private void ModelSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = ModelSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsInventoryContainer.ExocraftModelSeedPath(_type))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.ExocraftModelSeedPath(_type));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenerateModelSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        ModelSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.ExocraftModelSeedPath(_type));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnlockAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.UnlockAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnlockAllCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel?.UnlockAll();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MaxAllQtyCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel?.MaxAllQuantities();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SortCargo_Click(object sender, RoutedEventArgs e)
    {
        if (_cargoViewModel?.SortItems() != true) return;
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SuperchargeAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.SuperchargeAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void RepairAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.RepairAll();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Visible;
    }

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
        _techViewModel?.Revert();
        TechGrid.Refresh();
        _cargoViewModel?.Revert();
        CargoGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
