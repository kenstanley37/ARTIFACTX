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
/// way Pets fields were. A settlement slot lives in a much bigger array
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

    public SettlementPage()
    {
        InitializeComponent();

        foreach (var value in Enum.GetNames<GcAlienRace.AlienRaceEnum>())
            RaceBox.Items.Add(new ComboBoxItem { Content = value });

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;

        LoadSettlementList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadSettlementList);

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
    }

    private void LoadSelectedSettlement()
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

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsSettlementPaths.SettlementPath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

        _suppressStatChangeEvent = false;
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
