using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
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

public sealed partial class MultiToolPage : Page, ILoadoutTemplateHost
{
    // IsGameActive is now determined by content-matching against Kgt (a top-level
    // mirror of whichever tool is currently equipped - confirmed via real save
    // diff), NOT the OGV field. OGV looked like an "active" flag with only 2
    // owned tools, but turned out to stay True on every tool that's ever been
    // equipped at least once, not just the current one - a second real-world
    // test (switching tools and re-diffing) disproved it before this shipped.
    //
    // Real bug found 2026-08-01: content-matching breaks down when two or more
    // owned tools have an IDENTICAL tech loadout (same items at the same
    // positions - trivially reachable via this page's own "Copy Tech Stack..."
    // feature), since every matching tool then satisfies the signature check
    // and all showed "ACTIVE" at once. Checked whether Kgt carries any OTHER
    // field that could disambiguate: its own substructure is byte-for-byte
    // identical in shape to a tool's OsQ sub-container (same keys, same
    // values when loadouts match) and carries nothing from the tool's PARENT
    // level (Name/@EL seed/model/etc) - and each tool's own identity seed
    // (@EL) was confirmed to appear exactly once in the whole save file, only
    // on its own entry, nowhere else. So there is no reliable secondary
    // signal anywhere in the save to break a genuine content tie - the save
    // format itself doesn't appear to persist one. LoadToolList below now
    // only accepts the FIRST matching tool as active, which at least
    // guarantees exactly one "ACTIVE" badge (correct in the overwhelmingly
    // common case of no duplicate loadouts, and merely non-deterministic-but-
    // not-wrong in the rare duplicate-content edge case, instead of visibly
    // wrong).
    // Name is the button label; GameName the name the game gives an unnamed
    // tool (MultiToolKinds.GameNameAsync), shown as-is in the name box.
    private sealed record ToolEntry(int Index, string Name, bool IsGameActive, bool IsUnnamed = false, string? GameName = null);

    private InventoryGridViewModel? _techViewModel;
    private int _selectedIndex = -1;
    private int _activeIndex = -1;
    private List<ToolEntry> _tools = new();

    // Class -> max Technology slots, loaded once per page lifetime from the
    // catalog - see MultiToolCapacity for why these numbers live in the
    // database instead of being hardcoded here.
    private Dictionary<string, int>? _multiToolCapacity;

    // Set around SetStatValue's own StageEdit call so OnSessionOrEditsChanged
    // can skip the reload it would otherwise trigger for that same edit -
    // same fix and reasoning as ShipsPage.SetStatValue (2026-08-01): the full
    // reload recreates the Technology InventoryGridViewModel and re-warms the
    // catalog icon cache on every call, none of which changes when only
    // Damage/Mining/Scanner is edited.
    private bool _suppressReloadOnOwnStatEdit;

    public MultiToolPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        LiveItemsService.Changed += OnLiveItemsChanged;
        Unloaded += Page_Unloaded;

        LoadToolList();
        TemplatesPanel.Host = this;
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (_suppressReloadOnOwnStatEdit) return;
        DispatcherQueue.TryEnqueue(LoadToolList);
    }

    /// <summary>The game added or dropped a tool while it played: only the strip
    /// changes, the tool on screen stays (LiveItemsService).</summary>
    private void OnLiveItemsChanged(LiveCollection list)
    {
        if (list == LiveCollection.MultiTools) DispatcherQueue.TryEnqueue(() => LoadToolList(live: true));
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
        LiveItemsService.Changed -= OnLiveItemsChanged;
    }

    private async Task<Dictionary<string, int>> GetMultiToolCapacityAsync()
    {
        _multiToolCapacity ??= await CatalogService.GetMultiToolCapacityAsync();
        return _multiToolCapacity;
    }

    private static int LookupTechSlots(Dictionary<string, int> capacity, string? classLetter) =>
        capacity.TryGetValue(MultiToolCapacity.TechCapacityKey(classLetter), out int slots)
            ? slots : MultiToolCapacity.FallbackTechSlots;

    /// <summary>Reads a tool's Class letter directly from the save, BEFORE any
    /// InventoryGridViewModel exists for it - needed up front to size the grid
    /// correctly, since capacity depends on class (MultiToolCapacity).</summary>
    private static string? GetToolClassLetter(int toolIndex) =>
        SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolTechnologyPath(toolIndex).Append("B@N").Append("1o6").ToArray())?.Value<string>();

    /// <summary>Re-reads the owned multi-tool list from the current session and
    /// rebuilds the selector strip. Defaults selection to the in-game active
    /// tool the first time a session loads; a plain edits-changed refresh keeps
    /// whatever the user already has selected, so mid-edit work on a non-active
    /// tool doesn't get yanked out from under them. A live update (the game
    /// added or dropped a tool) rebuilds only the strip, unless the tool on
    /// screen is the one that changed.</summary>
    private void LoadToolList() => LoadToolList(live: false);

    private void LoadToolList(bool live)
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _tools = new();
            _selectedIndex = -1;
            _activeIndex = -1;
            ToolStrip.Items = null;
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            return;
        }

        if (SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolArrayPath) is not JArray array)
            return;

        var kgtOccupied = SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolActivePath)?[":No"] as JArray;
        var kgtSignature = BuildOccupiedSignature(kgtOccupied);
        _activeIndex = -1;

        var tools = new List<ToolEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            // Occupancy is the model scene path (NTx/93M), NOT NKm (Name) - a
            // brand-new save's starter tool is genuinely owned (real scene
            // path, real seed) but hasn't had a Name written yet, so NKm==""
            // there too; filtering on NKm silently dropped every tool on a
            // fresh save, this one included (real bug found 2026-08-10 by
            // testing against a save that hadn't left the starting planet).
            string scenePath = array[i]?["NTx"]?["93M"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(scenePath)) continue;

            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            bool unnamed = string.IsNullOrEmpty(name);
            if (unnamed) name = $"Multi-Tool {i + 1}"; // until LabelUnnamedAsync names its kind

            // The tool whose OsQ occupied items match Kgt's is the one currently
            // equipped - Kgt is a live mirror of it. Compared as a set of
            // (ItemId, Position) pairs rather than exact JToken.DeepEquals: the
            // latter broke after a real repair + in-game session produced tiny
            // formatting differences (field order, float precision) between the
            // two copies despite them holding the exact same items - a false
            // negative on something that was never actually different.
            var thisOccupied = array[i]?["OsQ"]?[":No"] as JArray;
            var thisSignature = BuildOccupiedSignature(thisOccupied);

            // _activeIndex < 0 guards against a duplicate-loadout tie (see
            // this class's doc comment) - only the first match counts, so at
            // most one tool is ever flagged active even if several tools'
            // signatures all happen to equal Kgt's.
            bool isActive = _activeIndex < 0 && kgtSignature.Count > 0 && kgtSignature.SetEquals(thisSignature);

            if (isActive) _activeIndex = i;
            tools.Add(new ToolEntry(i, name, isActive, unnamed));
        }

        _tools = tools;

        if (_selectedIndex < 0 || _tools.All(t => t.Index != _selectedIndex))
        {
            var defaultTool = _tools.FirstOrDefault(t => t.IsGameActive) ?? _tools.FirstOrDefault();
            _selectedIndex = defaultTool?.Index ?? -1;
        }

        // Unnamed tools are relabelled a moment later, rebuilding the strip; a
        // new tool's slide-in waits for that so it isn't cut off halfway.
        _labelsPending = _tools.Any(t => t.IsUnnamed);
        BuildSelectorStrip();
        if (!live || _shownIndex != _selectedIndex || LiveItemsService.IsNew(LiveCollection.MultiTools, _selectedIndex))
            LoadSelectedTool();
        _ = LabelUnnamedAsync();
    }

    private bool _labelsPending;

    /// <summary>A tool the player never named has no name in the save - the
    /// game makes one up from its seed ("Pillar of Ooruz-IX"), and so does
    /// ArtifactX (MultiToolNames, 2026-10-16), for the button and the name box.
    /// Before the game data has the name words, the button shows type and
    /// class ("Standard · B class") instead, worded so it doesn't pass for a
    /// name, and the name box stays empty (user, 2026-10-16).</summary>
    private async Task LabelUnnamedAsync()
    {
        var tools = _tools;
        if (!tools.Any(t => t.IsUnnamed)) return;
        var labelled = new List<ToolEntry>();
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tools)
        {
            if (!t.IsUnnamed) { labelled.Add(t); continue; }
            string scene = SaveSessionManager.GetValue("vLc", "6f=", "SuJ", t.Index.ToString(), "NTx", "93M")?.Value<string>() ?? "";
            string? gameName = SeedOf(t.Index) is ulong seed ? await MultiToolKinds.GameNameAsync(seed, scene) : null;
            string? label = gameName;
            if (label is null)
            {
                string? kind = MultiToolKinds.IsMainModel(scene) ? await KindOfAsync(t.Index) : MultiToolKinds.NameFor(scene, "");
                if (string.IsNullOrEmpty(kind)) { labelled.Add(t); continue; }
                label = GetToolClassLetter(t.Index) is { Length: > 0 } cls ? $"{kind} · {cls} class" : kind;
            }
            // The same tool twice (same seed) has the same name in game too.
            int n = used[label] = used.GetValueOrDefault(label) + 1;
            labelled.Add(t with { Name = n == 1 ? label : $"{label} ({n})", GameName = gameName });
        }
        if (!ReferenceEquals(tools, _tools)) return; // the list was rebuilt meanwhile
        _tools = labelled;
        _labelsPending = false;
        BuildSelectorStrip();
        if (_tools.FirstOrDefault(t => t.Index == _selectedIndex) is { IsUnnamed: true } selected)
        {
            TechHeaderTxt.Text = $"TECHNOLOGY  —  {selected.Name.ToUpperInvariant()}";
            ShowName(selected);
        }
    }

    /// <summary>Reduces a :No array down to a set of "ItemId@X,Y" strings - just
    /// enough to identify which items sit where, ignoring everything else
    /// (Amount, b76/eVk, exact field order). Two containers holding the same
    /// items in the same positions are "the same tool" for active-detection
    /// purposes, regardless of any other field-level differences between them.</summary>
    private static HashSet<string> BuildOccupiedSignature(JArray? array)
    {
        var result = new HashSet<string>();
        if (array is null) return result;

        foreach (var entry in array)
        {
            string? itemId = entry["b2n"]?.Value<string>();
            var pos = entry["3ZH"];
            if (itemId is null || pos is null) continue;

            int x = pos[">Qh"]?.Value<int>() ?? -1;
            int y = pos["XJ>"]?.Value<int>() ?? -1;
            result.Add($"{itemId}@{x},{y}");
        }

        return result;
    }

    private void BuildSelectorStrip()
    {
        bool IsNew(int index) => LiveItemsService.IsNew(LiveCollection.MultiTools, index);
        ToolStrip.Items = _tools
            .Select(tool => EquipmentChipViewModel.Item(tool.Index, tool.Name, tool.Index == _selectedIndex, tool.IsGameActive,
                isNew: IsNew(tool.Index),
                animateIn: IsNew(tool.Index) && !_labelsPending && LiveItemsService.TakeAnimation(LiveCollection.MultiTools, tool.Index)))
            .Concat(LiveItemsService.Gone(LiveCollection.MultiTools).Select(EquipmentChipViewModel.Gone))
            .ToList();
    }

    private void ToolStrip_ChipClicked(object? sender, EquipmentChipViewModel chip)
    {
        _selectedIndex = chip.Index;
        if (LiveItemsService.IsNew(LiveCollection.MultiTools, chip.Index))
        {
            LiveItemsService.Seen(LiveCollection.MultiTools, chip.Index); // Changed -> LoadToolList shows it
            return;
        }
        BuildSelectorStrip();
        LoadSelectedTool();
    }

    /// <summary>Multi-tool module families, from the 94 tools in the real saves
    /// (2026-10-13): each tops out at 3 per tool. UP_SENGUN is a one-off
    /// salvaged find, so it isn't offered.</summary>
    private static readonly string[] ModuleFamilies =
        { "UP_LASER", "UP_SCAN", "UP_BOLT", "UP_SMG", "UP_SHOT", "UP_CANN", "UP_RAIL", "UP_GREN", "UP_TGREN" };

    private static readonly string[] TechGroupOrder = { "Mining", "Scanning", "Weapons", "Utilities" };

    /// <summary>Groups by the main stat each tech works on (the game's
    /// BaseStat, via the catalog's TechStats), same as Starships.</summary>
    private static string TechGroup(CatalogPart tech)
    {
        string stat = tech.Stat ?? "";
        if (stat is "Weapon_Laser" or "Weapon_SunLaser") return "Mining";
        if (stat.StartsWith("Weapon_Scan")) return "Scanning";
        if (stat is "Weapon_Projectile" or "Weapon_Burst" or "Weapon_Shotgun" or "Weapon_Cannon" or "Weapon_Rail"
            or "Weapon_Grenade" or "Weapon_MineGrenade" or "Weapon_StunGrenade" or "Weapon_Flame" or "Weapon_Stun"
            or "Weapon_Gravity" or "Weapon_SoulLaser") return "Weapons";
        return "Utilities";
    }

    private void TechCargoStack_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutTechList();
    private void MainColumn_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutTechList();

    /// <summary>Tech list beside the tech card, as tall as it, or underneath
    /// at a fixed height when the window is too narrow.</summary>
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

    private async void LoadSelectedTool()
    {
        _shownIndex = _selectedIndex;
        await LoadSelectedToolAsync();
    }

    // The tool whose details are on screen - a live update leaves it there
    // unless it's the one that changed.
    private int _shownIndex = -1;

    private async Task LoadSelectedToolAsync()
    {
        if (_selectedIndex < 0)
        {
            _techViewModel = null;
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            TechListCard.Visibility = Visibility.Collapsed;
            TechHeaderTxt.Text = "TECHNOLOGY";
            ClassPicker.Visibility = Visibility.Collapsed;
            NameEditBox.Text = "";
            SelectedToolClassTxt.Text = "—";
            SelectedToolClassBadge.Background = ThemeResources.Brush("ThemePanelAltBrush");
            DamageStatBox.Value = double.NaN;
            MiningStatBox.Value = double.NaN;
            ScannerStatBox.Value = double.NaN;
            TypeComboBox.SelectedItem = null;
            KindTxt.Visibility = Visibility.Collapsed;
            ShowToolPreview();
            return;
        }

        var selectedTool = _tools.First(t => t.Index == _selectedIndex);
        ShowToolPreview();

        string? classLetter = GetToolClassLetter(_selectedIndex);
        var capacity = await GetMultiToolCapacityAsync();
        int techRows = MultiToolCapacity.SlotsToRows(LookupTechSlots(capacity, classLetter));

        // Kgt looked like the safer target for editing the active tool, but a
        // real save/load test proved it wrong: edits written there (an item add
        // AND a class change) never showed up in-game. Kgt is a derived/cached
        // mirror the game regenerates FROM SuJ[i].OsQ, not the other way around -
        // so SuJ[i].OsQ is the one true source regardless of active status.
        _techViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.MultiToolTechnologyPath(_selectedIndex),
            MultiToolCapacity.Columns,
            techRows);

        _techViewModel.Load();

        var itemIds = _techViewModel.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId);
        await CatalogService.WarmCacheAsync(itemIds);

        TechGrid.ViewModel = _techViewModel;
        TechGrid.AllowedCategories = new[] { "Technology" };
        TechGrid.AllowedTemplateTypes = new[] { "GcTechnologyTable" };
        // Every tech on the 94 multi-tools in the real saves is "Weapon" (2026-10-13).
        TechGrid.AllowedUsageCategories = new[] { "Weapon" };

        // Reload can run many times per page lifetime (every selector click) -
        // unsubscribe first so CellChanged doesn't fire PageResetBtn's handler
        // multiple times per single actual edit.
        TechGrid.CellChanged -= TechGrid_CellChanged;
        TechGrid.CellChanged += TechGrid_CellChanged;

        TechGrid.Refresh();

        TechHeaderTxt.Text = $"TECHNOLOGY  —  {selectedTool.Name.ToUpperInvariant()}";
        PageResetBtn.Visibility = _techViewModel.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;

        // The tech a multi-tool can fit, to click in - the same list limits
        // the right-click search.
        TechListCard.Visibility = Visibility.Visible;
        TechListPanel.IsBestClass = () => _techViewModel?.CurrentClass == "S";
        TechListPanel.SwitchToBestClassAsync = async () =>
        {
            _techViewModel?.SetClass("S");
            PageResetBtn.Visibility = Visibility.Visible;
            await LoadSelectedToolAsync();
        };
        TechListPanel.MoreRoomHint = () => _techViewModel?.CurrentClass == "S" ? null
            : "S class has more slots - change the class above to fit more.";
        _ = TechListPanel.LoadAsync(TechGrid, async () =>
        {
            var tech = await CatalogService.GetTechForAsync(new[] { "Weapon" });
            TechGrid.AllowedItemIds = tech.Select(t => t.GameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modules = await CatalogService.GetUpgradeModulesAsync(ModuleFamilies);
            return Controls.ItemListPanel.SortByGroups(tech.Concat(modules), TechGroup, TechGroupOrder);
        }, "technology", "technologies", "Technology", TechGroup);

        ShowClass();
        // The list may have been relabelled (LabelUnnamedAsync) while this loaded.
        var shown = _tools.FirstOrDefault(t => t.Index == selectedTool.Index) ?? selectedTool;
        ShowName(shown);
        TechHeaderTxt.Text = $"TECHNOLOGY  —  {shown.Name.ToUpperInvariant()}";
        ShowFoldedName();
        UpdateClassBadge();
        UpdateStatDisplay();
        await SyncTypeComboBoxAsync();
    }

    // Replaces ArtifactX.Core.GameKnowledge/ArtifactX.Almanac's curated list entirely - this
    // is now sourced live from the catalog's MultiToolTypes category, which
    // CatalogBuildService discovers by filename rule rather than a maintained
    // list anywhere in this project. See that service for the actual rule.
    private sealed record MultiToolTypeOption(string DisplayName, string ScenePath);

    private List<MultiToolTypeOption>? _multiToolTypeOptions;

    private bool _suppressTypeSelectionEvent;
    private bool _suppressStatChangeEvent;

    /// <summary>Loads the Type option list from the catalog once per page lifetime
    /// (options don't change mid-session), then syncs the combo's selection to the
    /// current tool's scene path, suppressing SelectionChanged so re-syncing
    /// doesn't stage a spurious edit. If the current path doesn't match any known
    /// Type, selection is cleared - expected for a Type the catalog's filename
    /// rule hasn't picked up (e.g. a brand-new game update not yet re-cataloged),
    /// not a bug.</summary>
    private async Task SyncTypeComboBoxAsync()
    {
        _suppressTypeSelectionEvent = true;
        try
        {
            var raw = await CatalogService.GetMultiToolTypesAsync();
            string? currentPath = _selectedIndex < 0 ? null
                : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", _selectedIndex.ToString(), "NTx", "93M")?.Value<string>();
            // The main model's name is the kind this tool's seed makes (Alien,
            // Experimental...); the other models use the game's type names.
            string mainName = "Standard";
            if (MultiToolKinds.IsMainModel(currentPath) && await KindAsync() is string kind) mainName = kind;
            _multiToolTypeOptions = raw.Select(t => new MultiToolTypeOption(
                MultiToolKinds.IsMainModel(t.ScenePath) ? mainName : MultiToolKinds.NameFor(t.ScenePath, t.DisplayName), t.ScenePath))
                .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
            TypeComboBox.ItemsSource = _multiToolTypeOptions;
            TypeComboBox.SelectedItem = _multiToolTypeOptions.FirstOrDefault(t =>
                string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));
            KindTxt.Text = "Set by the tool's seed";
            KindTxt.Visibility = MultiToolKinds.IsMainModel(currentPath) ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _suppressTypeSelectionEvent = false; }
    }

    private Task<string?> KindAsync() => KindOfAsync(_selectedIndex);

    private static async Task<string?> KindOfAsync(int index) =>
        SeedOf(index) is ulong seed ? await MultiToolKinds.KindAsync(seed) : null;

    /// <summary>A tool's own seed (SuJ[i].@EL[1]), which picks its look, kind and name.</summary>
    private static ulong? SeedOf(int index)
    {
        string? seedText = index < 0 ? null
            : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", index.ToString(), "@EL", "1")?.Value<string>();
        if (seedText is not { Length: > 2 }) return null;
        string hex = seedText.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? seedText[2..] : seedText;
        return ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong seed)
            ? seed : null;
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTypeSelectionEvent) return;
        if (TypeComboBox.SelectedItem is not MultiToolTypeOption type) return;

        SetType(type.ScenePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Stages the model scene path (NTx.93M) - the only field the visual
    /// swap actually needs, confirmed via a real in-game test. jl; (which a
    /// reference tool writes alongside it) was tested and found to have no
    /// effect on rendering, so it's deliberately left untouched here.</summary>
    private void SetType(string scenePath)
    {
        if (_selectedIndex < 0) return;
        SaveSessionManager.StageEdit(scenePath, "vLc", "6f=", "SuJ", _selectedIndex.ToString(), "NTx", "93M");
    }

    /// <summary>Class badge color follows the same rough tiering the game itself
    /// uses (gold S, purple A, blue B, green C) purely as a visual cue - not read
    /// from anywhere, just a local mapping.</summary>
    private void UpdateClassBadge()
    {
        string classLetter = _techViewModel?.CurrentClass ?? "—";
        SelectedToolClassTxt.Text = classLetter;

        Color badgeColor = classLetter switch
        {
            "S" => Color.FromArgb(255, 201, 151, 34),
            "A" => Color.FromArgb(255, 130, 80, 200),
            "B" => Color.FromArgb(255, 60, 110, 200),
            "C" => Color.FromArgb(255, 60, 150, 90),
            _ => Colors.Transparent
        };
        SelectedToolClassBadge.Background = badgeColor == Colors.Transparent
            ? ThemeResources.Brush("ThemePanelAltBrush")
            : new SolidColorBrush(badgeColor);
    }

    /// <summary>Loads OsQ.@bB's raw continuous stat rolls (WEAPON_DAMAGE/
    /// WEAPON_MINING/WEAPON_SCAN) into the editable NumberBoxes. These are the
    /// same values that feed the game's own Damage Potential/Scanner Range
    /// figures. Confirmed independent of Type/model (stayed identical across
    /// every model-swap Type we tested) - so editing them here doesn't touch
    /// which model renders, only the underlying stat rolls, exactly like
    /// a reference tool's own "Base Stats" fields.</summary>
    private void UpdateStatDisplay()
    {
        _suppressStatChangeEvent = true;

        var bonuses = SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolTechnologyPath(_selectedIndex).Append("@bB").ToArray()) as JArray;

        double? Find(string key) =>
            bonuses?.FirstOrDefault(b => b["QL1"]?.Value<string>() == key)?[">MX"]?.Value<double>();

        DamageStatBox.Value = Find("^WEAPON_DAMAGE") ?? double.NaN;
        MiningStatBox.Value = Find("^WEAPON_MINING") ?? double.NaN;
        ScannerStatBox.Value = Find("^WEAPON_SCAN") ?? double.NaN;

        _suppressStatChangeEvent = false;
    }

    /// <summary>Stages a new value for one @bB entry, matched by its QL1 key
    /// (WEAPON_DAMAGE/WEAPON_MINING/WEAPON_SCAN) rather than a fixed array
    /// index, in case a future save ever orders them differently. Rebuilds and
    /// stages the WHOLE @bB array at once - staging just the one entry's >MX
    /// leaf looked correct but silently reverted on the next read, because
    /// SaveSessionManager only recognizes a staged edit at the EXACT path
    /// queried; UpdateStatDisplay reads the array as a whole, so a deeper leaf
    /// edit was never seen by it. Same whole-array pattern :No/hl?/MMm already
    /// use for exactly this reason. No upper bound is enforced - confirmed
    /// the game itself doesn't enforce one either, and these are plain
    /// floats, not structural/indexing fields, so an extreme value can
    /// unbalance a save but won't corrupt it.</summary>
    private void SetStatValue(string weaponKey, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;

        var containerPath = NmsInventoryContainer.MultiToolTechnologyPath(_selectedIndex);
        var bonusesPath = containerPath.Append("@bB").ToArray();
        var bonuses = SaveSessionManager.GetValue(bonusesPath) as JArray;
        if (bonuses is null) return;

        var updated = new JArray();
        foreach (var entry in bonuses)
        {
            if (entry is JObject obj && obj["QL1"]?.Value<string>() == weaponKey)
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
        SetStatValue("^WEAPON_DAMAGE", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Redundant safety net alongside ValueChanged - some WinUI3
    /// NumberBox versions don't reliably fire ValueChanged on a plain focus
    /// loss (only guaranteed on Enter/certain validation triggers), which can
    /// look like "my edit reverted" when it's really that staging never ran at
    /// all. Reading sender.Value directly here and re-staging is a harmless,
    /// idempotent no-op if ValueChanged already handled it correctly.</summary>
    private void DamageStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(DamageStatBox.Value))
        {
            SetStatValue("^WEAPON_DAMAGE", DamageStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    private void MiningStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^WEAPON_MINING", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void MiningStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(MiningStatBox.Value))
        {
            SetStatValue("^WEAPON_MINING", MiningStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    private void ScannerStatBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent) return;
        SetStatValue("^WEAPON_SCAN", args.NewValue);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ScannerStatBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent) return;
        if (!double.IsNaN(ScannerStatBox.Value))
        {
            SetStatValue("^WEAPON_SCAN", ScannerStatBox.Value);
            PageResetBtn.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Stages the rename when the field loses focus - not on every
    /// keystroke, since that would rebuild the selector strip (via LoadToolList,
    /// which re-reads names for its buttons) mid-typing and fight the user's
    /// cursor. Only stages if the text actually changed from the tool's current
    /// name, so tabbing through without editing doesn't create a no-op edit.</summary>
    /// <summary>The name box: the player's name, or the one the game made up
    /// for an unnamed tool. Either way leaving the box unchanged writes nothing,
    /// so the game's name never becomes a saved custom name by accident.</summary>
    private void ShowName(ToolEntry tool)
    {
        NameEditBox.Text = tool.IsUnnamed ? tool.GameName ?? "" : tool.Name;
        // No game name: game data from before the name words (format 11) - say how to get it.
        bool oldData = GameDataService.Info is { } info && info.Format < ArtifactX.GameData.GameDataBuilder.CatalogFormat;
        // The way to fix it, right where it shows (the bar at the top is easy to miss).
        NameUpdateLink.Visibility = tool.IsUnnamed && tool.GameName is null && oldData && !GameDataService.IsBuilding
            ? Visibility.Visible : Visibility.Collapsed;
        NameEditBox.PlaceholderText = tool.IsUnnamed && tool.GameName is null
            ? (oldData ? "Update your game data to see the game's name" : "The game's own name couldn't be worked out")
            : "";
    }

    private void NameUpdateLink_Click(object sender, RoutedEventArgs e)
    {
        NameUpdateLink.Visibility = Visibility.Collapsed;
        App.MainWindowInstance?.StartGameDataUpdate();
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        var currentTool = _tools.FirstOrDefault(t => t.Index == _selectedIndex);
        if (currentTool != null && newName == (currentTool.IsUnnamed ? currentTool.GameName : currentTool.Name)) return;

        var namePath = new[] { "vLc", "6f=", "SuJ", _selectedIndex.ToString(), "NKm" };
        SaveSessionManager.StageEdit(newName, namePath);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadToolList();
    }

    /// <summary>S/A/B/C rating buttons - a plain literal string field (B@N.1o6),
    /// confirmed independent of the tool's identity/appearance seed, so picking
    /// a different class here is a simple, safe single-value write.</summary>
    private void ShowClass()
    {
        ClassPicker.Visibility = _techViewModel is null ? Visibility.Collapsed : Visibility.Visible;
        ClassPicker.SelectedClass = _techViewModel?.CurrentClass;
    }

    private void ClassPicker_ClassChosen(object? sender, string letter)
    {
        // A class change can change this tool's real Technology slot
        // total (MultiToolCapacity), so the grid gets fully rebuilt via
        // LoadSelectedTool rather than just refreshed in place.
        _techViewModel?.SetClass(letter);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadSelectedTool();
    }

    private void TechGrid_CellChanged(object? sender, EventArgs e) =>
        PageResetBtn.Visibility = Visibility.Visible;

    private void UnlockAllTech_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.UnlockAll();
        TechGrid.Refresh();
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

    /// <summary>Copies the currently selected tool's entire tech loadout onto
    /// another owned tool, after showing exactly what else that copy will
    /// require: any slots that need unlocking to fit it, and whether the two
    /// tools' classes differ (with an opt-in checkbox to also match class -
    /// never silent, never assumed). Only :No, hl?, and (if opted in) Class are
    /// touched - nothing else about the target changes.</summary>
    private async void CopyTechStackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var sourceTool = _tools.FirstOrDefault(t => t.Index == _selectedIndex);
        if (sourceTool is null) return;

        var otherTools = _tools.Where(t => t.Index != _selectedIndex).ToList();
        if (otherTools.Count == 0)
        {
            await new ContentDialog
            {
                Title = "No other tools",
                Content = "You only own one multi-tool, so there's nothing to copy this stack onto.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        // Step 1: pick which owned tool to copy onto.
        var pickerList = new ListView { SelectionMode = ListViewSelectionMode.Single };
        foreach (var t in otherTools)
            pickerList.Items.Add(t.Name);

        var pickerDialog = new ContentDialog
        {
            Title = $"Copy {sourceTool.Name}'s tech stack to...",
            Content = pickerList,
            PrimaryButtonText = "Next",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        pickerList.SelectionChanged += (_, _) => pickerDialog.IsPrimaryButtonEnabled = pickerList.SelectedIndex >= 0;

        if (await pickerDialog.ShowAsync() != ContentDialogResult.Primary || pickerList.SelectedIndex < 0)
            return;

        var targetTool = otherTools[pickerList.SelectedIndex];

        string? targetClassLetter = GetToolClassLetter(targetTool.Index);
        var targetCapacity = await GetMultiToolCapacityAsync();
        var targetViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.MultiToolTechnologyPath(targetTool.Index),
            MultiToolCapacity.Columns,
            MultiToolCapacity.SlotsToRows(LookupTechSlots(targetCapacity, targetClassLetter)));
        targetViewModel.Load();

        var sourcePositions = _techViewModel.Cells.Where(c => c.IsOccupied).Select(c => (c.X, c.Y));
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot,
            $"a copy of {sourceTool.Name}'s", _techViewModel.CurrentClass, sourcePositions, targetTool.Name, targetViewModel, targetViewModel.CurrentClass);

        if (!confirmed) return;

        targetViewModel.CopyTechStackFrom(_techViewModel, alsoMatchClass);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadToolList();
    }

    /// <summary>The selected tool as a Full Build template (Save Current, and
    /// "save it first" when deleting).</summary>
    private NmsLoadoutTemplate BuildFullBuildTemplate(string name, ToolEntry source)
    {
        var template = _techViewModel!.ExtractTemplate(name, source.Name, "MultiTool");
        template.Scope = "FullBuild";
        template.Stats = ExtractStatsList(source.Index);
        template.TypeScenePath = SaveSessionManager.GetValue(
            "vLc", "6f=", "SuJ", source.Index.ToString(), "NTx", "93M")?.Value<string>();
        NewFromTemplateService.CaptureMultiTool(template, source.Index);
        return template;
    }

    /// <summary>Deletes the selected Multi-Tool from the save, after a strict
    /// confirmation that can keep it as a template first. The equipped tool
    /// can't go - the game needs one in hand.</summary>
    private async void DeleteToolBtn_Click(object sender, RoutedEventArgs e)
    {
        var tool = _tools.FirstOrDefault(t => t.Index == _selectedIndex);
        if (tool is null || _techViewModel is null) return;
        if (tool.IsGameActive)
        {
            await DeleteConfirmation.ShowBlockedAsync(XamlRoot, "That's the Multi-Tool you're holding",
                $"{tool.Name} is your equipped Multi-Tool, and the game always needs one. Switch to another in game, save, then delete this one here.");
            return;
        }

        var choice = await DeleteConfirmation.ShowAsync(XamlRoot, tool.Name, "its tech, its stats and its look");
        if (!choice.Confirmed) return;

        if (choice.SaveTemplateFirst)
        {
            await LoadoutTemplateService.SaveAsync(BuildFullBuildTemplate($"{tool.Name} (saved before deleting)", tool));
            await TemplatesPanel.RefreshAsync();
        }
        NewFromTemplateService.RemoveMultiTool(tool.Index);
        _selectedIndex = -1;
        LoadToolList();
    }

    /// <summary>Reads a tool's whole stat-bonus array (@bB, nested under its
    /// Technology container) as a plain key/value list, for embedding in a
    /// Full Build template - captures every entry generically (not just the 3
    /// keys exposed as NumberBoxes) so nothing present but not individually
    /// surfaced gets silently dropped on save.</summary>
    private static List<NmsLoadoutStat> ExtractStatsList(int toolIndex)
    {
        var bonuses = SaveSessionManager.GetValue(
            NmsInventoryContainer.MultiToolTechnologyPath(toolIndex).Append("@bB").ToArray()) as JArray;
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

    /// <summary>Full Build templates add a whole new Multi-Tool in the first
    /// empty slot (NewFromTemplateService) - the Multi-Tools you have are never
    /// changed. Tech Stack templates are the ones that Apply onto the selected one.</summary>
    private async Task AddAsNewAsync(NmsLoadoutTemplate template)
    {
        int total = NewFromTemplateService.MultiToolSlotCount;
        int skeleton = _tools.FirstOrDefault(x => x.Index == _selectedIndex)?.Index ?? _tools.FirstOrDefault()?.Index ?? -1;
        if (skeleton < 0 || NewFromTemplateService.FirstEmptyMultiToolSlot() is not int slot)
        {
            await new ContentDialog
            {
                Title = "No room for another Multi-Tool",
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = skeleton < 0
                        ? "This save doesn't have a Multi-Tool yet. Get one in game first, then add builds here."
                        : $"All {total} Multi-Tool slots are in use. Sell or scrap one in game, then try again."
                },
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = $"Add \"{template.Name}\" as a new Multi-Tool?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"It goes in empty slot {slot + 1} of {total} with this build's tech, class, stats and look. "
                    + "Your other Multi-Tools aren't changed. Nothing is written to your save until you Save."
            },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        if (NewFromTemplateService.AddMultiTool(template, skeleton) is not int added) return;
        _selectedIndex = added;
        LoadToolList();
    }

    /// <summary>Applies a template to whichever tool is currently selected in
    /// the main strip - no separate "pick a target" step, since the whole point
    /// of the persistent list sitting next to the tool selector is that the
    /// target is already whatever you're looking at.</summary>
    private async Task ApplyTemplateAsync(NmsLoadoutTemplate template)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var targetTool = _tools.FirstOrDefault(t => t.Index == _selectedIndex);
        if (targetTool is null) return;

        var sourcePositions = template.UnlockedPositions.Select(p => (p.X, p.Y));
        var (confirmed, alsoMatchClass) = await TechStackCopyConfirmation.ShowAsync(XamlRoot,
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, targetTool.Name, _techViewModel, _techViewModel.CurrentClass);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        PageResetBtn.Visibility = Visibility.Visible;

        // A class change (alsoMatchClass) can change this tool's real
        // Technology slot total (MultiToolCapacity), so the grid gets fully
        // rebuilt via LoadSelectedTool rather than just refreshed in place.
        LoadSelectedTool();
    }

    // ------------------------------------------------------------ Templates sidebar (LoadoutTemplatesPanel)

    string? ILoadoutTemplateHost.CurrentName => _techViewModel is null ? null : _tools.FirstOrDefault(t => t.Index == _selectedIndex)?.Name;

    bool ILoadoutTemplateHost.HasTech => _techViewModel?.Cells.Any(c => c.IsOccupied) == true;

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureTechStack(string name) =>
        _techViewModel!.ExtractTemplate(name, _tools.FirstOrDefault(t => t.Index == _selectedIndex)!.Name, "MultiTool");

    NmsLoadoutTemplate ILoadoutTemplateHost.CaptureFullBuild(string name) => BuildFullBuildTemplate(name, _tools.FirstOrDefault(t => t.Index == _selectedIndex)!);

    Task ILoadoutTemplateHost.ApplyTechStackAsync(NmsLoadoutTemplate template) => ApplyTemplateAsync(template);

    Task ILoadoutTemplateHost.UseFullBuildAsync(NmsLoadoutTemplate template) => AddAsNewAsync(template);

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.Revert();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    /// <summary>What the Details band says while folded: the multi-tool's name (or the game's name for an unnamed one).</summary>
    private void ShowFoldedName() => DetailsCard.FoldedName = NameEditBox.Text?.Trim() is { Length: > 0 } typed ? typed : NameEditBox.PlaceholderText ?? "";
}
