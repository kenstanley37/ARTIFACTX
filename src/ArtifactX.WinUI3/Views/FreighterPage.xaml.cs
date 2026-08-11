using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
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

    // Set around SetStatValue's own StageEdit call so OnSessionOrEditsChanged
    // can skip the reload it would otherwise trigger for that same edit -
    // same fix and reasoning as ShipsPage.SetStatValue (2026-08-01): LoadGrids
    // recreates both InventoryGridViewModels and re-warms the catalog icon
    // cache on every call, none of which changes when only Hyperdrive/Fleet
    // Coordination is edited.
    private bool _suppressReloadOnOwnStatEdit;

    public FreighterPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadGrids();
        _ = RefreshTemplatesListAsync();
        _ = RefreshFullBuildTemplatesListAsync();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (_suppressReloadOnOwnStatEdit) return;
        DispatcherQueue.TryEnqueue(LoadGrids);
    }

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

    private async Task<Dictionary<string, int>> GetShipCapacityAsync()
    {
        _shipCapacity ??= await CatalogService.GetShipCapacityAsync();
        return _shipCapacity;
    }

    private static string? GetFreighterClassLetter() =>
        SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTechnologyPath.Append("B@N").Append("1o6").ToArray())?.Value<string>();

    /// <summary>The freighter hull's model scene path (bIR.93M) is empty
    /// until the player actually owns one - confirmed 2026-08-10 against a
    /// save that hadn't left the starting planet, where it was "" even
    /// though the Tech/Cargo containers (0wS/8ZP) and their supercharged-slot
    /// layout (hl?) already existed with real, non-default data. Those
    /// containers appear to be pre-allocated by the game from the start
    /// (same pattern as Multi-Tool/Ship's padding slots), so their mere
    /// presence isn't a reliable ownership signal - the hull scene path is.</summary>
    private static bool HasFreighter() =>
        !string.IsNullOrEmpty(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>());

    private async void LoadGrids()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        if (!HasFreighter())
        {
            NoFreighterTxt.Visibility = Visibility.Visible;
            HeaderGrid.Visibility = Visibility.Collapsed;
            return;
        }

        NoFreighterTxt.Visibility = Visibility.Collapsed;
        HeaderGrid.Visibility = Visibility.Visible;

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

    /// <summary>Rerolls the captain to a brand new random seed - see
    /// GenerateModelSeedBtn_Click above.</summary>
    private void GenerateCrewSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        CrewSeedEditBox.Text = newSeed;
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

    /// <summary>Rerolls the hull to a brand new random seed - see
    /// NmsSeedGenerator for why this is a plain reroll, not a targeted
    /// picker (shared with ShipsPage's own Generate button, and with
    /// GenerateCrewSeedBtn_Click above, so all three roll the same way).</summary>
    private void GenerateModelSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        ModelSeedEditBox.Text = newSeed;
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
    /// against a reference tool's own "Hyperdrive"/"Fleet Coordination" Base Stats.</summary>
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

        _suppressReloadOnOwnStatEdit = true;
        SaveSessionManager.StageEdit(updated, bonusesPath);
        _suppressReloadOnOwnStatEdit = false;
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

    private void MaxAllQtyCargo_Click(object sender, RoutedEventArgs e)
    {
        _cargoViewModel?.MaxAllQuantities();
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

    /// <summary>No "Copy Tech Stack..." button on this page unlike Ships/
    /// Multi-Tool - a save only ever has one freighter, so there's no other
    /// owned instance to copy onto. Templates (below) are the only loadout-
    /// sharing mechanism here, since they persist across SAVES rather than
    /// just between instances within one save - matching the user's own
    /// stated motivation ("a user may have multi saves and want to copy the
    /// tech stack").</summary>
    private async void SaveAsTemplateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_techViewModel is null) return;

        if (!_techViewModel.Cells.Any(c => c.IsOccupied))
        {
            await new ContentDialog
            {
                Title = "Nothing to save",
                Content = "Your freighter has no tech installed, so there's nothing to capture as a template.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var nameBox = new TextBox { PlaceholderText = "e.g. \"Combat Hauler\"" };
        var nameDialog = new ContentDialog
        {
            Title = "Save as Template",
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { new TextBlock { Text = "Name this loadout:" }, nameBox }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await nameDialog.ShowAsync() != ContentDialogResult.Primary) return;

        string name = nameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        string freighterName = NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "Freighter";
        var template = _techViewModel.ExtractTemplate(name, freighterName, "Freighter");
        await LoadoutTemplateService.SaveAsync(template);

        // No confirmation popup here on purpose - same reasoning as
        // ShipsPage/MultiToolPage: saving a template should visibly show up
        // in the list right away, not need a separate dialog to prove it worked.
        await RefreshTemplatesListAsync();
    }

    /// <summary>Rebuilds the persistent Tech Stack Templates list, filtered to
    /// Freighter-sourced, Scope=="TechStack" templates only - Ship/Multi-Tool
    /// templates share the same on-disk pool but don't belong in this grid.
    /// Full Build templates have their own separate list below
    /// (RefreshFullBuildTemplatesListAsync). Called after saving, deleting, or
    /// applying a template, and once on page load.</summary>
    private async Task RefreshTemplatesListAsync()
    {
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "Freighter" && t.Scope == "TechStack")
            .ToList();

        TemplatesListPanel.Children.Clear();

        if (templates.Count == 0)
        {
            TemplatesListPanel.Children.Add(new TextBlock
            {
                Text = "No templates saved yet.",
                FontSize = 11,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var template in templates)
        {
            string sourceInfo = template.SourceToolName is null ? "" : $" - from {template.SourceToolName}";

            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = $"{template.Name}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{template.TechItems.Count} items, {template.SourceClass ?? "?"} class{sourceInfo}",
                FontSize = 10,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            var applyButton = new Button { Content = "Apply", FontSize = 11, Padding = new Thickness(8, 3, 8, 3) };
            applyButton.Click += async (_, _) => await ApplyTemplateAsync(template);
            buttonRow.Children.Add(applyButton);

            var deleteButton = new Button { Content = "Delete", FontSize = 11, Padding = new Thickness(8, 3, 8, 3) };
            deleteButton.Click += async (_, _) =>
            {
                await LoadoutTemplateService.DeleteAsync(template.Id);
                await RefreshTemplatesListAsync();
            };
            buttonRow.Children.Add(deleteButton);

            row.Children.Add(buttonRow);

            row.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 4, 0, 0)
            });

            TemplatesListPanel.Children.Add(row);
        }
    }

    private async void SaveAsFullBuildTemplateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_techViewModel is null) return;

        if (!_techViewModel.Cells.Any(c => c.IsOccupied))
        {
            await new ContentDialog
            {
                Title = "Nothing to save",
                Content = "Your freighter has no tech installed, so there's nothing to capture as a template.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var nameBox = new TextBox { PlaceholderText = "e.g. \"Combat Hauler (Full Build)\"" };
        var nameDialog = new ContentDialog
        {
            Title = "Save as Full Build Template",
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { new TextBlock { Text = "Name this build (tech + stats + appearance):" }, nameBox }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await nameDialog.ShowAsync() != ContentDialogResult.Primary) return;

        string name = nameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        string freighterName = NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "Freighter";
        var template = _techViewModel.ExtractTemplate(name, freighterName, "Freighter");
        template.Scope = "FullBuild";
        template.Stats = ExtractStatsList();
        // Model Seed only (the hull's own look) - deliberately never the
        // Crew Seed (the captain's look, a separate concept), matching
        // NmsLoadoutTemplate.Seed's own doc comment.
        template.Seed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterModelSeedPath)?.Value<string>();

        await LoadoutTemplateService.SaveAsync(template);
        await RefreshFullBuildTemplatesListAsync();
    }

    /// <summary>Same shape as RefreshTemplatesListAsync, filtered to Scope==
    /// "FullBuild" instead and targeting the separate FullBuildTemplatesListPanel.</summary>
    private async Task RefreshFullBuildTemplatesListAsync()
    {
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "Freighter" && t.Scope == "FullBuild")
            .ToList();

        FullBuildTemplatesListPanel.Children.Clear();

        if (templates.Count == 0)
        {
            FullBuildTemplatesListPanel.Children.Add(new TextBlock
            {
                Text = "No full build templates saved yet.",
                FontSize = 11,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var template in templates)
        {
            string sourceInfo = template.SourceToolName is null ? "" : $" - from {template.SourceToolName}";

            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = $"{template.Name}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{template.TechItems.Count} items, {template.SourceClass ?? "?"} class, stats+seed{sourceInfo}",
                FontSize = 10,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap
            });

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            var applyButton = new Button { Content = "Apply", FontSize = 11, Padding = new Thickness(8, 3, 8, 3) };
            applyButton.Click += async (_, _) => await ApplyFullBuildTemplateAsync(template);
            buttonRow.Children.Add(applyButton);

            var deleteButton = new Button { Content = "Delete", FontSize = 11, Padding = new Thickness(8, 3, 8, 3) };
            deleteButton.Click += async (_, _) =>
            {
                await LoadoutTemplateService.DeleteAsync(template.Id);
                await RefreshFullBuildTemplatesListAsync();
            };
            buttonRow.Children.Add(deleteButton);

            row.Children.Add(buttonRow);

            row.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 4, 0, 0)
            });

            FullBuildTemplatesListPanel.Children.Add(row);
        }
    }

    /// <summary>Reads the freighter's whole stat-bonus array (@bB) as a plain
    /// key/value list, for embedding in a Full Build template - captures every
    /// entry generically (not just the 2 keys exposed as NumberBoxes) so
    /// nothing present but not individually surfaced gets silently dropped on
    /// save. Only one freighter per save, so unlike ShipsPage's version this
    /// takes no index parameter.</summary>
    private static List<NmsLoadoutStat> ExtractStatsList()
    {
        var bonuses = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterStatBonusesPath) as JArray;
        var result = new List<NmsLoadoutStat>();
        if (bonuses is null) return result;

        foreach (var entry in bonuses)
        {
            string? key = entry["QL1"]?.Value<string>();
            double? value = entry[">MX"]?.Value<double>();
            if (key is not null && value is not null)
                result.Add(new NmsLoadoutStat { Key = key, Value = value.Value });
        }

        return result;
    }

    /// <summary>Stages a Full Build template's saved stats onto the freighter -
    /// rebuilds the whole @bB array matched by key, same "match by key,
    /// replace whole array" pattern as SetStatValue.</summary>
    private static void ApplyStatsList(List<NmsLoadoutStat> stats)
    {
        if (stats.Count == 0) return;

        var bonusesPath = NmsInventoryContainer.FreighterStatBonusesPath;
        var bonuses = SaveSessionManager.GetValue(bonusesPath) as JArray;
        if (bonuses is null) return;

        var statsByKey = stats.ToDictionary(s => s.Key, s => s.Value);

        var updated = new JArray();
        foreach (var entry in bonuses)
        {
            if (entry is JObject obj && obj["QL1"]?.Value<string>() is string key && statsByKey.TryGetValue(key, out double newValue))
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

    /// <summary>Applies a saved Full Build template's tech, stats, and Model
    /// Seed to the freighter - Class only changes if the user opts in, same
    /// as ApplyTemplateAsync.</summary>
    private async Task ApplyFullBuildTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_techViewModel is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await ShowApplyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, includesStatsAndSeed: true);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        ApplyStatsList(template.Stats);
        if (!string.IsNullOrEmpty(template.Seed))
            SaveSessionManager.StageEdit(template.Seed, NmsInventoryContainer.FreighterModelSeedPath);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
    }

    /// <summary>Applies a saved Tech Stack template to the freighter - same
    /// flow as Ships/MultiTool's ApplyTemplateAsync, minus the target picker
    /// step (only one freighter exists to apply onto). A class change
    /// (alsoMatchClass) can change the freighter's real Tech/Cargo slot
    /// totals (StarshipCapacity), so the grids get fully rebuilt via
    /// LoadGrids rather than just refreshed in place.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_techViewModel is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await ShowApplyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
    }

    /// <summary>Confirmation dialog for applying a template - simpler than
    /// Ships/MultiTool's ShowCopyConfirmationAsync since a save only ever has
    /// one freighter: no target picker, just "apply this template to your
    /// freighter now."</summary>
    private async Task<(bool Confirmed, bool AlsoMatchClass)> ShowApplyConfirmationAsync(
        string sourceLabel, string? sourceClass, IEnumerable<(int X, int Y)> sourcePositions, bool includesStatsAndSeed = false)
    {
        if (_techViewModel is null) return (false, false);

        var sourcePositionSet = sourcePositions.ToHashSet();
        var targetUnlocked = _techViewModel.Cells.Where(c => c.State != InventorySlotState.Locked).Select(c => (c.X, c.Y)).ToHashSet();
        int newSlotsNeeded = sourcePositionSet.Except(targetUnlocked).Count();

        bool classDiffers = !string.Equals(sourceClass, _techViewModel.CurrentClass, StringComparison.OrdinalIgnoreCase);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = includesStatsAndSeed
                ? $"This replaces your freighter's entire tech loadout, base stats, and appearance seed with {sourceLabel}."
                : $"This replaces your freighter's entire tech loadout with {sourceLabel}."
        });

        if (newSlotsNeeded > 0)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 160, 60)),
                Text = $"⚠ {newSlotsNeeded} slot(s) will also be unlocked on your freighter to fit this stack."
            });
        }

        CheckBox? matchClassBox = null;
        if (classDiffers)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 160, 60)),
                Text = $"⚠ Class differs: your freighter is currently {_techViewModel.CurrentClass ?? "unknown"}, source is {sourceClass ?? "unknown"}."
            });

            matchClassBox = new CheckBox { Content = $"Also change your freighter's class to {sourceClass} to match" };
            panel.Children.Add(matchClassBox);
        }

        var dialog = new ContentDialog
        {
            Title = includesStatsAndSeed ? "Confirm full build apply" : "Confirm tech stack apply",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        bool confirmed = await dialog.ShowAsync() == ContentDialogResult.Primary;
        return (confirmed, matchClassBox?.IsChecked == true);
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
