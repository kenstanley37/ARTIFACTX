using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using libMBIN.NMS.GameComponents;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Owned Settlements - FIRST PASS (2026-07-29), field names inferred by
/// exact positional cross-reference against libMBIN's GcSettlementState
/// (see NmsSettlementPaths for the full map), not yet confirmed in-game the
/// way Companions fields were. A settlement slot lives in a much bigger array
/// (GQA, 100 entries in the one real save checked) shared with OTHER real
/// players' settlements the save has encountered - occupancy here means
/// "has a name AND its Owner matches the local account," not just "has a
/// name" (see NmsSettlementPaths.LocalPlayerOnlineIdPath).
/// </summary>
public sealed partial class SettlementPage : Page
{
    private sealed record SettlementEntry(int Index, string SelectorLabel);

    private int _selectedIndex = -1;
    private List<SettlementEntry> _settlements = new();
    private bool _suppressStatChangeEvent;

    // Perk id -> catalog data, loaded once per page lifetime from the
    // SettlementPerks category - see CatalogService.GetSettlementPerksAsync.
    private Dictionary<string, (string DisplayName, string Description, string Category)>? _settlementPerks;

    public SettlementPage()
    {
        InitializeComponent();

        foreach (var value in Enum.GetNames<GcAlienRace.AlienRaceEnum>())
            RaceBox.Items.Add(new ComboBoxItem { Content = value });

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadSettlementList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadSettlementList);

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

    private void LoadSettlementList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _settlements = new();
            _selectedIndex = -1;
            SettlementSelectorPanel.Children.Clear();
            ClearFields();
            return;
        }

        if (SaveSessionManager.GetValue(NmsSettlementPaths.SettlementArrayPath) is not JArray array)
            return;

        string localOnlineId = SaveSessionManager.GetValue(NmsSettlementPaths.LocalPlayerOnlineIdPath)?.Value<string>() ?? "";

        var settlements = new List<SettlementEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            string ownerOnlineId = array[i]?["3?K"]?["K7E"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(localOnlineId) || ownerOnlineId != localOnlineId) continue;

            settlements.Add(new SettlementEntry(i, name));
        }

        _settlements = settlements;

        if (_selectedIndex < 0 || _settlements.All(s => s.Index != _selectedIndex))
            _selectedIndex = _settlements.FirstOrDefault()?.Index ?? -1;

        BuildSelectorStrip();
        LoadSelectedSettlement();
    }

    private void BuildSelectorStrip()
    {
        SettlementSelectorPanel.Children.Clear();

        foreach (var settlement in _settlements)
        {
            bool isSelected = settlement.Index == _selectedIndex;

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = settlement.SelectorLabel,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal
                },
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
                _selectedIndex = settlement.Index;
                BuildSelectorStrip();
                LoadSelectedSettlement();
            };

            SettlementSelectorPanel.Children.Add(button);
        }
    }

    private void ClearFields()
    {
        NameEditBox.Text = "";
        OwnerTxt.Text = "";
        RaceBox.SelectedIndex = -1;
        PopulationBox.Value = double.NaN;
        Stat0Box.Value = double.NaN;
        Stat1Box.Value = double.NaN;
        Stat2Box.Value = double.NaN;
        Stat3Box.Value = double.NaN;
        Stat4Box.Value = double.NaN;
        Stat5Box.Value = double.NaN;
        Stat6Box.Value = double.NaN;
        Stat7Box.Value = double.NaN;
        PerksPanel.Children.Clear();
    }

    private async void LoadSelectedSettlement()
    {
        if (_selectedIndex < 0)
        {
            ClearFields();
            return;
        }

        _suppressStatChangeEvent = true;

        NameEditBox.Text = SaveSessionManager.GetValue(NmsSettlementPaths.NamePath(_selectedIndex))?.Value<string>() ?? "";

        string username = SaveSessionManager.GetValue(NmsSettlementPaths.OwnerUsernamePath(_selectedIndex))?.Value<string>() ?? "";
        string platform = SaveSessionManager.GetValue(NmsSettlementPaths.OwnerPlatformPath(_selectedIndex))?.Value<string>() ?? "";
        OwnerTxt.Text = string.IsNullOrEmpty(username) ? "" : $"{username} ({platform})";

        string race = SaveSessionManager.GetValue(NmsSettlementPaths.RacePath(_selectedIndex))?.Value<string>() ?? "";
        RaceBox.SelectedItem = RaceBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, race, StringComparison.OrdinalIgnoreCase));

        PopulationBox.Value = SaveSessionManager.GetValue(NmsSettlementPaths.PopulationPath(_selectedIndex))?.Value<double>() ?? 0;

        var stats = SaveSessionManager.GetValue(NmsSettlementPaths.StatsPath(_selectedIndex)) as JArray;
        Stat0Box.Value = stats?.Count > 0 ? stats[0].Value<double>() : double.NaN;
        Stat1Box.Value = stats?.Count > 1 ? stats[1].Value<double>() : double.NaN;
        Stat2Box.Value = stats?.Count > 2 ? stats[2].Value<double>() : double.NaN;
        Stat3Box.Value = stats?.Count > 3 ? stats[3].Value<double>() : double.NaN;
        Stat4Box.Value = stats?.Count > 4 ? stats[4].Value<double>() : double.NaN;
        Stat5Box.Value = stats?.Count > 5 ? stats[5].Value<double>() : double.NaN;
        Stat6Box.Value = stats?.Count > 6 ? stats[6].Value<double>() : double.NaN;
        Stat7Box.Value = stats?.Count > 7 ? stats[7].Value<double>() : double.NaN;

        _settlementPerks ??= await CatalogService.GetSettlementPerksAsync();
        BuildPerksPanel(_selectedIndex, _settlementPerks);

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsSettlementPaths.SettlementPath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

        _suppressStatChangeEvent = false;
    }

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        Width = 90,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.8,
        FontSize = 12
    };

    private static readonly Random PerkRandom = new();

    /// <summary>Appends ONE new empty ("^") entry to the Perks array, rather
    /// than filling/swapping an existing one - added 2026-07-30 after
    /// discovering the array isn't actually capped at 8 the way an earlier
    /// (wrong) analysis claimed: real gameplay grew a real settlement to 10
    /// active features with no save editing at all. CONFIRMED 2026-07-30
    /// (same day): using this button, a real settlement was grown to 19
    /// entries and tested in-game - all loaded fine across 4 paginated panel
    /// pages, no crash, no cap found. Treat the array as effectively
    /// unbounded - follow with "Fill Empty Slots" or pick a perk for the new
    /// row directly.</summary>
    private void AddFeatureSlotBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        var path = NmsSettlementPaths.PerksPath(_selectedIndex);
        if (SaveSessionManager.GetValue(path) is not JArray currentArray) return;

        var updated = new JArray(currentArray.Select(v => v.DeepClone()));
        updated.Add("^");

        SaveSessionManager.StageEdit(updated, path);
        PageResetBtn.Visibility = Visibility.Visible;
        BuildPerksPanel(_selectedIndex, _settlementPerks ?? new());
    }

    private static readonly SolidColorBrush NegativePerkBrush = new(Color.FromArgb(255, 0xE0, 0x4C, 0x3D));
    private static readonly SolidColorBrush PositivePerkBrush = new(Color.FromArgb(255, 0x3D, 0xC4, 0x77));
    private static readonly SolidColorBrush NeutralPerkBrush = new(Color.FromArgb(255, 0x8A, 0x96, 0xA8));

    /// <summary>Red for a Negative-flagged perk, green for Positive,
    /// neutral gray for "(None)"/unrecognized - Category always starts
    /// with "Negative"/"Positive" (see CatalogBuildService's Phase 1.8;
    /// the other flags like Blessing/Job/Starter/Proc are appended after,
    /// never first) so a simple StartsWith check is enough.</summary>
    private static SolidColorBrush PerkSignBrush(string? category) => category switch
    {
        null or "" => NeutralPerkBrush,
        _ when category.StartsWith("Negative", StringComparison.OrdinalIgnoreCase) => NegativePerkBrush,
        _ when category.StartsWith("Positive", StringComparison.OrdinalIgnoreCase) => PositivePerkBrush,
        _ => NeutralPerkBrush
    };

    private static readonly SolidColorBrush CardBorderBrush = new(Color.FromArgb(255, 90, 98, 112));
    private static readonly SolidColorBrush CardBackgroundBrush = new(Color.FromArgb(18, 255, 255, 255));
    private const int PerksPerCard = 6;

    /// <summary>One dropdown per Perks (OEf) array slot, each offering
    /// "(None)" plus every cataloged perk - see NmsSettlementPaths.
    /// PerksPath and this page's XAML description for the full picture.
    /// Matches the slot's CURRENT value by its base id (stripping the "^"
    /// prefix and any "#NNNNN" per-instance roll suffix some perks carry).
    /// Picking a genuinely different perk writes a plain "^"+id with no
    /// roll suffix - confirmed 2026-07-30 that Proc-flagged perks work fine
    /// without one (the game generates its own flavor text on the fly).
    /// Each option is colored red/green
    /// by its Negative/Positive flag, and a small swatch next to the
    /// dropdown mirrors whichever perk is currently selected, since a
    /// WinUI3 ComboBox's own closed-state header doesn't reliably repaint
    /// itself in the selected ComboBoxItem's Foreground.
    ///
    /// Rows are grouped into PerksPerCard-sized card Borders rather than one
    /// flat list - added 2026-07-30 once the array's real length was
    /// confirmed unbounded (tested to 19 with no cap found), so there's no
    /// longer a sane fixed page width to design a single list around. Cards
    /// are dropped into PerksPanel, a WrapPanel in XAML, which flows them
    /// left-to-right and wraps to new rows on its own as the window resizes
    /// - no explicit breakpoints needed here.</summary>
    private void BuildPerksPanel(int settlementIndex, Dictionary<string, (string DisplayName, string Description, string Category)> perksCatalog)
    {
        PerksPanel.Children.Clear();

        var path = NmsSettlementPaths.PerksPath(settlementIndex);
        if (SaveSessionManager.GetValue(path) is not JArray perks) return;

        var sortedPerks = perksCatalog
            .OrderBy(kv => kv.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StackPanel? cardStack = null;

        for (int i = 0; i < perks.Count; i++)
        {
            if (i % PerksPerCard == 0)
            {
                cardStack = new StackPanel { Spacing = 10 };
                var card = new Border
                {
                    BorderBrush = CardBorderBrush,
                    Background = CardBackgroundBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14),
                    Child = cardStack
                };
                PerksPanel.Children.Add(card);
            }

            int index = i; // capture for the closure below
            string raw = perks[i]?.Value<string>() ?? "";
            string stripped = raw.TrimStart('^');
            int hashIdx = stripped.IndexOf('#');
            string baseId = hashIdx >= 0 ? stripped[..hashIdx] : stripped;

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(FieldLabel($"Buff {i + 1}"));

            var swatch = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                VerticalAlignment = VerticalAlignment.Center
            };

            var box = new ComboBox { Width = 340 };
            var noneItem = new ComboBoxItem { Content = "(None)", Tag = "", Foreground = NeutralPerkBrush };
            box.Items.Add(noneItem);
            foreach (var kv in sortedPerks)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{kv.Value.DisplayName} ({kv.Value.Category})",
                    Tag = kv.Key,
                    Foreground = PerkSignBrush(kv.Value.Category)
                };
                ToolTipService.SetToolTip(item, kv.Value.Description);
                box.Items.Add(item);
            }

            box.SelectedItem = string.IsNullOrEmpty(baseId)
                ? noneItem
                : box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, baseId, StringComparison.OrdinalIgnoreCase)) ?? noneItem;

            swatch.Background = perksCatalog.TryGetValue(baseId, out var currentInfo) ? PerkSignBrush(currentInfo.Category) : NeutralPerkBrush;
            if (perksCatalog.TryGetValue(baseId, out currentInfo))
                ToolTipService.SetToolTip(box, currentInfo.Description);

            box.SelectionChanged += (_, _) =>
            {
                string selectedBaseId = (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                swatch.Background = perksCatalog.TryGetValue(selectedBaseId, out var selectedInfo) ? PerkSignBrush(selectedInfo.Category) : NeutralPerkBrush;

                if (_suppressStatChangeEvent) return;

                if (SaveSessionManager.GetValue(path) is not JArray currentArray || index >= currentArray.Count) return;

                string existing = currentArray[index]?.Value<string>() ?? "";
                string existingStripped = existing.TrimStart('^');
                int existingHashIdx = existingStripped.IndexOf('#');
                string existingBaseId = existingHashIdx >= 0 ? existingStripped[..existingHashIdx] : existingStripped;

                if (string.Equals(selectedBaseId, existingBaseId, StringComparison.OrdinalIgnoreCase)) return;

                string newValue = string.IsNullOrEmpty(selectedBaseId) ? "^" : "^" + selectedBaseId;

                var updated = new JArray(currentArray.Select(v => v.DeepClone()));
                updated[index] = newValue;

                SaveSessionManager.StageEdit(updated, path);
                PageResetBtn.Visibility = Visibility.Visible;
            };
            row.Children.Add(box);
            row.Children.Add(swatch);

            cardStack!.Children.Add(row);
        }
    }

    private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newName = NameEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        string currentName = SaveSessionManager.GetValue(NmsSettlementPaths.NamePath(_selectedIndex))?.Value<string>() ?? "";
        if (newName == currentName) return;

        SaveSessionManager.StageEdit(newName, NmsSettlementPaths.NamePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
        LoadSettlementList();
    }

    private void RaceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0) return;

        string newValue = (RaceBox.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        var path = NmsSettlementPaths.RacePath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PopulationBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(args.NewValue)) return;
        SaveSessionManager.StageEdit((int)args.NewValue, NmsSettlementPaths.PopulationPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PopulationBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressStatChangeEvent || _selectedIndex < 0 || double.IsNaN(PopulationBox.Value)) return;
        SaveSessionManager.StageEdit((int)PopulationBox.Value, NmsSettlementPaths.PopulationPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Stages one entry of the whole 8-slot Stats array - rebuilds
    /// and stages the WHOLE array at once, same reasoning as every other
    /// array edit in the app (a deeper leaf-only stage isn't seen by
    /// SaveSessionManager's staged-edit lookup).</summary>
    private void SetStat(int statIndex, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;

        var path = NmsSettlementPaths.StatsPath(_selectedIndex);
        if (SaveSessionManager.GetValue(path) is not JArray stats || stats.Count != 8) return;

        var updated = new JArray(stats.Select(s => s.DeepClone()));
        updated[statIndex] = (int)newValue;

        SaveSessionManager.StageEdit(updated, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void Stat0Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(0, args.NewValue); }
    private void Stat1Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(1, args.NewValue); }
    private void Stat2Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(2, args.NewValue); }
    private void Stat3Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(3, args.NewValue); }
    private void Stat4Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(4, args.NewValue); }
    private void Stat5Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(5, args.NewValue); }
    private void Stat6Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(6, args.NewValue); }
    private void Stat7Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressStatChangeEvent) SetStat(7, args.NewValue); }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.RevertEditsUnder(NmsSettlementPaths.SettlementPath(_selectedIndex));
        LoadSelectedSettlement();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
