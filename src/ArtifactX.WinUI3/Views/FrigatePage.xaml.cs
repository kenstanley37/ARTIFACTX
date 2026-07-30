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

        LoadFrigateList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadFrigateList);

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

    private void LoadSelectedFrigate()
    {
        if (_selectedIndex < 0)
        {
            ClearFields();
            return;
        }

        _suppressFieldChangeEvent = true;

        NameEditBox.Text = SaveSessionManager.GetValue(NmsFrigatePaths.CustomNamePath(_selectedIndex))?.Value<string>() ?? "";

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

        BuildTraitsPanel(_selectedIndex);

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

    /// <summary>Read-only for v1 - no trait id/description catalog exists
    /// yet (Settlement Perks started the same way before its own catalog
    /// phase was added). "^" is an empty slot.</summary>
    private void BuildTraitsPanel(int frigateIndex)
    {
        TraitsPanel.Children.Clear();

        var path = NmsFrigatePaths.TraitIDsPath(frigateIndex);
        if (SaveSessionManager.GetValue(path) is not JArray traits) return;

        for (int i = 0; i < traits.Count; i++)
        {
            string raw = traits[i]?.Value<string>() ?? "";
            string stripped = raw.TrimStart('^');

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(FieldLabel($"Trait {i + 1}"));
            row.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(stripped) ? "(empty)" : stripped,
                Opacity = string.IsNullOrEmpty(stripped) ? 0.5 : 1.0,
                VerticalAlignment = VerticalAlignment.Center
            });
            TraitsPanel.Children.Add(row);
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
