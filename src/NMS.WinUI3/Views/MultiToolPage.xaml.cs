using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using NMS.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace NMS.WinUI3.Views;

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

    public MultiToolPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;

        LoadToolList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadToolList);

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

        // Kgt looked like the safer target for editing the active tool, but a
        // real save/load test proved it wrong: edits written there (an item add
        // AND a class change) never showed up in-game. Kgt is a derived/cached
        // mirror the game regenerates FROM SuJ[i].OsQ, not the other way around -
        // so SuJ[i].OsQ is the one true source regardless of active status.
        _techViewModel = new InventoryGridViewModel(
            NmsInventoryContainer.MultiToolTechnologyPath(_selectedIndex),
            MultiToolCapacity.TechnologyColumns,
            MultiToolCapacity.TechnologyRows);

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

    // Replaces NMS.Core.GameKnowledge/NMS.Almanac's curated list entirely - this
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
                _techViewModel?.SetClass(letter);
                TechGrid.Refresh();
                PageResetBtn.Visibility = Visibility.Visible;
                BuildClassSelector();
                UpdateClassBadge();
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

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _techViewModel?.Revert();
        TechGrid.Refresh();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}