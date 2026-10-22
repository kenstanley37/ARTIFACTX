using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Almanac.Starship;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

public sealed partial class ShipsPage : Page, ILoadoutTemplateHost
{
    // Matches the ScrollViewers' own XAML-declared MaxHeight - restored when a
    // section is collapsed again. Same values as ExosuitPage since ships share
    // the same 10x6 Technology / 10x12 Cargo best-guess capacity.


    // Unlike Multi-Tool's Kgt (a full mirrored container), the currently
    // EQUIPPED ship is tracked by a plain integer index - NmsInventoryContainer.
    // ActiveShipIndexPath (aBE) - confirmed against 15 real save files across
    // 3 characters.
    // Name is the button label: the player's name, else the one the game makes
    // up for an unnamed ship (GameName, MultiToolKinds.ShipNameAsync), else "Starship N".
    private sealed record ShipEntry(int Index, string Name, bool IsGameActive, bool IsCorvette, int BuildParts,
        bool IsUnnamed = false, string? GameName = null);

    // The game's names for unnamed ships, by (seed, model) - worked out once, off the list build.
    // Emptied when the game data changes: names worked out before an update (none,
    // without the word lists) stayed cached as none afterwards (2026-10-16).
    private static readonly Dictionary<(ulong, string), string?> GameNames = new();
    private static DateTime? _gameNamesStamp;
    private bool _namingShips;

    private InventoryGridViewModel? _techViewModel;
    private InventoryGridViewModel? _cargoViewModel;
    private int _selectedIndex = -1;
    private List<ShipEntry> _ships = new();

    // Set around CommitName's own StageEdit while typing, so OnSessionOrEditsChanged
    // skips the full reload for that edit (stat edits: StatsBoxes.IsStaging).
    private bool _suppressReloadOnOwnNameEdit;

    // Ship-type/class -> max-slot lookups (GameId -> CapacityValue), loaded once
    // per page lifetime from the catalog - see StarshipCapacity for why these
    // numbers live in the database instead of being hardcoded here.
    private Dictionary<string, int>? _shipCapacity;

    // Type option list (which physical model this ship is - Fighter/Explorer/
    // Golden Vector/etc.) - a page-local mutable COPY of the static
    // StarshipTypes.All, never the shared list itself, since a synthetic
    // "(Current - not in known list)" entry may need inserting/removing per
    // selected ship (same pattern as SquadronPage's own Ship Type dropdown).
    private List<StarshipTypeInfo>? _shipTypeOptions;
    private bool _suppressTypeSelectionEvent;

    public ShipsPage()
    {
        InitializeComponent();
        TechCard.GroupOf = TechListPanel.TypeOf;
        PartsPanel.ViewModel.SeedChosen += PartsPanel_SeedChosen;

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        LiveItemsService.Changed += OnLiveItemsChanged;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        Unloaded += Page_Unloaded;

        ShowFoldedName();
        LoadShipList();
        TemplatesPanel.Host = this;
    }

    /// <summary>Skips the reload for this page's own stat edits (StatsBoxes) and
    /// name typing - rebuilding both grids on each made switching boxes lag
    /// (2026-08-01). Everything else that can trigger this event (switching save
    /// file, another page, TechGrid/CargoGrid cell edits, Class/Seed changes)
    /// still gets the normal full reload.</summary>
    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (_suppressReloadOnOwnNameEdit || StatsBoxes.IsStaging) return;
        DispatcherQueue.TryEnqueue(LoadShipList);
    }

    /// <summary>The game added or dropped a ship while it played: only the strip
    /// changes, the ship on screen stays (LiveItemsService).</summary>
    private void OnLiveItemsChanged(LiveCollection list)
    {
        if (list == LiveCollection.Ships) DispatcherQueue.TryEnqueue(() => LoadShipList(live: true));
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
    /// page stages a stat edit -> PendingEditsChanged, which
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
        LiveItemsService.Changed -= OnLiveItemsChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    /// <summary>What follows the game for the ship on screen (LiveFieldsService), with
    /// the field that shows it. The class is kept on both inventories, so both class
    /// fields come before the inventories - a class change flashes the class, not the
    /// grids. The ship's customisation entry holds its paint and parts.</summary>
    private (string[] Path, Func<UIElement> Field)[] LiveFields(int ship)
    {
        string i = ship.ToString(CultureInfo.InvariantCulture);
        return new (string[], Func<UIElement>)[]
        {
            (new[] { "vLc", "6f=", "@Cs", i, "NKm" }, () => NameEditBox),
            (new[] { "vLc", "6f=", "@Cs", i, "NTx", "93M" }, () => TypeComboBox),
            (NmsInventoryContainer.ShipModelSeedPath(ship), () => SeedBox),
            (NmsInventoryContainer.ShipTechnologyPath(ship).Append("B@N").Append("1o6").ToArray(), () => ClassPicker),
            (NmsInventoryContainer.ShipCargoPath(ship).Append("B@N").Append("1o6").ToArray(), () => ClassPicker),
            (NmsInventoryContainer.ShipStatBonusesPath(ship), () => StatsBoxes),
            (NmsInventoryContainer.ShipTechnologyPath(ship), () => TechGrid),
            (NmsInventoryContainer.ShipCargoPath(ship), () => CargoGrid),
            (new[] { "vLc", "6f=", "l:j", NewFromTemplateService.ShipCustomisationIndex(ship).ToString(CultureInfo.InvariantCulture) }, () => PartsPanel),
        };
    }

    /// <summary>The game saved: the ship on screen takes its values where nothing is
    /// edited here, and each changed field pulses once - no reload (2026-10-22).</summary>
    private void OnGameSaved(JObject disk)
    {
        if (_selectedIndex < 0) return;
        var fields = LiveFields(_selectedIndex);
        var changed = LiveFieldsService.Adopt(disk, fields.Select(f => f.Path),
            fields.Where(f => f.Field() == TechGrid || f.Field() == CargoGrid).Select(f => f.Path).ToList());
        if (changed.Count == 0) return;
        LoadShipList();
        foreach (var field in fields.Where(f => changed.Contains(f.Path)).Select(f => f.Field()).Distinct())
            FieldHighlight.Flash(field);
    }

    private async Task<Dictionary<string, int>> GetShipCapacityAsync()
    {
        _shipCapacity ??= await CatalogService.GetShipCapacityAsync();
        return _shipCapacity;
    }

    /// <summary>Re-reads the owned ship list from the current session and
    /// rebuilds the selector strip. A plain edits-changed refresh keeps
    /// whatever the user already has selected, mirroring MultiToolPage. A live
    /// update (the game added or dropped a ship) rebuilds only the strip,
    /// unless the ship on screen is the one that changed.</summary>
    private void LoadShipList() => LoadShipList(live: false);

    private void LoadShipList(bool live)
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _ships = new();
            _selectedIndex = -1;
            ShipStrip.Items = null;
            ShipSlotsTxt.Text = "";
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            CargoGrid.ViewModel = null;
            CargoGrid.Refresh();
            return;
        }

        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is not JArray array)
            return;

        int activeIndex = SaveSessionManager.GetValue(NmsInventoryContainer.ActiveShipIndexPath)?.Value<int>() ?? -1;
        var builds = ShipBaseService.BuildsBySlot();

        var ships = new List<ShipEntry>();
        var missingNames = new List<(ulong, string)>();
        if (GameDataService.Info?.BuiltAtUtc != _gameNamesStamp) { GameNames.Clear(); _gameNamesStamp = GameDataService.Info?.BuiltAtUtc; }
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
            bool unnamed = string.IsNullOrEmpty(name);
            string? gameName = null;
            if (unnamed)
            {
                ShipPartsService.TryParseSeed(array[i]?["NTx"]?["@EL"]?[1]?.Value<string>(), out ulong seed);
                if (!GameNames.TryGetValue((seed, scenePath), out gameName)) missingNames.Add((seed, scenePath));
                name = gameName ?? $"Starship {i + 1}";
            }
            int parts = builds.GetValueOrDefault(i);
            ships.Add(new ShipEntry(i, name, i == activeIndex, parts > 0 || ShipBaseService.IsCorvetteModel(scenePath), parts, unnamed, gameName));
        }

        _ships = ships;
        if (missingNames.Count > 0) NameShips(missingNames);

        // Defaults selection to the in-game active ship the first time a
        // session loads; a plain edits-changed refresh keeps whatever the
        // user already has selected, mirroring MultiToolPage.
        if (_selectedIndex < 0 || _ships.All(s => s.Index != _selectedIndex))
        {
            var defaultShip = _ships.FirstOrDefault(s => s.IsGameActive) ?? _ships.FirstOrDefault();
            _selectedIndex = defaultShip?.Index ?? -1;
        }

        BuildSelectorStrip();
        if (!live || _shownIndex != _selectedIndex || LiveItemsService.IsNew(LiveCollection.Ships, _selectedIndex))
            LoadSelectedShip();
    }

    /// <summary>Works out the game's names for unnamed ships in the background, then
    /// relabels the strip (and the name box, when it shows one of them).</summary>
    private async void NameShips(List<(ulong Seed, string Scene)> missing)
    {
        if (_namingShips) return;
        _namingShips = true;
        try
        {
            foreach (var (seed, scene) in missing)
                GameNames[(seed, scene)] = await MultiToolKinds.ShipNameAsync(seed, scene);
        }
        finally { _namingShips = false; }
        if (missing.All(m => GameNames[m] is null)) return;
        string? typed = NameEditBox.Text;
        var before = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        LoadShipList(live: true);
        var after = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        // Only fill the box if the player hasn't typed into it meanwhile.
        if (after is { IsUnnamed: true } && before is { IsUnnamed: true } && string.IsNullOrEmpty(typed)) ShowName(after);
        if (after is not null) TechCard.Title = $"TECHNOLOGY  —  {after.Name.ToUpperInvariant()}";
    }

    /// <summary>The name box: the player's name, or the one the game made up for an
    /// unnamed ship. Leaving it unchanged writes nothing, so the game's name never
    /// becomes a saved custom name by accident (as on Multi-Tool).</summary>
    private void ShowName(ShipEntry ship)
    {
        // The list may have been relabelled since the caller looked the ship up: the
        // game's names arrive in the background, and a slow ship load used to put the
        // old empty name back over them (2026-10-16).
        ship = _ships.FirstOrDefault(s => s.Index == ship.Index) ?? ship;
        NameEditBox.Text = ship.IsUnnamed ? ship.GameName ?? "" : ship.Name;
        bool oldData = GameDataService.Info is { } info && info.Format < ArtifactX.GameData.GameDataBuilder.CatalogFormat;
        // The way to fix it, right where it shows (the bar at the top is easy to miss).
        NameUpdateLink.Visibility = ship.IsUnnamed && ship.GameName is null && oldData && !GameDataService.IsBuilding
            ? Visibility.Visible : Visibility.Collapsed;
        NameEditBox.PlaceholderText = ship.IsUnnamed && ship.GameName is null
            ? (oldData ? "Update your game data to see the game's name" : "No name yet - type one to name this ship")
            : "";
    }

    private void BuildSelectorStrip()
    {
        bool IsNew(int index) => LiveItemsService.IsNew(LiveCollection.Ships, index);
        var chips = _ships
            .Select(ship => EquipmentChipViewModel.Item(ship.Index, ship.Name, ship.Index == _selectedIndex, ship.IsGameActive,
                isNew: IsNew(ship.Index),
                animateIn: IsNew(ship.Index) && LiveItemsService.TakeAnimation(LiveCollection.Ships, ship.Index),
                isCorvette: ship.IsCorvette))
            .Concat(LiveItemsService.Gone(LiveCollection.Ships).Select(EquipmentChipViewModel.Gone))
            .ToList();

        // Empty slots as faint placeholders, so how much room is left shows at a glance.
        int total = NewFromTemplateService.ShipSlotCount, free = NewFromTemplateService.FreeShipSlots;
        ShipSlotsTxt.Text = total > 0 ? $"{total - free} OF {total} SLOTS USED" : "";
        for (int i = 0; i < free; i++)
            chips.Add(EquipmentChipViewModel.EmptySlot("An empty starship slot. Design a ship for it in the Ship Designer, or use a Full Build template's Add as new."));
        ShipStrip.Items = chips;
    }

    private void ShipStrip_ChipClicked(object? sender, EquipmentChipViewModel chip)
    {
        if (chip.IsEmptySlot)
        {
            Frame.Navigate(typeof(ShipDesignerPage));
            return;
        }
        _selectedIndex = chip.Index;
        if (LiveItemsService.IsNew(LiveCollection.Ships, chip.Index))
        {
            LiveItemsService.Seen(LiveCollection.Ships, chip.Index); // Changed -> LoadShipList shows it
            return;
        }
        BuildSelectorStrip();
        LoadSelectedShip();
    }

    private async void LoadSelectedShip()
    {
        _shownIndex = _selectedIndex;
        await LoadSelectedShipAsync();
    }

    // The ship whose details are on screen - a live update leaves it there
    // unless it's the one that changed.
    private int _shownIndex = -1;

    private async Task LoadSelectedShipAsync()
    {
        SyncTypeComboBox();

        if (_selectedIndex < 0)
        {
            _techViewModel = null;
            _cargoViewModel = null;
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            CargoGrid.ViewModel = null;
            CargoGrid.Refresh();
            TechCard.Title = "TECHNOLOGY";
            TechLayout.ShowList = false;
            ClassPicker.Visibility = Visibility.Collapsed;
            NameEditBox.Text = "";
            SeedBox.Show(null);
            StatsBoxes.Clear();
            return;
        }

        var selectedShip = _ships.First(s => s.Index == _selectedIndex);

        var (shipType, classLetter) = StarshipService.TypeAndClass(_selectedIndex);
        var capacity = await GetShipCapacityAsync();
        int techRows = StarshipCapacity.SlotsToRows(StarshipService.TechSlots(capacity, shipType, classLetter));
        int cargoRows = StarshipCapacity.SlotsToRows(StarshipService.CargoSlots(capacity, shipType, classLetter));

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
        var techRule = StarshipService.TechRuleFor(SaveSessionManager.GetValue(
            "vLc", "6f=", "@Cs", _selectedIndex.ToString(), "NTx", "93M")?.Value<string>());
        TechGrid.AllowedUsageCategories = techRule.UsageCategories;

        CargoGrid.ViewModel = _cargoViewModel;
        CargoGrid.AllowedCategories = new[] { "Substance", "Product" };
        CargoGrid.AllowedTemplateTypes = new[] { "GcProductTable", "GcSubstanceTable", "ProceduralProducts" };
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

        TechCard.Title = $"TECHNOLOGY  —  {(_ships.FirstOrDefault(s => s.Index == selectedShip.Index) ?? selectedShip).Name.ToUpperInvariant()}";

        // The tech this kind of ship can fit, to click in - the same list
        // limits the right-click search.
        TechLayout.ShowList = true;
        TechLayout.ListTitle = techRule.Header;
        // Figurines and trails don't boost each other in game.
        TechListPanel.NoAdjacency = p => StarshipService.TechGroup(p) is "Figurines" or "Starship trails";
        TechListPanel.IsBestClass = () => StarshipService.ReadClass(_selectedIndex) == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            _techViewModel?.SetClass("S");
            StarshipService.SyncCargoClass(_selectedIndex);
            PageResetBtn.Visibility = Visibility.Visible;
            await LoadSelectedShipAsync();
        };
        TechListPanel.MoreRoomHint = () => StarshipService.ReadClass(_selectedIndex) == "S" ? null
            : "S class has more slots - change the class in Ship Details to fit more.";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            // Advanced Launch System is a special ship's built-in launcher: it
            // blocks take-off next to a normal Launch Thruster and the game
            // won't let you remove it (user, 2026-10-13); no real ship had it.
            var tech = (await CatalogService.GetTechForAsync(techRule.UsageCategories))
                .Where(t => !string.Equals(t.GameId, "LAUNCHER_SPEC", StringComparison.OrdinalIgnoreCase)).ToList();
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(StarshipService.ModuleFamiliesFor(techRule.Header));
            return StarshipService.SortTech(tech.Concat(modules));
        }, "technology", "technologies", "Technology", StarshipService.TechGroup);
        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(SelectedShipPath) ? Visibility.Visible : Visibility.Collapsed;

        ShowClass();
        ShowName(selectedShip);
        CorvetteNote.Visibility = selectedShip.IsCorvette ? Visibility.Visible : Visibility.Collapsed;
        CorvetteNoteTxt.Text = selectedShip.BuildParts > 0
            ? $"This is a corvette. What you built onto it - {selectedShip.BuildParts} parts: cockpit, habitat, wings, engines and the rest - is stored apart from the ship. Full Build templates include it."
            : "This is a corvette with no build of its own yet, so the game shows the default one.";
        SeedBox.Show(NmsInventoryContainer.ShipModelSeedPath(_selectedIndex));
        RefreshShipParts();
        UpdateStatDisplay();
    }

    private const string UnlistedShipTypeLabel = "(Current - not in known list)";

    /// <summary>Reads the selected ship's model scene path and syncs
    /// TypeComboBox to it - same idea as SquadronPage's own Ship Type
    /// dropdown (synthetic "current" entry for a ship outside the known
    /// list), but built as a fresh list every call rather than mutating a
    /// shared one: `_shipTypeOptions` is never touched after its first
    /// assignment, and the actual bound list (with an unlisted entry
    /// prepended when needed) is a new object each time, so ComboBox always
    /// sees a real reference change and re-renders reliably. Purely
    /// synchronous, unlike MultiTool/Freighter's catalog-backed Type combos -
    /// StarshipTypes.All is a small static in-code list, no DB round trip
    /// needed. Added 2026-08-12 - this page never had a Type control at all
    /// before, which was the real cause of a reported bug: applying a Full
    /// Build template captured from a reward ship (Golden Vector) could
    /// never actually restore its look, since nothing on this page could
    /// write the model scene path in the first place.</summary>
    private void SyncTypeComboBox()
    {
        _suppressTypeSelectionEvent = true;

        _shipTypeOptions ??= StarshipTypes.All.ToList();

        if (_selectedIndex < 0)
        {
            TypeComboBox.ItemsSource = _shipTypeOptions;
            TypeComboBox.SelectedItem = null;
            _suppressTypeSelectionEvent = false;
            return;
        }

        string? currentPath = SaveSessionManager.GetValue(
            "vLc", "6f=", "@Cs", _selectedIndex.ToString(), "NTx", "93M")?.Value<string>();

        var matched = _shipTypeOptions.FirstOrDefault(t =>
            string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));

        // Real saves commonly have ships outside the known list (see
        // StarshipTypes' own doc comment on how incomplete it still is) - a
        // synthetic, always-first placeholder keeps the current value
        // visible and selected without silently overwriting it the moment
        // the dropdown renders, same as SquadronPage's identical situation.
        List<StarshipTypeInfo> boundOptions = _shipTypeOptions;
        if (matched is null && !string.IsNullOrEmpty(currentPath))
        {
            matched = new StarshipTypeInfo(UnlistedShipTypeLabel, currentPath);
            boundOptions = new List<StarshipTypeInfo> { matched }.Concat(_shipTypeOptions).ToList();
        }

        TypeComboBox.ItemsSource = boundOptions;
        TypeComboBox.SelectedItem = matched;

        _suppressTypeSelectionEvent = false;
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTypeSelectionEvent || _selectedIndex < 0) return;
        if (TypeComboBox.SelectedItem is not StarshipTypeInfo type) return;

        SaveSessionManager.StageEdit(type.ScenePath, "vLc", "6f=", "@Cs", _selectedIndex.ToString(), "NTx", "93M");
        PageResetBtn.Visibility = Visibility.Visible;
        RefreshShipParts();
    }

    private void GridCellChanged(object? sender, EventArgs e) =>
        PageResetBtn.Visibility = Visibility.Visible;

    private void NameUpdateLink_Click(object sender, RoutedEventArgs e)
    {
        NameUpdateLink.Visibility = Visibility.Collapsed;
        App.MainWindowInstance?.StartGameDataUpdate();
    }

    // The rename is staged shortly after typing stops, so Save shows up without
    // leaving the box (2026-10-17: the player typed a name and found no Save), and
    // straight away on Enter or leaving the box.
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _nameTimer;

    /// <summary>Clicking in selects the whole name, ready to type over. A lambda, not
    /// the method group NameEditBox.SelectAll: handing the TextBox's own projected
    /// method to the queue as the callback closed the app on click (2026-10-17).</summary>
    private void NameEditBox_GotFocus(object sender, RoutedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => NameEditBox.SelectAll());

    private void NameEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Only the player's typing - ShowName filling the box isn't an edit.
        if (NameEditBox.FocusState == FocusState.Unfocused) return;
        _nameTimer ??= CreateNameTimer();
        _nameTimer.Stop();
        _nameTimer.Start();
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer CreateNameTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => CommitName(typing: true);
        return timer;
    }

    private void NameEditBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        _nameTimer?.Stop();
        CommitName(typing: true);
        e.Handled = true;
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _nameTimer?.Stop();
        CommitName(typing: false);
    }

    /// <summary>Stages the rename. While the box has focus the ship isn't reloaded -
    /// that would put the text back and move the cursor - only the strip and the
    /// technology header are relabelled.</summary>
    private void CommitName(bool typing)
    {
        if (_selectedIndex < 0) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        var currentShip = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (currentShip != null && newName == (currentShip.IsUnnamed ? currentShip.GameName : currentShip.Name)) return;

        var namePath = new[] { "vLc", "6f=", "@Cs", _selectedIndex.ToString(), "NKm" };
        _suppressReloadOnOwnNameEdit = typing;
        SaveSessionManager.StageEdit(newName, namePath);
        _suppressReloadOnOwnNameEdit = false;

        PageResetBtn.Visibility = Visibility.Visible;
        if (!typing) { LoadShipList(); return; }
        LoadShipList(live: true);
        TechCard.Title = $"TECHNOLOGY  —  {newName.ToUpperInvariant()}";
    }

    private void ShowClass()
    {
        ClassPicker.Visibility = _techViewModel is null ? Visibility.Collapsed : Visibility.Visible;
        ClassPicker.SelectedClass = StarshipService.ReadClass(_selectedIndex);
    }

    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        // A class change can change this ship's real Tech/Cargo slot
        // totals (StarshipCapacity), so both grids get fully rebuilt via
        // LoadSelectedShip rather than just refreshed in place.
        _techViewModel?.SetClass(letter);
        StarshipService.SyncCargoClass(_selectedIndex);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadSelectedShip();
    }

    /// <summary>Loads the selected ship's PMT.@bB raw continuous stat rolls
    /// (SHIP_DAMAGE/SHIP_SHIELD/SHIP_HYPERDRIVE/SHIP_AGILE) into the editable
    /// NumberBoxes - same source/shape as Freighter's Hyperdrive/Fleet
    /// Coordination and Multi-Tool's Damage/Mining/Scanner. Confirmed
    /// independent of the Class letter (SetClass only ever writes B@N.1o6),
    /// which is exactly why these need their own editable fields - changing
    /// Class alone doesn't touch them.</summary>
    /// <summary>The Base Stats boxes: the stat-bonus entries this page shows.</summary>
    private static readonly (string Label, string Key)[] BaseStats = StatBonusService.ShipStats;

    private void UpdateStatDisplay()
    {
        if (_selectedIndex < 0) StatsBoxes.Clear();
        else StatsBoxes.Show(NmsInventoryContainer.ShipStatBonusesPath(_selectedIndex), BaseStats);
    }

    private void StatsBoxes_Edited(object? sender, EventArgs e) => PageResetBtn.Visibility = Visibility.Visible;




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

        var (targetType, targetClass) = StarshipService.TypeAndClass(targetShip.Index);
        var targetCapacity = await GetShipCapacityAsync();
        var targetViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.ShipTechnologyPath(targetShip.Index),
            StarshipCapacity.Columns,
            StarshipCapacity.SlotsToRows(StarshipService.TechSlots(targetCapacity, targetType, targetClass)));
        targetViewModel.Load();

        var sourcePositions = _techViewModel.Cells.Where(c => c.IsOccupied).Select(c => (c.X, c.Y));
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot,
            $"a copy of {sourceShip.Name}'s", StarshipService.ReadClass(sourceShip.Index), sourcePositions, targetShip.Name, targetViewModel, targetViewModel.CurrentClass);

        if (!confirmed) return;

        targetViewModel.CopyTechStackFrom(_techViewModel, alsoMatchClass);
        StarshipService.SyncCargoClass(targetShip.Index);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadShipList();
    }

    /// <summary>The selected ship as a Full Build template (Save Current, and
    /// "save it first" when deleting).</summary>
    private NmsLoadoutTemplate BuildFullBuildTemplate(string name, ShipEntry source)
    {
        var template = _techViewModel!.ExtractTemplate(name, source.Name, "Ship");
        template.Scope = "FullBuild";
        template.Stats = StatBonusService.ReadAll(NmsInventoryContainer.ShipStatBonusesPath(source.Index));
        template.Seed = SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(source.Index))?.Value<string>();
        template.TypeScenePath = SaveSessionManager.GetValue(
            "vLc", "6f=", "@Cs", source.Index.ToString(), "NTx", "93M")?.Value<string>();
        NewFromTemplateService.CaptureShip(template, source.Index);
        return template;
    }

    /// <summary>Deletes the selected ship from the save (with its corvette
    /// build), after a strict confirmation that can keep it as a template first.
    /// The ship being flown can't go - the game needs one.</summary>
    private async void DeleteShipBtn_Click(object sender, RoutedEventArgs e)
    {
        var ship = _ships.FirstOrDefault(s => s.Index == _selectedIndex);
        if (ship is null || _techViewModel is null) return;
        if (ship.IsGameActive)
        {
            await DeleteConfirmation.ShowBlockedAsync(XamlRoot, "That's the ship you're flying",
                $"{ship.Name} is your active ship, and the game always needs one. Switch to another ship in game, save, then delete this one here.");
            return;
        }

        string whatGoes = "its tech, everything in its cargo, its paint"
            + (ship.BuildParts > 0 ? $" and its corvette build ({ship.BuildParts} parts)" : "");
        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, ship.Name, whatGoes);
        if (!choice.Confirmed) return;

        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(BuildFullBuildTemplate($"{ship.Name} (saved before deleting)", ship));
            await TemplatesPanel.RefreshAsync();
        }
        NewFromTemplateService.RemoveShip(ship.Index);
        _selectedIndex = -1;
        LoadShipList();
    }

    /// <summary>Full Build templates add a whole new Starship in the first
    /// empty slot (NewFromTemplateService) - the Starships you have are never
    /// changed. Tech Stack templates are the ones that Apply onto the selected one.</summary>
    private async Task AddAsNewAsync(NmsLoadoutTemplate template)
    {
        int total = NewFromTemplateService.ShipSlotCount;
        int skeleton = _ships.FirstOrDefault(x => x.Index == _selectedIndex)?.Index ?? _ships.FirstOrDefault()?.Index ?? -1;
        if (skeleton < 0 || NewFromTemplateService.FirstEmptyShipSlot() is not int slot)
        {
            await new ContentDialog
            {
                Title = "No room for another Starship",
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = skeleton < 0
                        ? "This save doesn't have a Starship yet. Get one in game first, then add builds here."
                        : $"All {total} Starship slots are in use. Sell or scrap one in game, then try again."
                },
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = $"Add \"{template.Name}\" as a new Starship?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"It goes in empty slot {slot + 1} of {total} with this build's tech, class, stats and look. "
                    + "Your other Starships aren't changed. Nothing is written to your save until you Save."
            },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        if (NewFromTemplateService.AddShip(template, skeleton) is not int added) return;
        _selectedIndex = added;
        LoadShipList();
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
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot,
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, targetShip.Name, _techViewModel, _techViewModel.CurrentClass);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        StarshipService.SyncCargoClass(_selectedIndex);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadSelectedShip();
    }

    // ------------------------------------------------------------ Templates sidebar (LoadoutTemplatesPanel)

    string? ILoadoutTemplateHost.CurrentName => _techViewModel is null ? null : _ships.FirstOrDefault(s => s.Index == _selectedIndex)?.Name;

    bool ILoadoutTemplateHost.HasTech => _techViewModel?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) =>
        _techViewModel!.ExtractTemplate(name, _ships.FirstOrDefault(s => s.Index == _selectedIndex)!.Name, "Ship");

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) => BuildFullBuildTemplate(name, _ships.FirstOrDefault(s => s.Index == _selectedIndex)!);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => AddAsNewAsync(template);

    /// <summary>The selected ship's whole save entry: name, seed, class, stats, tech and cargo.</summary>
    private string[] SelectedShipPath => NmsInventoryContainer.ShipArrayPath.Append(_selectedIndex.ToString()).ToArray();

    /// <summary>Undoes every staged change to the selected ship - its inventories and
    /// also its name, seed and stats, which the grids' own revert didn't cover.</summary>
    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex >= 0) SaveSessionManager.RevertEditsUnder(SelectedShipPath);
        PageResetBtn.Visibility = Visibility.Collapsed;
        LoadShipList();
    }

    /// <summary>What the Details band says while folded: the ship's name and type.</summary>
    private void ShowFoldedName()
    {
        string name = NameEditBox.Text?.Trim() ?? "";
        string type = (TypeComboBox.SelectedItem as StarshipTypeInfo)?.DisplayName ?? "";
        DetailsCard.FoldedName = name.Length > 0 && type.Length > 0 ? $"{name}  ·  {type}" : name;
    }

    private void Inventory_Edited(object? sender, EventArgs e) => PageResetBtn.Visibility = Visibility.Visible;

    private void Seed_Changed(object? sender, SeedChange change)
    {
        RefreshShipParts();
        PageResetBtn.Visibility = Visibility.Visible;
    }
}
