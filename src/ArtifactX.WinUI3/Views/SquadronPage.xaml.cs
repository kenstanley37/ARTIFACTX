using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using libMBIN.NMS.GameComponents;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Windows.UI;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Squadron Pilots (the 4-slot in-game "Squadron Pilots" screen) - FIRST
/// PASS (2026-08-09). See NmsSquadronPaths for the full field map and what
/// is/isn't confirmed.
/// </summary>
public sealed partial class SquadronPage : Page
{
    private int _selectedIndex = -1;
    private bool _suppressFieldChangeEvent;

    public SquadronPage()
    {
        InitializeComponent();

        foreach (var option in NmsSquadronPaths.NpcRaceOptions)
            RaceBox.Items.Add(new ComboBoxItem { Content = option.DisplayName, Tag = option.Filename });

        foreach (var value in Enum.GetNames<GcInventoryClass.InventoryClassEnum>())
            RankBox.Items.Add(new ComboBoxItem { Content = value });

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;

        LoadPilotList();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadPilotList);

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
    }

    private void LoadPilotList()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            _selectedIndex = -1;
            PilotSelectorPanel.Children.Clear();
            ClearFields();
            return;
        }

        if (SaveSessionManager.GetValue(NmsSquadronPaths.SquadronArrayPath) is not JArray array)
            return;

        if (_selectedIndex < 0 || _selectedIndex >= array.Count)
            _selectedIndex = array.Count > 0 ? 0 : -1;

        BuildSelectorStrip(array.Count);
        LoadSelectedPilot();
    }

    private void BuildSelectorStrip(int slotCount)
    {
        PilotSelectorPanel.Children.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            bool isSelected = i == _selectedIndex;
            bool unlocked = SaveSessionManager.GetValue(NmsSquadronPaths.UnlockedPath(i))?.Value<bool>() ?? true;
            string label = unlocked ? $"Slot {i + 1}" : $"Slot {i + 1} (Locked)";

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = label,
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

            int index = i;
            button.Click += (_, _) =>
            {
                _selectedIndex = index;
                BuildSelectorStrip(slotCount);
                LoadSelectedPilot();
            };

            PilotSelectorPanel.Children.Add(button);
        }
    }

    private void ClearFields()
    {
        UnlockedCheckBox.IsChecked = false;
        RaceBox.SelectedIndex = -1;
        RankBox.SelectedIndex = -1;
        NpcSeedEditBox.Text = "";
        ShipFilenameTxt.Text = "";
        ShipSeedEditBox.Text = "";
        TraitsSeedEditBox.Text = "";
    }

    private void LoadSelectedPilot()
    {
        if (_selectedIndex < 0)
        {
            ClearFields();
            return;
        }

        _suppressFieldChangeEvent = true;

        UnlockedCheckBox.IsChecked = SaveSessionManager.GetValue(NmsSquadronPaths.UnlockedPath(_selectedIndex))?.Value<bool>() ?? true;

        string npcFilename = SaveSessionManager.GetValue(NmsSquadronPaths.NpcFilenamePath(_selectedIndex))?.Value<string>() ?? "";
        RaceBox.SelectedItem = RaceBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, npcFilename, StringComparison.OrdinalIgnoreCase));

        string rank = SaveSessionManager.GetValue(NmsSquadronPaths.PilotRankPath(_selectedIndex))?.Value<string>() ?? "";
        RankBox.SelectedItem = RankBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, rank, StringComparison.OrdinalIgnoreCase));

        NpcSeedEditBox.Text = SaveSessionManager.GetValue(NmsSquadronPaths.NpcSeedPath(_selectedIndex))?.Value<string>() ?? "";
        ShipFilenameTxt.Text = SaveSessionManager.GetValue(NmsSquadronPaths.ShipFilenamePath(_selectedIndex))?.Value<string>() ?? "";
        ShipSeedEditBox.Text = SaveSessionManager.GetValue(NmsSquadronPaths.ShipSeedPath(_selectedIndex))?.Value<string>() ?? "";
        TraitsSeedEditBox.Text = SaveSessionManager.GetValue(NmsSquadronPaths.TraitsSeedPath(_selectedIndex))?.Value<string>() ?? "";

        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsSquadronPaths.PilotPath(_selectedIndex))
            ? Visibility.Visible : Visibility.Collapsed;

        _suppressFieldChangeEvent = false;
    }

    private void UnlockedCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressFieldChangeEvent || _selectedIndex < 0) return;

        SaveSessionManager.StageEdit(UnlockedCheckBox.IsChecked ?? true, NmsSquadronPaths.UnlockedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
        BuildSelectorStrip(PilotSelectorPanel.Children.Count);
    }

    private void RaceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldChangeEvent || _selectedIndex < 0) return;

        string newValue = (RaceBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var path = NmsSquadronPaths.NpcFilenamePath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void RankBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldChangeEvent || _selectedIndex < 0) return;

        string newValue = (RankBox.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        var path = NmsSquadronPaths.PilotRankPath(_selectedIndex);
        string current = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (newValue == current) return;

        SaveSessionManager.StageEdit(newValue, path);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void NpcSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NpcSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsSquadronPaths.NpcSeedPath(_selectedIndex))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsSquadronPaths.NpcSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenerateNpcSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        NpcSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsSquadronPaths.NpcSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void ShipSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = ShipSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsSquadronPaths.ShipSeedPath(_selectedIndex))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsSquadronPaths.ShipSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenerateShipSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        ShipSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsSquadronPaths.ShipSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void TraitsSeedEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = TraitsSeedEditBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newSeed)) return;

        string? currentSeed = SaveSessionManager.GetValue(NmsSquadronPaths.TraitsSeedPath(_selectedIndex))?.Value<string>();
        if (newSeed == currentSeed) return;

        SaveSessionManager.StageEdit(newSeed, NmsSquadronPaths.TraitsSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void GenerateTraitsSeedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        string newSeed = NmsSeedGenerator.GenerateRandom();
        TraitsSeedEditBox.Text = newSeed;
        SaveSessionManager.StageEdit(newSeed, NmsSquadronPaths.TraitsSeedPath(_selectedIndex));
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0) return;

        SaveSessionManager.RevertEditsUnder(NmsSquadronPaths.PilotPath(_selectedIndex));
        LoadSelectedPilot();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
