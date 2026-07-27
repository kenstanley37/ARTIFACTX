using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Unlike Exosuit (fixed capacity), a Freighter's Technology/Cargo max slots
/// depend on its Class, so the grids can't be built once in the constructor -
/// they're rebuilt in LoadGrids after reading the current class, same pattern
/// as ShipsPage/MultiToolPage. Unlike those pages there's no selector strip:
/// a player only ever actively manages one freighter's inventory (owning a
/// wider Frigate Fleet is a separate system, not exposed here).
/// </summary>
public sealed partial class FreighterPage : Page
{
    private const double TechCollapsedHeight = 232;
    private const double CargoCollapsedHeight = 368;

    private static readonly string ChevronDownGlyph = char.ConvertFromUtf32(0xE70D);
    private static readonly string ChevronUpGlyph = char.ConvertFromUtf32(0xE70E);

    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;

    // Class -> max Technology/Cargo slots, loaded once per page lifetime from
    // the catalog - shares the same "ShipCapacity" category as Ships, since
    // GcInventoryTable.ShipInventoryMaxUpgradeSize indexes "Freighter" as its
    // own ship type (see StarshipCapacity.ShipType.Freighter).
    private Dictionary<string, int>? _shipCapacity;

    // Model scene-path options for Type/Crew Race, loaded once per page
    // lifetime - options don't change mid-session. Same pattern as
    // MultiToolPage's Type combo.
    private sealed record SceneOption(string DisplayName, string ScenePath);
    private List<SceneOption>? _typeOptions;
    private List<SceneOption>? _crewRaceOptions;
    private bool _suppressTypeSelectionEvent;
    private bool _suppressCrewRaceSelectionEvent;
    private bool _suppressStatChangeEvent;

    public FreighterPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;

        LoadGrids();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadGrids);

    private async Task<Dictionary<string, int>> GetShipCapacityAsync()
    {
        _shipCapacity ??= await CatalogService.GetShipCapacityAsync();
        return _shipCapacity;
    }

    private static string? GetFreighterClassLetter() =>
        SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTechnologyPath.Append("B@N").Append("1o6").ToArray())?.Value<string>();

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string? classLetter = GetFreighterClassLetter();
        var capacity = await GetShipCapacityAsync();
        int techRows = StarshipCapacity.SlotsToRows(
            capacity.TryGetValue(StarshipCapacity.TechCapacityKey(StarshipCapacity.ShipType.Freighter, classLetter), out int techSlots)
                ? techSlots : StarshipCapacity.FallbackTechSlots);
        int cargoRows = StarshipCapacity.SlotsToRows(
            capacity.TryGetValue(StarshipCapacity.CargoCapacityKey(StarshipCapacity.ShipType.Freighter, classLetter), out int cargoSlots)
                ? cargoSlots : StarshipCapacity.FallbackCargoSlots);

        _techViewModel = new InventoryGridViewModel(NmsInventoryContainer.FreighterTechnologyPath, StarshipCapacity.Columns, techRows);
        _cargoViewModel = new InventoryGridViewModel(NmsInventoryContainer.FreighterCargoPath, StarshipCapacity.Columns, cargoRows);

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.ViewModel = _techViewModel;
        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = new[] { "Freighter", "All" };

        CargoGrid.ViewModel = _cargoViewModel;
        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        // Reload can run many times per page lifetime (class change, session
        // refresh) - unsubscribe first so CellChanged doesn't fire the
        // reset-button handler multiple times per single actual edit.
        TechGrid.CellChanged -= GridCellChanged;
        TechGrid.CellChanged += GridCellChanged;
        CargoGrid.CellChanged -= GridCellChanged;
        CargoGrid.CellChanged += GridCellChanged;

        TechGrid.Refresh();
        CargoGrid.Refresh();

        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;

        BuildClassSelector();
        NameEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterNamePath)?.Value<string>() ?? "";
        ModelSeedEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterModelSeedPath)?.Value<string>() ?? "";
        CrewSeedEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCrewSeedPath)?.Value<string>() ?? "";

        await SyncTypeComboBoxAsync();
        await SyncCrewRaceComboBoxAsync();
        UpdateStatDisplay();
    }

    private void GridCellChanged(object? sender, EventArgs e) =>
        PageResetBtn.Visibility = Visibility.Visible;

    /// <summary>Stages the rename when the field loses focus - not on every
    /// keystroke, matching Ships/MultiTool's own name-editing behavior.</summary>
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        string? currentName = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterNamePath)?.Value<string>();
        if (newName == currentName) return;

        SaveSessionManager.StageEdit(newName, NmsInventoryContainer.FreighterNamePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Loads the Type option list from the catalog once per page
    /// lifetime, then syncs the combo's selection to the current freighter's
    /// scene path - same pattern as MultiToolPage's SyncTypeComboBoxAsync.</summary>
    private async Task SyncTypeComboBoxAsync()
    {
        _suppressTypeSelectionEvent = true;

        if (_typeOptions is null)
        {
            var raw = await CatalogService.GetFreighterTypesAsync();
            _typeOptions = raw.Select(t => new SceneOption(t.DisplayName, t.ScenePath)).ToList();
            TypeComboBox.ItemsSource = _typeOptions;
        }

        string? currentPath = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>();
        TypeComboBox.SelectedItem = _typeOptions.FirstOrDefault(t =>
            string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));

        _suppressTypeSelectionEvent = false;
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTypeSelectionEvent) return;
        if (TypeComboBox.SelectedItem is not SceneOption option) return;

        SaveSessionManager.StageEdit(option.ScenePath, NmsInventoryContainer.FreighterTypePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private async Task SyncCrewRaceComboBoxAsync()
    {
        _suppressCrewRaceSelectionEvent = true;

        if (_crewRaceOptions is null)
        {
            var raw = await CatalogService.GetFreighterCrewRacesAsync();
            _crewRaceOptions = raw.Select(t => new SceneOption(t.DisplayName, t.ScenePath)).ToList();
            CrewRaceComboBox.ItemsSource = _crewRaceOptions;
        }

        string? currentPath = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCrewRacePath)?.Value<string>();
        CrewRaceComboBox.SelectedItem = _crewRaceOptions.FirstOrDefault(t =>
            string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));

        _suppressCrewRaceSelectionEvent = false;
    }

    private void CrewRaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressCrewRaceSelectionEvent) return;
        if (CrewRaceComboBox.SelectedItem is not SceneOption option) return;

        SaveSessionManager.StageEdit(option.ScenePath, NmsInventoryContainer.FreighterCrewRacePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void CrewSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = CrewSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCrewSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.FreighterCrewSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ModelSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = ModelSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterModelSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.FreighterModelSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void BuildClassSelector()
    {
        ClassSelectorPanel.Children.Clear();
        if (_techViewModel is null) return;

        foreach (var letter in new[] { "S", "A", "B", "C" })
        {
            bool isCurrent = string.Equals(_techViewModel.CurrentClass, letter, StringComparison.OrdinalIgnoreCase);

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = letter,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal
                },
                Padding = new Thickness(14, 4, 14, 4),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                BorderBrush = new SolidColorBrush(isCurrent
                    ? Color.FromArgb(255, 255, 157, 0)
                    : Color.FromArgb(255, 90, 98, 112)),
                Background = new SolidColorBrush(isCurrent
                    ? Color.FromArgb(60, 255, 157, 0)
                    : Color.FromArgb(20, 255, 255, 255))
            };

            button.Click += (_, _) =>
            {
                // A class change can change the freighter's real Tech/Cargo
                // slot totals (StarshipCapacity), so both grids get fully
                // rebuilt via LoadGrids rather than just refreshed in place.
                _techViewModel?.SetClass(letter);
                PageResetBtn.Visibility = Visibility.Visible;
                LoadGrids();
            };

            ClassSelectorPanel.Children.Add(button);
        }
    }

    /// <summary>Loads 0wS.@bB's raw continuous stat rolls (FREI_HYPERDRIVE/
    /// FREI_FLEET) into the editable NumberBoxes - same source/shape as
    /// Multi-Tool's OsQ.@bB, confirmed by real save data and cross-checked
    /// against NomNom's "Hyperdrive"/"Fleet Coordination" Base Stats.</summary>
    private void UpdateStatDisplay()
    {
        _suppressStatChangeEvent = true;

        var bonuses = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterStatBonusesPath) as JArray;

        double? Find(string key) =>
            bonuses?.FirstOrDefault(b => b["QL1"]?.Value<string>() == key)?[">MX"]?.Value<double>();

        HyperdriveStatBox.Value = Find("^FREI_HYPERDRIVE") ?? double.NaN;
        FleetStatBox.Value = Find("^FREI_FLEET") ?? double.NaN;

        _suppressStatChangeEvent = false;
    }

    /// <summary>Stages a new value for one @bB entry, matched by its QL1 key,
    /// rebuilding and staging the WHOLE @bB array at once - same reasoning as
    /// MultiToolPage.SetStatValue (a deeper leaf-only stage isn't seen by
    /// SaveSessionManager's staged-edit lookup, which only matches at the
    /// exact path queried).</summary>
    private void SetStatValue(string statKey, double newValue)
    {
        if (!SaveSessionManager.IsSaveLoaded || double.IsNaN(newValue)) return;

        var bonusesPath = NmsInventoryContainer.FreighterStatBonusesPath;
        var bonuses = SaveSessionManager.GetValue(bonusesPath) as JArray;
        if (bonuses is null) return;

        var updated = new JArray();
        foreach (var entry in bonuses)
        {
            if (entry is JObject obj && obj["QL1"]?.Value<string>() == statKey)
            {
                var clone = (JObject)obj.DeepClone();
                clone[">MX"] = newValue;
                updated.Add(clone);
            }
            else
            {
                updated.Add(entry.DeepClone());
            }
        }

        SaveSessionManager.StageEdit(updated, bonusesPath);
    }

    private void HyperdriveStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^FREI_HYPERDRIVE", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Redundant safety net alongside ValueChanged - some WinUI3
    /// NumberBox versions don't reliably fire ValueChanged on a plain focus
    /// loss, matching MultiToolPage's own DamageStatBox_LostFocus reasoning.</summary>
    private void HyperdriveStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(HyperdriveStatBox.Value))
        {
            SetStatValue("^FREI_HYPERDRIVE", HyperdriveStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    private void FleetStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^FREI_FLEET", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void FleetStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(FleetStatBox.Value))
        {
            SetStatValue("^FREI_FLEET", FleetStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
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
