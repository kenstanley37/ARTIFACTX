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

public sealed partial class ShipsPage : Page
{
    // Matches the ScrollViewers' own XAML-declared MaxHeight - restored when a
    // section is collapsed again. Same values as ExosuitPage since ships share
    // the same 10x6 Technology / 10x12 Cargo best-guess capacity.
    private const double TechCollapsedHeight = 232;
    private const double CargoCollapsedHeight = 368;

    private static readonly string ChevronDownGlyph = char.ConvertFromUtf32(0xE70D);
    private static readonly string ChevronUpGlyph = char.ConvertFromUtf32(0xE70E);

    // Unlike Multi-Tool's Kgt (a full mirrored container), the currently
    // EQUIPPED ship is tracked by a plain integer index - NmsInventoryContainer.
    // ActiveShipIndexPath (aBE) - confirmed against 15 real save files across
    // 3 characters.
    private sealed record ShipEntry(int Index, string Name, bool IsGameActive);

    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;
    private int _selectedIndex = -1;
    private List<ShipEntry> _ships = new();
    private bool _suppressStatChangeEvent;

    // Set around SetStatValue's own StageEdit call so OnSessionOrEditsChanged
    // can skip the reload it would otherwise trigger for that same edit - see
    // OnSessionOrEditsChanged's doc comment for why this matters specifically
    // on this page.
    private bool _suppressReloadOnOwnStatEdit;

    // Ship-type/class -> max-slot lookups (GameId -> CapacityValue), loaded once
    // per page lifetime from the catalog - see StarshipCapacity for why these
    // numbers live in the database instead of being hardcoded here.
    private Dictionary<string, int>? _shipCapacity;

    public ShipsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadShipList();
        _ = RefreshTemplatesListAsync();
        _ = RefreshFullBuildTemplatesListAsync();
    }

    /// <summary>Skips the reload for edits SetStatValue just staged itself -
    /// see its own doc comment for why. Everything else that can trigger this
    /// event (switching save file, another page, TechGrid/CargoGrid cell
    /// edits, Class/Name/Seed changes) still gets the normal full reload.</summary>
    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (_suppressReloadOnOwnStatEdit) return;
        DispatcherQueue.TryEnqueue(LoadShipList);
    }

    /// <summary>Fix for a real reported bug (2026-08-01): "changing from
    /// Damage to Shield to Hyperdrive etc takes a couple of seconds each
    /// time... like a memory leak." It was one - Frame.Navigate creates a
    /// brand-new Page instance on every visit to any of this app's pages
    /// (no NavigationCacheMode set anywhere), and EVERY page subscribes to
    /// these two static SaveSessionManager events in its constructor with
    /// no matching unsubscribe anywhere in the app. Since a static event
    /// holds a strong reference to every subscriber, none of those old Page
    /// instances could ever be garbage collected - each past visit to
    /// EVERY page (General/Exosuit/MultiTool/Ships/Freighter/Frigate/
    /// BaseStorage/CorvetteCache/Companions/Settlement) left a permanently
    /// subscribed dead instance behind. Editing a single NumberBox on THIS
    /// page fires SetStatValue -> StageEdit -> PendingEditsChanged, which
    /// then re-ran OnSessionOrEditsChanged's full LoadShipList (rebuilding
    /// the Technology/Cargo grids, incl. catalog icon lookups) on every one
    /// of those accumulated dead instances too, all synchronously on the UI
    /// thread - explaining why it got slower the more the app had been
    /// navigated around, exactly matching the user's own "memory leak"
    /// hypothesis. Fixed the same way on all 10 pages that had this pattern
    /// (see Page_Unloaded on each) by unsubscribing on Unloaded, which
    /// fires reliably when Frame.Navigate swaps a page out.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    /// <summary>Reads a ship's model scene path (to detect its real ship type -
    /// Hauler/Fighter/Explorer/etc.) and its current Class letter, BEFORE any
    /// InventoryGridViewModel exists for it - both are needed up front to size
    /// the grids correctly, since capacity genuinely differs by type+class
    /// (a Hauler has more Cargo than a Fighter - confirmed in StarshipCapacity).</summary>
    private static (StarshipCapacity.ShipType? Type, string? ClassLetter) GetShipTypeAndClass(int shipIndex)
    {
        string? scenePath = SaveSessionManager.GetValue(
            "vLc", "6f=", "@Cs", shipIndex.ToString(), "NTx", "93M")?.Value<string>();
        var type = StarshipCapacity.DetectShipType(scenePath);

        var classPath = NmsInventoryContainer.ShipTechnologyPath(shipIndex).Append("B@N").Append("1o6").ToArray();
        string? classLetter = SaveSessionManager.GetValue(classPath)?.Value<string>();

        return (type, classLetter);
    }

    private async Task<Dictionary<string, int>> GetShipCapacityAsync()
    {
        _shipCapacity ??= await CatalogService.GetShipCapacityAsync();
        return _shipCapacity;
    }

    private static int LookupCargoSlots(Dictionary<string, int> capacity, StarshipCapacity.ShipType? type, string? classLetter) =>
        type.HasValue && capacity.TryGetValue(StarshipCapacity.CargoCapacityKey(type.Value, classLetter), out int slots)
            ? slots : StarshipCapacity.FallbackCargoSlots;

    private static int LookupTechSlots(Dictionary<string, int> capacity, StarshipCapacity.ShipType? type, string? classLetter) =>
        type.HasValue && capacity.TryGetValue(StarshipCapacity.TechCapacityKey(type.Value, classLetter), out int slots)
            ? slots : StarshipCapacity.FallbackTechSlots;

    /// <summary>Re-reads the owned ship list from the current session and
    /// rebuilds the selector strip. A plain edits-changed refresh keeps
    /// whatever the user already has selected, mirroring MultiToolPage.</summary>
    private void LoadShipList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _ships = new();
            _selectedIndex = -1;
            ShipSelectorPanel.Children.Clear();
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            CargoGrid.ViewModel = null;
            CargoGrid.Refresh();
            return;
        }

        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is not JArray array)
            return;

        int activeIndex = SaveSessionManager.GetValue(NmsInventoryContainer.ActiveShipIndexPath)?.Value<int>() ?? -1;

        var ships = new List<ShipEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            // Occupancy is the model scene path (NTx/93M), NOT NKm (Name) - a
            // brand-new save's starter ship is genuinely owned (real scene
            // path) but hasn't had a Name written yet, so NKm=="" there too;
            // filtering on NKm silently dropped every ship on a fresh save,
            // this one included (real bug found 2026-08-10 by testing
            // against a save that hadn't left the starting planet).
            string scenePath = array[i]?["NTx"]?["93M"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(scenePath)) continue;

            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name)) name = $"Starship {i + 1}";
            ships.Add(new ShipEntry(i, name, i == activeIndex));
        }

        _ships = ships;

        // Defaults selection to the in-game active ship the first time a
        // session loads; a plain edits-changed refresh keeps whatever the
        // user already has selected, mirroring MultiToolPage.
        if (_selectedIndex < 0 || _ships.All(s => s.Index != _selectedIndex))
        {
            var defaultShip = _ships.FirstOrDefault(s => s.IsGameActive) ?? _ships.FirstOrDefault();
            _selectedIndex = defaultShip?.Index ?? -1;
        }

        BuildSelectorStrip();
        LoadSelectedShip();
    }

    private void BuildSelectorStrip()
    {
        ShipSelectorPanel.Children.Clear();

        foreach (var ship in _ships)
        {
            bool isSelected = ship.Index == _selectedIndex;

            var button = new Button
            {
                Content = BuildButtonContent(ship, isSelected),
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 255, 157, 0)
                    : Color.FromArgb(255, 90, 98, 112)),
                Background = new SolidColorBrush(isSelected
                    ? Color.FromArgb(60, 255, 157, 0)
                    : Color.FromArgb(20, 255, 255, 255))
            };

            button.Click += (_, _) =>
            {
                _selectedIndex = ship.Index;
                BuildSelectorStrip();
                LoadSelectedShip();
            };

            ShipSelectorPanel.Children.Add(button);
        }
    }

    /// <summary>Selected state bolds the name; game-active state adds a small
    /// green "ACTIVE" badge - the two are independent and can appear together
    /// or separately (e.g. viewing a ship that isn't the one you're flying).</summary>
    private static StackPanel BuildButtonContent(ShipEntry ship, bool isSelected)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        panel.Children.Add(new TextBlock
        {
            Text = ship.Name,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (ship.IsGameActive)
        {
            panel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 200, 90)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "ACTIVE", FontSize = 10, FontWeight = FontWeights.Bold }
            });
        }

        return panel;
    }

    private async void LoadSelectedShip()
    {
        if (_selectedIndex < 0)
        {
            _techViewModel = null;
            _cargoViewModel = null;
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            CargoGrid.ViewModel = null;
            CargoGrid.Refresh();
            TechHeaderTxt.Text = "Technology";
            ClassSelectorPanel.Children.Clear();
            NameEditBox.Text = "";
            SeedEditBox.Text = "";
            DamageStatBox.Value = double.NaN;
            ShieldStatBox.Value = double.NaN;
            HyperdriveStatBox.Value = double.NaN;
            AgileStatBox.Value = double.NaN;
            return;
        }

        var selectedShip = _ships.First(s => s.Index == _selectedIndex);

        var (shipType, classLetter) = GetShipTypeAndClass(_selectedIndex);
        var capacity = await GetShipCapacityAsync();
        int techRows = StarshipCapacity.SlotsToRows(LookupTechSlots(capacity, shipType, classLetter));
        int cargoRows = StarshipCapacity.SlotsToRows(LookupCargoSlots(capacity, shipType, classLetter));

        _techViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ShipTechnologyPath(_selectedIndex),
            StarshipCapacity.Columns,
            techRows);
        _cargoViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ShipCargoPath(_selectedIndex),
            StarshipCapacity.Columns,
            cargoRows);

        _techViewModel.Load();
        _cargoViewModel.Load();

        var itemIds = _techViewModel.Cells.Concat(_cargoViewModel.Cells)
            .Where(c => c.IsOccupied)
            .Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.ViewModel = _techViewModel;
        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        TechGrid.AllowedUsageCategories = new[] { "Ship", "All" };

        CargoGrid.ViewModel = _cargoViewModel;
        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable" };
        CargoGrid.SupportsSupercharge = false;
        CargoGrid.SupportsRepair = false;
        CargoGrid.ProductStorageMultiplier = 10;

        // Reload can run many times per page lifetime (every selector click) -
        // unsubscribe first so CellChanged doesn't fire the reset-button handler
        // multiple times per single actual edit.
        TechGrid.CellChanged -= GridCellChanged;
        TechGrid.CellChanged += GridCellChanged;
        CargoGrid.CellChanged -= GridCellChanged;
        CargoGrid.CellChanged += GridCellChanged;

        TechGrid.Refresh();
        CargoGrid.Refresh();

        TechHeaderTxt.Text = $"Technology - {selectedShip.Name}";
        PageResetBtn.Visibility = (_techViewModel.HasLocalChanges || _cargoViewModel.HasLocalChanges)
            ? Visibility.Visible : Visibility.Collapsed;

        BuildClassSelector();
        NameEditBox.Text = selectedShip.Name;
        SeedEditBox.Text = SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(_selectedIndex))?.Value<string>() ?? "";
        UpdateStatDisplay();
    }

    /// <summary>Stages the seed when the field loses focus - same plain-text
    /// staging pattern as Freighter's ModelSeedEditBox/CrewSeedEditBox.</summary>
    private void SeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = SeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        var seedPath = NmsInventoryContainer.ShipModelSeedPath(_selectedIndex);
        string? currentSeed = SaveSessionManager.GetValue(seedPath)?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, seedPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Rerolls the selected ship to a brand new random seed - same
    /// idea as a reference tool's own reroll, not a targeted "give me X wings" picker.
    /// See NmsSeedGenerator for why (shared with FreighterPage's Model
    /// Seed/Crew Seed Generate buttons, so both pages roll the same way).</summary>
    private void GenerateSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();

        SeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsInventoryContainer.ShipModelSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GridCellChanged(object? sender, EventArgs e) =>
        PageResetBtn.Visibility = Visibility.Visible;

    /// <summary>Stages the rename when the field loses focus - not on every
    /// keystroke, since that would rebuild the selector strip mid-typing.</summary>
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        var currentShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (currentShip != null && newName == currentShip.Name) return;

        var namePath = new[] { "vLc", "6f=", "@Cs", _selectedIndex.ToString(), "NKm" };
        SaveSessionManager.StageEdit(newName, namePath);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadShipList();
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
                // A class change can change this ship's real Tech/Cargo slot
                // totals (StarshipCapacity), so both grids get fully rebuilt via
                // LoadSelectedShip rather than just refreshed in place.
                _techViewModel?.SetClass(letter);
                PageResetBtn.Visibility = Visibility.Visible;
                LoadSelectedShip();
            };

            ClassSelectorPanel.Children.Add(button);
        }
    }

    /// <summary>Loads the selected ship's PMT.@bB raw continuous stat rolls
    /// (SHIP_DAMAGE/SHIP_SHIELD/SHIP_HYPERDRIVE/SHIP_AGILE) into the editable
    /// NumberBoxes - same source/shape as Freighter's Hyperdrive/Fleet
    /// Coordination and Multi-Tool's Damage/Mining/Scanner. Confirmed
    /// independent of the Class letter (SetClass only ever writes B@N.1o6),
    /// which is exactly why these need their own editable fields - changing
    /// Class alone doesn't touch them.</summary>
    private void UpdateStatDisplay()
    {
        _suppressStatChangeEvent = true;

        var bonuses = _selectedIndex >= 0
            ? SaveSessionManager.GetValue(NmsInventoryContainer.ShipStatBonusesPath(_selectedIndex)) as JArray
            : null;

        double? Find(string key) =>
            bonuses?.FirstOrDefault(b => b["QL1"]?.Value<string>() == key)?[">MX"]?.Value<double>();

        DamageStatBox.Value = Find("^SHIP_DAMAGE") ?? double.NaN;
        ShieldStatBox.Value = Find("^SHIP_SHIELD") ?? double.NaN;
        HyperdriveStatBox.Value = Find("^SHIP_HYPERDRIVE") ?? double.NaN;
        AgileStatBox.Value = Find("^SHIP_AGILE") ?? double.NaN;

        _suppressStatChangeEvent = false;
    }

    /// <summary>Stages a new value for one @bB entry, matched by its QL1 key,
    /// rebuilding and staging the WHOLE @bB array at once - same reasoning as
    /// FreighterPage/MultiToolPage.SetStatValue (a deeper leaf-only stage
    /// isn't seen by SaveSessionManager's staged-edit lookup, which only
    /// matches at the exact path queried). Entries this page doesn't know
    /// about (e.g. a Living Ship's "^ALIEN_SHIP") pass through untouched.
    ///
    /// The StageEdit call is wrapped with _suppressReloadOnOwnStatEdit
    /// (2026-08-01 fix, real reported bug: switching between Damage/Shield/
    /// Hyperdrive/Maneuverability had a noticeable sub-second lag on this
    /// page specifically). StageEdit fires SaveSessionManager's static
    /// PendingEditsChanged event, which this page's own OnSessionOrEditsChanged
    /// reacts to by reloading the WHOLE selected ship - rebuilding both the
    /// Technology and Cargo InventoryGridViewModels (up to 10x6 + 10x12 =
    /// 180 cells) and re-warming the catalog icon cache for every occupied
    /// slot, none of which changed. That reload is genuinely needed for
    /// edits that came from elsewhere (switching save file, another page,
    /// TechGrid/CargoGrid cell edits, Class/Name/Seed changes all still go
    /// through un-suppressed), but not for a stat box re-confirming a value
    /// it already displays correctly - the NumberBox is already showing what
    /// was just typed. Other pages' grids are small enough this same
    /// redundant-reload pattern isn't noticeable; Ships' is the heaviest in
    /// the app, which is why the lag showed up here specifically.</summary>
    private void SetStatValue(string statKey, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;

        var bonusesPath = NmsInventoryContainer.ShipStatBonusesPath(_selectedIndex);
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

    private void DamageStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^SHIP_DAMAGE", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Redundant safety net alongside ValueChanged - some WinUI3
    /// NumberBox versions don't reliably fire ValueChanged on a plain focus
    /// loss, matching Freighter/MultiToolPage's own *StatBox_LostFocus.</summary>
    private void DamageStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(DamageStatBox.Value))
        {
            SetStatValue("^SHIP_DAMAGE", DamageStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    private void ShieldStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^SHIP_SHIELD", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ShieldStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(ShieldStatBox.Value))
        {
            SetStatValue("^SHIP_SHIELD", ShieldStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    private void HyperdriveStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^SHIP_HYPERDRIVE", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void HyperdriveStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(HyperdriveStatBox.Value))
        {
            SetStatValue("^SHIP_HYPERDRIVE", HyperdriveStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    private void AgileStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^SHIP_AGILE", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void AgileStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(AgileStatBox.Value))
        {
            SetStatValue("^SHIP_AGILE", AgileStatBox.Value);
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

    /// <summary>Toggles between the compact, game-matching preview height and
    /// showing every row at once - same pattern as ExosuitPage.</summary>
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

    /// <summary>Copies the currently selected ship's entire tech loadout onto
    /// another owned ship - same analysis/confirmation flow as MultiToolPage's
    /// Copy Tech Stack. Cargo is deliberately not included, matching the
    /// approved core scope.</summary>
    private async void CopyTechStackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var sourceShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (sourceShip is null) return;

        var otherShips = _ships.Where(s => s.Index != _selectedIndex).ToList();
        if (otherShips.Count == 0)
        {
            await new ContentDialog
            {
                Title = "No other ships",
                Content = "You only own one starship, so there's nothing to copy this stack onto.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var pickerList = new ListView { SelectionMode = ListViewSelectionMode.Single };
        foreach (var s in otherShips)
            pickerList.Items.Add(s.Name);

        var pickerDialog = new ContentDialog
        {
            Title = $"Copy {sourceShip.Name}'s tech stack to...",
            Content = pickerList,
            PrimaryButtonText = "Next",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        pickerList.SelectionChanged += (_, _) => pickerDialog.IsPrimaryButtonEnabled = pickerList.SelectedIndex >= 0;

        if (await pickerDialog.ShowAsync() != ContentDialogResult.Primary || pickerList.SelectedIndex < 0)
            return;

        var targetShip = otherShips[pickerList.SelectedIndex];

        var (targetType, targetClass) = GetShipTypeAndClass(targetShip.Index);
        var targetCapacity = await GetShipCapacityAsync();
        var targetViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ShipTechnologyPath(targetShip.Index),
            StarshipCapacity.Columns,
            StarshipCapacity.SlotsToRows(LookupTechSlots(targetCapacity, targetType, targetClass)));
        targetViewModel.Load();

        var sourcePositions = _techViewModel.Cells.Where(c => c.IsOccupied).Select(c => (c.X, c.Y));
        var (confirmed, alsoMatchClass) = await ShowCopyConfirmationAsync(
            $"a copy of {sourceShip.Name}'s", _techViewModel.CurrentClass, sourcePositions, targetShip.Name, targetViewModel);

        if (!confirmed) return;

        targetViewModel.CopyTechStackFrom(_techViewModel, alsoMatchClass);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadShipList();
    }

    private async void SaveAsTemplateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var sourceShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (sourceShip is null) return;

        if (!_techViewModel.Cells.Any(c => c.IsOccupied))
        {
            await new ContentDialog
            {
                Title = "Nothing to save",
                Content = $"{sourceShip.Name} has no tech installed, so there's nothing to capture as a template.",
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

        var template = _techViewModel.ExtractTemplate(name, sourceShip.Name, "Ship");
        await LoadoutTemplateService.SaveAsync(template);

        // No confirmation popup here on purpose - the whole point of moving
        // templates into a persistent list is that saving one should visibly
        // show up right away, not need a separate dialog to prove it worked.
        await RefreshTemplatesListAsync();
    }

    /// <summary>Rebuilds the persistent Tech Stack Templates list, filtered to
    /// Ship-sourced, Scope=="TechStack" templates only - Multi-Tool templates
    /// share the same on-disk pool but contain Weapon-usage tech that doesn't
    /// belong in a ship's grid, and Full Build templates have their own
    /// separate list below (RefreshFullBuildTemplatesListAsync) so it's
    /// obvious which ones will touch Stats/Seed and which won't. Called after
    /// saving, deleting, or applying a template, and once on page load.</summary>
    private async Task RefreshTemplatesListAsync()
    {
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "Ship" && t.Scope == "TechStack")
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
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var sourceShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (sourceShip is null) return;

        if (!_techViewModel.Cells.Any(c => c.IsOccupied))
        {
            await new ContentDialog
            {
                Title = "Nothing to save",
                Content = $"{sourceShip.Name} has no tech installed, so there's nothing to capture as a template.",
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

        var template = _techViewModel.ExtractTemplate(name, sourceShip.Name, "Ship");
        template.Scope = "FullBuild";
        template.Stats = ExtractStatsList(_selectedIndex);
        template.Seed = SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(_selectedIndex))?.Value<string>();

        await LoadoutTemplateService.SaveAsync(template);
        await RefreshFullBuildTemplatesListAsync();
    }

    /// <summary>Same shape as RefreshTemplatesListAsync, filtered to Scope==
    /// "FullBuild" instead and targeting the separate FullBuildTemplatesListPanel.</summary>
    private async Task RefreshFullBuildTemplatesListAsync()
    {
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "Ship" && t.Scope == "FullBuild")
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

    /// <summary>Reads a ship's whole stat-bonus array (@bB) as a plain key/value
    /// list, for embedding in a Full Build template - captures every entry
    /// generically (not just the 4 keys exposed as NumberBoxes) so nothing
    /// present but not individually surfaced (e.g. a Living Ship's
    /// "^ALIEN_SHIP") gets silently dropped on save.</summary>
    private static List<NmsLoadoutStat> ExtractStatsList(int shipIndex)
    {
        var bonuses = SaveSessionManager.GetValue(NmsInventoryContainer.ShipStatBonusesPath(shipIndex)) as JArray;
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

    /// <summary>Stages a Full Build template's saved stats onto the given ship -
    /// rebuilds the whole @bB array matched by key, same "match by key, replace
    /// whole array" pattern as SetStatValue. A key present on the target but not
    /// in the template (or vice versa) is left untouched/dropped respectively -
    /// this only ever adjusts values for keys both sides already agree exist.</summary>
    private static void ApplyStatsList(int targetShipIndex, List<NmsLoadoutStat> stats)
    {
        if (stats.Count == 0) return;

        var bonusesPath = NmsInventoryContainer.ShipStatBonusesPath(targetShipIndex);
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

    /// <summary>Applies a saved Full Build template's tech, stats, and Seed to
    /// whichever ship is currently selected - Class only changes if the user
    /// opts in, same as ApplyTemplateAsync.</summary>
    private async Task ApplyFullBuildTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var targetShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (targetShip is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await ShowCopyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, targetShip.Name, _techViewModel,
            includesStatsAndSeed: true);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        ApplyStatsList(_selectedIndex, template.Stats);
        if (!string.IsNullOrEmpty(template.Seed))
            SaveSessionManager.StageEdit(template.Seed, NmsInventoryContainer.ShipModelSeedPath(_selectedIndex));

        PageResetBtn.Visibility = Visibility.Visible;
        LoadSelectedShip();
    }

    /// <summary>Applies a saved template to whichever ship is currently selected
    /// in the main strip - same flow as MultiToolPage's ApplyTemplateAsync. A
    /// class change (alsoMatchClass) can change this ship's real Tech/Cargo
    /// slot totals (StarshipCapacity), so the grids get fully rebuilt via
    /// LoadSelectedShip rather than just refreshed in place.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var targetShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (targetShip is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await ShowCopyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, targetShip.Name, _techViewModel);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadSelectedShip();
    }

    /// <summary>Shared confirmation dialog - identical shape to MultiToolPage's,
    /// duplicated rather than factored out since it's a small, page-local UI
    /// helper and the two pages have no other shared base to hang it on.</summary>
    private async Task<(bool Confirmed, bool AlsoMatchClass)> ShowCopyConfirmationAsync(
        string sourceLabel, string? sourceClass, IEnumerable<(int X, int Y)> sourcePositions,
        string targetLabel, InventoryGridViewModel targetViewModel, bool includesStatsAndSeed = false)
    {
        var sourcePositionSet = sourcePositions.ToHashSet();
        var targetUnlocked = targetViewModel.Cells.Where(c => c.State != InventorySlotState.Locked).Select(c => (c.X, c.Y)).ToHashSet();
        int newSlotsNeeded = sourcePositionSet.Except(targetUnlocked).Count();

        bool classDiffers = !string.Equals(sourceClass, targetViewModel.CurrentClass, StringComparison.OrdinalIgnoreCase);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = includesStatsAndSeed
                ? $"This replaces {targetLabel}'s entire tech loadout, base stats, and appearance seed with {sourceLabel}."
                : $"This replaces {targetLabel}'s entire tech loadout with {sourceLabel}."
        });

        if (newSlotsNeeded > 0)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 160, 60)),
                Text = $"⚠ {newSlotsNeeded} slot(s) will also be unlocked on {targetLabel} to fit this stack."
            });
        }

        CheckBox? matchClassBox = null;
        if (classDiffers)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 160, 60)),
                Text = $"⚠ Class differs: {targetLabel} is currently {targetViewModel.CurrentClass ?? "unknown"}, source is {sourceClass ?? "unknown"}."
            });

            matchClassBox = new CheckBox { Content = $"Also change {targetLabel}'s class to {sourceClass} to match" };
            panel.Children.Add(matchClassBox);
        }

        var dialog = new ContentDialog
        {
            Title = includesStatsAndSeed ? "Confirm full build copy" : "Confirm tech stack copy",
            Content = panel,
            PrimaryButtonText = "Copy",
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
