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
/// Owned Frigates - FIRST PASS (2026-07-30), field names confirmed by exact
/// positional cross-reference against libMBIN's GcFleetFrigateSaveData AND
/// by matching all 29 real frigates in the test save against 4 screenshots
/// of the in-game Frigate List panel (see NmsFrigatePaths for the full
/// map). Unlike Settlements' GQA array, FleetFrigates (;Du) is directly and
/// solely owned by the player - no ownership filter needed.
/// </summary>
public sealed partial class FrigatePage : Page
{
    private sealed record FrigateEntry(int Index, string SelectorLabel);

    private int _selectedIndex = -1;
    private List<FrigateEntry> _frigates = new();
    private bool _suppressFieldChangeEvent;

    // Trait id -> catalog data, loaded once per page lifetime from the
    // FrigateTraits category - see CatalogService.GetFrigateTraitsAsync.
    private Dictionary<string, (string DisplayName, string Description, string Category)>? _frigateTraits;

    public FrigatePage()
    {
        InitializeComponent();

        foreach (var value in Enum.GetNames<GcFrigateClass.FrigateClassEnum>())
            FrigateClassBox.Items.Add(new ComboBoxItem { Content = value });

        foreach (var value in Enum.GetNames<GcAlienRace.AlienRaceEnum>())
            RaceBox.Items.Add(new ComboBoxItem { Content = value });

        foreach (var value in Enum.GetNames<GcInventoryClass.InventoryClassEnum>())
            InventoryClassBox.Items.Add(new ComboBoxItem { Content = value });

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadFrigateList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadFrigateList);

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

    private void LoadFrigateList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _frigates = new();
            _selectedIndex = -1;
            FrigateSelectorPanel.Children.Clear();
            ClearFields();
            return;
        }

        if (SaveSessionManager.GetValue(NmsFrigatePaths.FrigateArrayPath) is not JArray array)
            return;

        var frigates = new List<FrigateEntry>();
        for (int i = 0; i < array.Count; i++)
        {
            string name = array[i]?["fH8"]?.Value<string>() ?? "";
            string frigateClass = array[i]?["uw7"]?["uw7"]?.Value<string>() ?? "?";
            string label = string.IsNullOrEmpty(name) ? $"(Auto) {frigateClass}" : name;
            frigates.Add(new FrigateEntry(i, label));
        }

        _frigates = frigates;

        if (_selectedIndex < 0 || _frigates.All(f => f.Index != _selectedIndex))
            _selectedIndex = _frigates.FirstOrDefault()?.Index ?? -1;

        BuildSelectorStrip();
        LoadSelectedFrigate();
    }

    private void BuildSelectorStrip()
    {
        FrigateSelectorPanel.Children.Clear();

        foreach (var frigate in _frigates)
        {
            bool isSelected = frigate.Index == _selectedIndex;

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = frigate.SelectorLabel,
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
                _selectedIndex = frigate.Index;
                BuildSelectorStrip();
                LoadSelectedFrigate();
            };

            FrigateSelectorPanel.Children.Add(button);
        }
    }

    private void ClearFields()
    {
        NameEditBox.Text = "";
        FrigateClassBox.SelectedIndex = -1;
        RaceBox.SelectedIndex = -1;
        InventoryClassBox.SelectedIndex = -1;
        ModelSeedEditBox.Text = "";
        HomeSeedEditBox.Text = "";
        Stat0Box.Value = double.NaN;
        Stat1Box.Value = double.NaN;
        Stat2Box.Value = double.NaN;
        Stat3Box.Value = double.NaN;
        Stat4Box.Value = double.NaN;
        Stat5Box.Value = double.NaN;
        Stat6Box.Value = double.NaN;
        Stat7Box.Value = double.NaN;
        Stat8Box.Value = double.NaN;
        Stat9Box.Value = double.NaN;
        Stat10Box.Value = double.NaN;
        ExpeditionsBox.Value = double.NaN;
        SuccessesBox.Value = double.NaN;
        FailuresBox.Value = double.NaN;
        TimesDamagedBox.Value = double.NaN;
        RepairsMadeBox.Value = double.NaN;
        DamageTakenBox.Value = double.NaN;
        TraitsPanel.Children.Clear();
    }

    private async void LoadSelectedFrigate()
    {
        if (_selectedIndex < 0)
        {
            ClearFields();
            return;
        }

        _suppressFieldChangeEvent = true;

        NameEditBox.Text = SaveSessionManager.GetValue(NmsFrigatePaths.CustomNamePath(_selectedIndex))?.Value<string>() ?? "";
        ModelSeedEditBox.Text = SaveSessionManager.GetValue(NmsFrigatePaths.ModelSeedPath(_selectedIndex))?.Value<string>() ?? "";
        HomeSeedEditBox.Text = SaveSessionManager.GetValue(NmsFrigatePaths.HomeSeedPath(_selectedIndex))?.Value<string>() ?? "";

        string frigateClass = SaveSessionManager.GetValue(NmsFrigatePaths.FrigateClassPath(_selectedIndex))?.Value<string>() ?? "";
        FrigateClassBox.SelectedItem = FrigateClassBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, frigateClass, StringComparison.OrdinalIgnoreCase));

        string race = SaveSessionManager.GetValue(NmsFrigatePaths.RacePath(_selectedIndex))?.Value<string>() ?? "";
        RaceBox.SelectedItem = RaceBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, race, StringComparison.OrdinalIgnoreCase));

        string invClass = SaveSessionManager.GetValue(NmsFrigatePaths.InventoryClassPath(_selectedIndex))?.Value<string>() ?? "";
        InventoryClassBox.SelectedItem = InventoryClassBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, invClass, StringComparison.OrdinalIgnoreCase));

        var stats = SaveSessionManager.GetValue(NmsFrigatePaths.StatsPath(_selectedIndex)) as JArray;
        Stat0Box.Value = stats?.Count > 0 ? stats[0].Value<double>() : double.NaN;
        Stat1Box.Value = stats?.Count > 1 ? stats[1].Value<double>() : double.NaN;
        Stat2Box.Value = stats?.Count > 2 ? stats[2].Value<double>() : double.NaN;
        Stat3Box.Value = stats?.Count > 3 ? stats[3].Value<double>() : double.NaN;
        Stat4Box.Value = stats?.Count > 4 ? stats[4].Value<double>() : double.NaN;
        Stat5Box.Value = stats?.Count > 5 ? stats[5].Value<double>() : double.NaN;
        Stat6Box.Value = stats?.Count > 6 ? stats[6].Value<double>() : double.NaN;
        Stat7Box.Value = stats?.Count > 7 ? stats[7].Value<double>() : double.NaN;
        Stat8Box.Value = stats?.Count > 8 ? stats[8].Value<double>() : double.NaN;
        Stat9Box.Value = stats?.Count > 9 ? stats[9].Value<double>() : double.NaN;
        Stat10Box.Value = stats?.Count > 10 ? stats[10].Value<double>() : double.NaN;

        ExpeditionsBox.Value = SaveSessionManager.GetValue(NmsFrigatePaths.TotalNumberOfExpeditionsPath(_selectedIndex))?.Value<double>() ?? 0;
        SuccessesBox.Value = SaveSessionManager.GetValue(NmsFrigatePaths.TotalNumberOfSuccessfulEventsPath(_selectedIndex))?.Value<double>() ?? 0;
        FailuresBox.Value = SaveSessionManager.GetValue(NmsFrigatePaths.TotalNumberOfFailedEventsPath(_selectedIndex))?.Value<double>() ?? 0;
        TimesDamagedBox.Value = SaveSessionManager.GetValue(NmsFrigatePaths.NumberOfTimesDamagedPath(_selectedIndex))?.Value<double>() ?? 0;
        RepairsMadeBox.Value = SaveSessionManager.GetValue(NmsFrigatePaths.RepairsMadePath(_selectedIndex))?.Value<double>() ?? 0;
        DamageTakenBox.Value = SaveSessionManager.GetValue(NmsFrigatePaths.DamageTakenPath(_selectedIndex))?.Value<double>() ?? 0;

        _frigateTraits ??= await CatalogService.GetFrigateTraitsAsync();
        BuildTraitsPanel(_selectedIndex, _frigateTraits);

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsFrigatePaths.FrigatePath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

        _suppressFieldChangeEvent = false;
    }

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        Width = 90,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.8,
        FontSize = 12
    };

    private static readonly SolidColorBrush NegativeTraitBrush = new(Color.FromArgb(255, 0xE0, 0x4C, 0x3D));
    private static readonly SolidColorBrush PositiveTraitBrush = new(Color.FromArgb(255, 0x3D, 0xC4, 0x77));
    private static readonly SolidColorBrush NeutralTraitBrush = new(Color.FromArgb(255, 0x8A, 0x96, 0xA8));
    private static readonly SolidColorBrush CardBorderBrush = new(Color.FromArgb(255, 90, 98, 112));
    private static readonly SolidColorBrush CardBackgroundBrush = new(Color.FromArgb(18, 255, 255, 255));
    private const int TraitsPerCard = 6;

    private static SolidColorBrush TraitSignBrush(string? category) => category?.ToLowerInvariant() switch
    {
        "negative" => NegativeTraitBrush,
        "positive" => PositiveTraitBrush,
        _ => NeutralTraitBrush
    };

    /// <summary>One dropdown per Traits (Mjm) array slot, each offering
    /// "(None)" plus every cataloged trait, formatted with its exact stat
    /// bonus (e.g. "Deep Scout Prototype (+15 Combat)") - see
    /// NmsFrigatePaths.TraitIDsPath. Matches the slot's CURRENT value by its
    /// base id (stripping the "^" prefix). Rows are grouped into
    /// TraitsPerCard-sized card Borders inside a WrapPanel, same pattern as
    /// Settlement's BuildPerksPanel - the array is declared unbounded in
    /// libMBIN even though every sampled frigate had exactly 5.</summary>
    private void BuildTraitsPanel(int frigateIndex, Dictionary<string, (string DisplayName, string Description, string Category)> traitsCatalog)
    {
        TraitsPanel.Children.Clear();

        var path = NmsFrigatePaths.TraitIDsPath(frigateIndex);
        if (SaveSessionManager.GetValue(path) is not JArray traits) return;

        var sortedTraits = traitsCatalog
            .OrderBy(kv => kv.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StackPanel? cardStack = null;

        for (int i = 0; i < traits.Count; i++)
        {
            if (i % TraitsPerCard == 0)
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
                TraitsPanel.Children.Add(card);
            }

            int index = i; // capture for the closure below
            string raw = traits[i]?.Value<string>() ?? "";
            string baseId = raw.TrimStart('^');

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(FieldLabel($"Trait {i + 1}"));

            var swatch = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                VerticalAlignment = VerticalAlignment.Center
            };

            var box = new ComboBox { Width = 340 };
            var noneItem = new ComboBoxItem { Content = "(None)", Tag = "", Foreground = NeutralTraitBrush };
            box.Items.Add(noneItem);
            foreach (var kv in sortedTraits)
            {
                var item = new ComboBoxItem
                {
                    Content = kv.Value.Description,
                    Tag = kv.Key,
                    Foreground = TraitSignBrush(kv.Value.Category)
                };
                box.Items.Add(item);
            }

            box.SelectedItem = string.IsNullOrEmpty(baseId)
                ? noneItem
                : box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, baseId, StringComparison.OrdinalIgnoreCase)) ?? noneItem;

            swatch.Background = traitsCatalog.TryGetValue(baseId, out var currentInfo) ? TraitSignBrush(currentInfo.Category) : NeutralTraitBrush;

            box.SelectionChanged += (_, _) =>
            {
                string selectedBaseId = (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                swatch.Background = traitsCatalog.TryGetValue(selectedBaseId, out var selectedInfo) ? TraitSignBrush(selectedInfo.Category) : NeutralTraitBrush;

                if (_suppressFieldChangeEvent) return;

                if (SaveSessionManager.GetValue(path) is not JArray currentArray || index >= currentArray.Count) return;

                string existingBaseId = (currentArray[index]?.Value<string>() ?? "").TrimStart('^');
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
        string currentName = SaveSessionManager.GetValue(NmsFrigatePaths.CustomNamePath(_selectedIndex))?.Value<string>() ?? "";
        if (newName == currentName) return;

        SaveSessionManager.StageEdit(newName, NmsFrigatePaths.CustomNamePath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
        LoadFrigateList();
    }

    private void FrigateClassBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldChangeEvent || _selectedIndex < 0) return;

        string newValue = (FrigateClassBox.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        var path = NmsFrigatePaths.FrigateClassPath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
        LoadFrigateList();
    }

    private void RaceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldChangeEvent || _selectedIndex < 0) return;

        string newValue = (RaceBox.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        var path = NmsFrigatePaths.RacePath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ModelSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = ModelSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsFrigatePaths.ModelSeedPath(_selectedIndex))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsFrigatePaths.ModelSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Rerolls the hull to a brand new random seed - see
    /// NmsSeedGenerator; same plain-reroll approach FreighterPage/ShipsPage
    /// already use for their own Model Seed buttons.</summary>
    private void GenerateModelSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        ModelSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsFrigatePaths.ModelSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void HomeSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = HomeSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsFrigatePaths.HomeSeedPath(_selectedIndex))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsFrigatePaths.HomeSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenerateHomeSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        HomeSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsFrigatePaths.HomeSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void InventoryClassBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldChangeEvent || _selectedIndex < 0) return;

        string newValue = (InventoryClassBox.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        var path = NmsFrigatePaths.InventoryClassPath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    /// <summary>Stages one entry of the whole 11-slot Stats array - rebuilds
    /// and stages the WHOLE array at once, same reasoning as Settlement's
    /// SetStat.</summary>
    private void SetStat(int statIndex, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;

        var path = NmsFrigatePaths.StatsPath(_selectedIndex);
        if (SaveSessionManager.GetValue(path) is not JArray stats || stats.Count != 11) return;

        var updated = new JArray(stats.Select(s => s.DeepClone()));
        updated[statIndex] = (int)newValue;

        SaveSessionManager.StageEdit(updated, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void Stat0Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(0, args.NewValue); }
    private void Stat1Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(1, args.NewValue); }
    private void Stat2Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(2, args.NewValue); }
    private void Stat3Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(3, args.NewValue); }
    private void Stat4Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(4, args.NewValue); }
    private void Stat5Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(5, args.NewValue); }
    private void Stat6Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(6, args.NewValue); }
    private void Stat7Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(7, args.NewValue); }
    private void Stat8Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(8, args.NewValue); }
    private void Stat9Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(9, args.NewValue); }
    private void Stat10Box_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetStat(10, args.NewValue); }

    private void SetSingleIntField(string[] path, double newValue)
    {
        if (_selectedIndex < 0 || double.IsNaN(newValue)) return;
        SaveSessionManager.StageEdit((int)newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ExpeditionsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetSingleIntField(NmsFrigatePaths.TotalNumberOfExpeditionsPath(_selectedIndex), args.NewValue); }
    private void SuccessesBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetSingleIntField(NmsFrigatePaths.TotalNumberOfSuccessfulEventsPath(_selectedIndex), args.NewValue); }
    private void FailuresBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetSingleIntField(NmsFrigatePaths.TotalNumberOfFailedEventsPath(_selectedIndex), args.NewValue); }
    private void TimesDamagedBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetSingleIntField(NmsFrigatePaths.NumberOfTimesDamagedPath(_selectedIndex), args.NewValue); }
    private void RepairsMadeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetSingleIntField(NmsFrigatePaths.RepairsMadePath(_selectedIndex), args.NewValue); }
    private void DamageTakenBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppressFieldChangeEvent) SetSingleIntField(NmsFrigatePaths.DamageTakenPath(_selectedIndex), args.NewValue); }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.RevertEditsUnder(NmsFrigatePaths.FrigatePath(_selectedIndex));
        LoadSelectedFrigate();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
