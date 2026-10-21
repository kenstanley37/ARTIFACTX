using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Controls;
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
public sealed partial class FreighterPage : Page, ILoadoutTemplateHost
{


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
        TemplatesPanel.Host = this;
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
        SaveSessionManager.GetValue(NmsInventoryContainer.FreighterClassPath)?.Value<string>();

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

    /// <summary>Freighter module families, from the 24 freighters in the real
    /// saves (2026-10-13): hyperdrive plus the six fleet ones, each topping
    /// out at 3.</summary>
    private static readonly string[] ModuleFamilies =
        { "UP_FRHYP", "UP_FRCOM", "UP_FREXP", "UP_FRFUE", "UP_FRMIN", "UP_FRSPE", "UP_FRTRA" };

    private static readonly string[] TechGroupOrder = { "Hyperdrive", "Fleet", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    private static string TechGroup(CatalogPart tech)
    {
        string stat = tech.Stat ?? "";
        if (stat.StartsWith("Freighter_Hyperdrive")) return "Hyperdrive";
        if (stat.StartsWith("Freighter_Fleet")) return "Fleet";
        return "Utilities";
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

    private async void LoadGrids() => await LoadGridsAsync();

    private async Task LoadGridsAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        if (!HasFreighter())
        {
            NoFreighterPanel.Visibility = Visibility.Visible;
            HeaderGrid.Visibility = Visibility.Collapsed;
            _ = RefreshAddFreighterListAsync();
            return;
        }

        NoFreighterPanel.Visibility = Visibility.Collapsed;
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
        // Every tech on the 24 freighters in the real saves is "Freighter" (2026-10-13).
        TechGrid.AllowedUsageCategories = new[] { "Freighter" };

        CargoGrid.ViewModel = _cargoViewModel;
        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
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

        // The tech a freighter can fit, to click in - the same list limits
        // the right-click search.
        TechListPanel.IsBestClass = () => GetFreighterClassLetter() == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            SaveSessionManager.StageEdit("S", NmsInventoryContainer.FreighterClassPath);
            PageResetBtn.Visibility = Visibility.Visible;
            await LoadGridsAsync();
        };
        TechListPanel.MoreRoomHint = () => GetFreighterClassLetter() == "S" ? null
            : "S class has more slots - change the class in Freighter Details to fit more.";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(new[] { "Freighter" });
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(ModuleFamilies);
            return Controls.ItemListPanel.SortByGroups(tech.Concat(modules), TechGroup, TechGroupOrder);
        }, "technology", "technologies", "Technology", TechGroup);

        ShowClass();
        NameEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterNamePath)?.Value<string>() ?? "";
        ShowFoldedName();
        HomeSeedEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterHomeSeedPath)?.Value<string>() ?? "";
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

    private void HomeSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = HomeSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterHomeSeedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.FreighterHomeSeedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Same full 64-bit random range as Model/Crew Seed - a reference
    /// tool's own Generate button does the same for Home Seed despite the
    /// CURRENT value here often printing shorter (its underlying field isn't
    /// zero-padded, so a numerically small GalacticAddress just shows fewer
    /// hex digits).</summary>
    private void GenerateHomeSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        HomeSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.FreighterHomeSeedPath);
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

    /// <summary>Reads/writes NmsInventoryContainer.FreighterClassPath (8ZP,
    /// Cargo) directly rather than going through _techViewModel.CurrentClass/
    /// SetClass (tied to 0wS, Technology) the way Ships/MultiTool's own class
    /// selectors do - see FreighterClassPath's own doc comment for why
    /// Freighter specifically needs its own separate read/write here.</summary>
    private void ShowClass()
    {
        ClassPicker.Visibility = !SaveSessionManager.IsSaveLoaded ? Visibility.Collapsed : Visibility.Visible;
        ClassPicker.SelectedClass = GetFreighterClassLetter();
    }

    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        // A class change can change the freighter's real Tech/Cargo
        // slot totals (StarshipCapacity), so both grids get fully
        // rebuilt via LoadGrids rather than just refreshed in place.
        SaveSessionManager.StageEdit(letter, NmsInventoryContainer.FreighterClassPath);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
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

    /// <summary>The whole freighter as a Full Build template: tech, unlocked
    /// slots, stats, look and furnished interior.</summary>
    private NmsLoadoutTemplate BuildFullBuildTemplate(string name)
    {
        string freighterName = NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "Freighter";
        var template = _techViewModel!.ExtractTemplate(name, freighterName, "Freighter");
        template.Scope = "FullBuild";
        template.Stats = ExtractStatsList();
        // Model Seed only (the hull's own look) - deliberately never the
        // Crew Seed (the captain's look, a separate concept), matching
        // NmsLoadoutTemplate.Seed's own doc comment.
        template.Seed = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterModelSeedPath)?.Value<string>();
        template.TypeScenePath = SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>();
        FreighterBuildService.Capture(template);
        // Every unlocked slot, not just the ones holding tech (a Tech Stack
        // template keeps only those, so it never unlocks extra slots).
        template.UnlockedPositions = _techViewModel.Cells.Where(c => c.State != InventorySlotState.Locked)
            .Select(c => new NmsLoadoutPosition { X = c.X, Y = c.Y }).ToList();
        return template;
    }

    private async void DeleteFreighterBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_techViewModel is null) return;
        string name = NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "your freighter";
        int cargo = _cargoViewModel?.Cells.Count(c => c.IsOccupied) ?? 0;
        int frigates = (SaveSessionManager.GetValue(NmsFrigatePaths.FrigateArrayPath) as JArray)?.Count ?? 0;
        string whatGoes = "its tech, its furnished interior"
            + (cargo > 0 ? $", the {cargo} stacks in its cargo" : "")
            + " and its look. What you've learned (blueprints, known tech) stays"
            + (frigates > 0 ? $", and your {frigates} frigate{(frigates == 1 ? "" : "s")} stay as they are" : "");
        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, name, whatGoes);
        if (!choice.Confirmed) return;

        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(BuildFullBuildTemplate($"{(name == "your freighter" ? "Freighter" : name)} (saved before deleting)"));
            await TemplatesPanel.RefreshAsync();
        }
        FreighterBuildService.RemoveFreighter();
        LoadGrids();
    }

    /// <summary>The "Add a freighter" card shown when the save has none:
    /// every Freighter Full Build template, each with Add as my freighter.</summary>
    private void BuildStarterList()
    {
        if (StarterListPanel.Children.Count > 0) return;
        foreach (var (name, description, scene) in FreighterBuildService.StarterHulls)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel { Spacing = 2 };
            info.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            info.Children.Add(new TextBlock { Text = description, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(info);
            var add = new Button { Content = "Add as my freighter", VerticalAlignment = VerticalAlignment.Center };
            add.Click += async (_, _) => await AddFreighterAsync(FreighterBuildService.Starter(name, scene));
            Grid.SetColumn(add, 1);
            row.Children.Add(add);
            StarterListPanel.Children.Add(row);
        }
    }

    private async Task RefreshAddFreighterListAsync()
    {
        BuildStarterList();
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "Freighter" && t.Scope == "FullBuild")
            .ToList();

        AddFreighterListPanel.Children.Clear();
        if (templates.Count == 0)
        {
            AddFreighterListPanel.Children.Add(new TextBlock
            {
                Text = "No freighter templates yet.",
                FontSize = 12,
                Opacity = 0.7,
            });
            return;
        }

        foreach (var template in templates)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel { Spacing = 2 };
            info.Children.Add(new TextBlock { Text = template.Name, FontWeight = FontWeights.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
            info.Children.Add(new TextBlock
            {
                Text = $"{template.SourceClass ?? "?"} class, {template.TechItems.Count} tech"
                    + (FreighterBuildService.HasInterior(template) ? $", interior with {template.InteriorObjects!.Count} parts" : ", default interior (saved before interiors were included)")
                    + (template.SourceToolName is { Length: > 0 } source ? $" - from {source}" : ""),
                FontSize = 11,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
            row.Children.Add(info);

            var add = new Button { Content = "Add as my freighter", VerticalAlignment = VerticalAlignment.Center };
            add.Click += async (_, _) => await AddFreighterAsync(template);
            Grid.SetColumn(add, 1);
            row.Children.Add(add);
            AddFreighterListPanel.Children.Add(row);
        }
    }

    private async void AddFreighterImport_Click(object sender, RoutedEventArgs e)
    {
        var result = await TemplateSharingService.ImportAsync("Freighter");
        if (result is not { } r) return;
        AddFreighterStatusTxt.Text = r.Ok && r.Message.Contains("Use Apply", StringComparison.Ordinal)
            ? r.Message.Replace("Use Apply", "Use Add as my freighter", StringComparison.Ordinal) : r.Message;
        AddFreighterStatusTxt.Foreground = ThemeResources.Brush(r.Ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        AddFreighterStatusTxt.Visibility = Visibility.Visible;
        if (r.Ok) await RefreshAddFreighterListAsync();
    }

    /// <summary>Builds a whole freighter from the template (FreighterBuildService)
    /// in a save that doesn't have one. Nothing is written until Save.</summary>
    private async Task AddFreighterAsync(NmsLoadoutTemplate template)
    {
        bool fresh = template.TechItems.Count <= 2 && !FreighterBuildService.HasInterior(template);
        var confirm = new ContentDialog
        {
            Title = fresh ? $"Add a new {template.Name.ToLowerInvariant()}?" : $"Add \"{template.Name}\" as your freighter?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = fresh
                    ? "A brand-new one, just as the game hands it over: C class, a hyperdrive and scanner, the starter interior and a random look, "
                        + "parked in the system you're in, with a new Gek captain (change the race under Crew Race). Nothing is written to your save until you Save."
                    : "It arrives with this template's model, look, tech, class, stats and "
                    + (FreighterBuildService.HasInterior(template) ? "furnished interior" : "the standard starter interior")
                    + ", parked in the system you're in. Its cargo starts empty and it gets a new Gek captain (change the race under Crew Race). "
                    + "Nothing is written to your save until you Save."
            },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        if (FreighterBuildService.AddFreighter(template) is string problem)
        {
            AddFreighterStatusTxt.Text = $"Couldn't add the freighter: {problem}";
            AddFreighterStatusTxt.Foreground = ThemeResources.Brush("ThemeWarningBrush");
            AddFreighterStatusTxt.Visibility = Visibility.Visible;
            return;
        }
        LoadGrids();
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
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, includesStatsAndSeed: true,
            replacesInterior: FreighterBuildService.HasInterior(template));

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        FreighterBuildService.ApplyInterior(template);
        ApplyStatsList(template.Stats);
        if (!string.IsNullOrEmpty(template.Seed))
            SaveSessionManager.StageEdit(template.Seed, NmsInventoryContainer.FreighterModelSeedPath);
        if (!string.IsNullOrEmpty(template.TypeScenePath))
            SaveSessionManager.StageEdit(template.TypeScenePath, NmsInventoryContainer.FreighterTypePath);
        // ApplyTemplate's own internal class write lands on the Technology
        // container (0wS, inert for Class - see FreighterClassPath), so the
        // real class write has to happen here too, same as Seed/TypeScenePath.
        if (alsoMatchClass && !string.IsNullOrEmpty(template.SourceClass))
            SaveSessionManager.StageEdit(template.SourceClass, NmsInventoryContainer.FreighterClassPath);

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
        // See ApplyFullBuildTemplateAsync - ApplyTemplate's own internal class
        // write lands on the wrong (inert) container for Freighter.
        if (alsoMatchClass && !string.IsNullOrEmpty(template.SourceClass))
            SaveSessionManager.StageEdit(template.SourceClass, NmsInventoryContainer.FreighterClassPath);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadGrids();
    }

    /// <summary>Confirms applying a template. A save only ever has one freighter, so
    /// there's no target picker - just "apply this to your freighter". The class
    /// compared is the real one (Cargo's, FreighterClassPath), not the grid's.</summary>
    private async Task<(bool Confirmed, bool AlsoMatchClass)> ShowApplyConfirmationAsync(
        string sourceLabel, string? sourceClass, IEnumerable<(int X, int Y)> sourcePositions, bool includesStatsAndSeed = false,
        bool replacesInterior = false)
    {
        if (_techViewModel is null) return (false, false);

        string summary = includesStatsAndSeed && replacesInterior
            ? $"This replaces your freighter's entire tech loadout, base stats, Type, appearance seed and interior (every room and furnishing) with {sourceLabel}. "
                + "Items in your storage containers aren't affected."
            : includesStatsAndSeed
            ? $"This replaces your freighter's entire tech loadout, base stats, Type, and appearance seed with {sourceLabel}."
            : $"This replaces your freighter's entire tech loadout with {sourceLabel}.";
        var choice = await TechStackCopyConfirmation.ShowAsync(XamlRoot, sourceLabel, sourceClass, sourcePositions,
            "your freighter", _techViewModel, GetFreighterClassLetter(), summary,
            includesStatsAndSeed ? "Confirm full build apply" : "Confirm tech stack apply", "Apply");
        return (choice.Confirmed, choice.AlsoMatchClass);
    }

    // ------------------------------------------------------------ Templates sidebar (LoadoutTemplatesPanel)

    string? ILoadoutTemplateHost.CurrentName => _techViewModel is null ? null : "Your freighter";

    bool ILoadoutTemplateHost.HasTech => _techViewModel?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) =>
        _techViewModel!.ExtractTemplate(name, NameEditBox.Text?.Trim() is { Length: > 0 } n ? n : "Freighter", "Freighter");

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) => BuildFullBuildTemplate(name);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => ApplyFullBuildTemplateAsync(template);

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.Revert();
        TechGrid.Refresh();
        _cargoViewModel?.Revert();
        CargoGrid.Refresh();
        TechListPanel.RefreshOwned();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    /// <summary>What the Details band says while folded: your freighter's name.</summary>
    private void ShowFoldedName() => DetailsCard.FoldedName = NameEditBox.Text?.Trim() ?? "";
}
