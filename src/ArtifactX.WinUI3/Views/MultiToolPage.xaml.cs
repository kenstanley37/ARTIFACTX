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

public sealed partial class MultiToolPage : Page
{
    // IsGameActive is now determined by content-matching against Kgt (a top-level
    // mirror of whichever tool is currently equipped - confirmed via real save
    // diff), NOT the OGV field. OGV looked like an "active" flag with only 2
    // owned tools, but turned out to stay True on every tool that's ever been
    // equipped at least once, not just the current one - a second real-world
    // test (switching tools and re-diffing) disproved it before this shipped.
    private sealed record ToolEntry(int Index, string Name, bool IsGameActive);

    private InventoryGridViewModel? _techViewModel;
    private int _selectedIndex = -1;
    private int _activeIndex = -1;
    private List<ToolEntry> _tools = new();

    // Class -> max Technology slots, loaded once per page lifetime from the
    // catalog - see MultiToolCapacity for why these numbers live in the
    // database instead of being hardcoded here.
    private Dictionary<string, int>? _multiToolCapacity;

    public MultiToolPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;

        LoadToolList();
        _ = RefreshTemplatesListAsync();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadToolList);

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
    /// tool doesn't get yanked out from under them.</summary>
    private void LoadToolList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _tools = new();
            _selectedIndex = -1;
            _activeIndex = -1;
            ToolSelectorPanel.Children.Clear();
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
            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            // The tool whose OsQ occupied items match Kgt's is the one currently
            // equipped - Kgt is a live mirror of it. Compared as a set of
            // (ItemId, Position) pairs rather than exact JToken.DeepEquals: the
            // latter broke after a real repair + in-game session produced tiny
            // formatting differences (field order, float precision) between the
            // two copies despite them holding the exact same items - a false
            // negative on something that was never actually different.
            var thisOccupied = array[i]?["OsQ"]?[":No"] as JArray;
            var thisSignature = BuildOccupiedSignature(thisOccupied);
            bool isActive = kgtSignature.Count > 0 && kgtSignature.SetEquals(thisSignature);

            if (isActive) _activeIndex = i;
            tools.Add(new ToolEntry(i, name, isActive));
        }

        _tools = tools;

        if (_selectedIndex < 0 || _tools.All(t => t.Index != _selectedIndex))
        {
            var defaultTool = _tools.FirstOrDefault(t => t.IsGameActive) ?? _tools.FirstOrDefault();
            _selectedIndex = defaultTool?.Index ?? -1;
        }

        BuildSelectorStrip();
        LoadSelectedTool();
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
        ToolSelectorPanel.Children.Clear();

        foreach (var tool in _tools)
        {
            bool isSelected = tool.Index == _selectedIndex;

            var button = new Button
            {
                Content = BuildButtonContent(tool, isSelected),
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 255, 157, 0)   // selected - accent orange
                    : Color.FromArgb(255, 90, 98, 112)),  // unselected - neutral
                Background = new SolidColorBrush(isSelected
                    ? Color.FromArgb(60, 255, 157, 0)
                    : Color.FromArgb(20, 255, 255, 255))
            };

            button.Click += (_, _) =>
            {
                _selectedIndex = tool.Index;
                BuildSelectorStrip();
                LoadSelectedTool();
            };

            ToolSelectorPanel.Children.Add(button);
        }
    }

    /// <summary>Selected state bolds the name; game-active state adds a small
    /// green "ACTIVE" badge - the two are independent and can appear together
    /// or separately (e.g. viewing a tool that isn't the one you're carrying).</summary>
    private static StackPanel BuildButtonContent(ToolEntry tool, bool isSelected)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        panel.Children.Add(new TextBlock
        {
            Text = tool.Name,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (tool.IsGameActive)
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

    private async void LoadSelectedTool()
    {
        if (_selectedIndex < 0)
        {
            _techViewModel = null;
            TechGrid.ViewModel = null;
            TechGrid.Refresh();
            TechHeaderTxt.Text = "Technology";
            ClassSelectorPanel.Children.Clear();
            NameEditBox.Text = "";
            SelectedToolClassTxt.Text = "—";
            SelectedToolClassBadge.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            DamageStatBox.Value = double.NaN;
            MiningStatBox.Value = double.NaN;
            ScannerStatBox.Value = double.NaN;
            TypeComboBox.SelectedItem = null;
            return;
        }

        var selectedTool = _tools.First(t => t.Index == _selectedIndex);

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
        TechGrid.AllowedUsageCategories = new[] { "Weapon", "All" };

        // Reload can run many times per page lifetime (every selector click) -
        // unsubscribe first so CellChanged doesn't fire PageResetBtn's handler
        // multiple times per single actual edit.
        TechGrid.CellChanged -= TechGrid_CellChanged;
        TechGrid.CellChanged += TechGrid_CellChanged;

        TechGrid.Refresh();

        TechHeaderTxt.Text = $"Technology - {selectedTool.Name}";
        PageResetBtn.Visibility = _techViewModel.HasLocalChanges ? Visibility.Visible : Visibility.Collapsed;

        BuildClassSelector();
        NameEditBox.Text = selectedTool.Name;
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

        if (_multiToolTypeOptions is null)
        {
            var raw = await CatalogService.GetMultiToolTypesAsync();
            _multiToolTypeOptions = raw.Select(t => new MultiToolTypeOption(t.DisplayName, t.ScenePath)).ToList();
            TypeComboBox.ItemsSource = _multiToolTypeOptions;
        }

        if (_selectedIndex < 0)
        {
            TypeComboBox.SelectedItem = null;
            _suppressTypeSelectionEvent = false;
            return;
        }

        string? currentPath = SaveSessionManager.GetValue(
            "vLc", "6f=", "SuJ", _selectedIndex.ToString(), "NTx", "93M")?.Value<string>();

        TypeComboBox.SelectedItem = _multiToolTypeOptions.FirstOrDefault(t =>
            string.Equals(t.ScenePath, currentPath, StringComparison.OrdinalIgnoreCase));

        _suppressTypeSelectionEvent = false;
    }

    private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTypeSelectionEvent) return;
        if (TypeComboBox.SelectedItem is not MultiToolTypeOption type) return;

        SetType(type.ScenePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Stages the model scene path (NTx.93M) - the only field the visual
    /// swap actually needs, confirmed via a real in-game test. jl; (which NomNom
    /// writes alongside it) was tested and found to have no effect on rendering,
    /// so it's deliberately left untouched here.</summary>
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
            _ => Color.FromArgb(60, 255, 255, 255)
        };
        SelectedToolClassBadge.Background = new SolidColorBrush(badgeColor);
    }

    /// <summary>Loads OsQ.@bB's raw continuous stat rolls (WEAPON_DAMAGE/
    /// WEAPON_MINING/WEAPON_SCAN) into the editable NumberBoxes. These are the
    /// same values that feed the game's own Damage Potential/Scanner Range
    /// figures. Confirmed independent of Type/model (stayed identical across
    /// every model-swap Type we tested) - so editing them here doesn't touch
    /// which model renders, only the underlying stat rolls, exactly like
    /// NomNom's own "Base Stats" fields.</summary>
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
    /// neither the game nor NomNom enforces one either, and these are plain
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

        SaveSessionManager.StageEdit(updated, bonusesPath);
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
    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        var currentTool = _tools.FirstOrDefault(t => t.Index == _selectedIndex);
        if (currentTool != null && newName == currentTool.Name) return;

        var namePath = new[] { "vLc", "6f=", "SuJ", _selectedIndex.ToString(), "NKm" };
        SaveSessionManager.StageEdit(newName, namePath);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadToolList();
    }

    /// <summary>S/A/B/C rating buttons - a plain literal string field (B@N.1o6),
    /// confirmed independent of the tool's identity/appearance seed, so picking
    /// a different class here is a simple, safe single-value write.</summary>
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
                // A class change can change this tool's real Technology slot
                // total (MultiToolCapacity), so the grid gets fully rebuilt via
                // LoadSelectedTool rather than just refreshed in place.
                _techViewModel?.SetClass(letter);
                PageResetBtn.Visibility = Visibility.Visible;
                LoadSelectedTool();
            };

            ClassSelectorPanel.Children.Add(button);
        }
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
        var (confirmed, alsoMatchClass) = await ShowCopyConfirmationAsync(
            $"a copy of {sourceTool.Name}'s", _techViewModel.CurrentClass, sourcePositions, targetTool.Name, targetViewModel);

        if (!confirmed) return;

        targetViewModel.CopyTechStackFrom(_techViewModel, alsoMatchClass);

        PageResetBtn.Visibility = Visibility.Visible;
        LoadToolList();
    }

    private async void SaveAsTemplateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _techViewModel is null) return;

        var sourceTool = _tools.FirstOrDefault(t => t.Index == _selectedIndex);
        if (sourceTool is null) return;

        if (!_techViewModel.Cells.Any(c => c.IsOccupied))
        {
            await new ContentDialog
            {
                Title = "Nothing to save",
                Content = $"{sourceTool.Name} has no tech installed, so there's nothing to capture as a template.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
            return;
        }

        var nameBox = new TextBox { PlaceholderText = "e.g. \"PvE Rifle Build\"" };
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

        var template = _techViewModel.ExtractTemplate(name, sourceTool.Name, "MultiTool");
        await LoadoutTemplateService.SaveAsync(template);

        // No confirmation popup here on purpose - the whole point of moving
        // templates into a persistent list is that saving one should visibly
        // show up right away, not need a separate dialog to prove it worked.
        await RefreshTemplatesListAsync();
    }

    /// <summary>Rebuilds the persistent Templates list. Called after saving,
    /// deleting, or applying a template, and once on page load, so the list
    /// visibly reflects reality at all times rather than living behind a
    /// button the user has to remember to press.</summary>
    private async Task RefreshTemplatesListAsync()
    {
        var templates = (await LoadoutTemplateService.LoadAllAsync())
            .Where(t => t.SourceKind == "MultiTool")
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
        var (confirmed, alsoMatchClass) = await ShowCopyConfirmationAsync(
            $"the \"{template.Name}\" template", template.SourceClass, sourcePositions, targetTool.Name, _techViewModel);

        if (!confirmed) return;

        _techViewModel.ApplyTemplate(template, alsoMatchClass);
        PageResetBtn.Visibility = Visibility.Visible;

        // A class change (alsoMatchClass) can change this tool's real
        // Technology slot total (MultiToolCapacity), so the grid gets fully
        // rebuilt via LoadSelectedTool rather than just refreshed in place.
        LoadSelectedTool();
    }

    /// <summary>Shared by CopyTechStackBtn_Click and ApplyTemplateAsync - shows
    /// the same analysis (new slots needed, class-differs opt-in checkbox) and
    /// confirmation dialog regardless of whether the source is a live tool or a
    /// saved template. Never assumes a class change - always an explicit
    /// checkbox the user has to tick.</summary>
    private async Task<(bool Confirmed, bool AlsoMatchClass)> ShowCopyConfirmationAsync(
        string sourceLabel, string? sourceClass, IEnumerable<(int X, int Y)> sourcePositions,
        string targetLabel, InventoryGridViewModel targetViewModel)
    {
        var sourcePositionSet = sourcePositions.ToHashSet();
        var targetUnlocked = targetViewModel.Cells.Where(c => c.State != InventorySlotState.Locked).Select(c => (c.X, c.Y)).ToHashSet();
        int newSlotsNeeded = sourcePositionSet.Except(targetUnlocked).Count();

        bool classDiffers = !string.Equals(sourceClass, targetViewModel.CurrentClass, StringComparison.OrdinalIgnoreCase);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = $"This replaces {targetLabel}'s entire tech loadout with {sourceLabel}."
        });

        if (newSlotsNeeded > 0)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 160, 60)),
                Text = $"\u26A0 {newSlotsNeeded} slot(s) will also be unlocked on {targetLabel} to fit this stack."
            });
        }

        CheckBox? matchClassBox = null;
        if (classDiffers)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 160, 60)),
                Text = $"\u26A0 Class differs: {targetLabel} is currently {targetViewModel.CurrentClass ?? "unknown"}, source is {sourceClass ?? "unknown"}."
            });

            matchClassBox = new CheckBox { Content = $"Also change {targetLabel}'s class to {sourceClass} to match" };
            panel.Children.Add(matchClassBox);
        }

        var dialog = new ContentDialog
        {
            Title = "Confirm tech stack copy",
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
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}