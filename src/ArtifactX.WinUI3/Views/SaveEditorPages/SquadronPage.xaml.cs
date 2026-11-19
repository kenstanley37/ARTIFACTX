using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

/// <summary>
/// Squadron pilots (SquadronViewModel). See NmsSquadronPaths for the confirmed field map. The page wires the seed
/// boxes and the two 3D previews; the ViewModel holds the state and stages edits.
/// </summary>
public sealed partial class SquadronPage : Page
{
    public SquadronViewModel ViewModel { get; } = new();

    private string? _pilotKey, _shipKey;

    public SquadronPage()
    {
        InitializeComponent();
        ViewModel.PreviewChanged += ShowDetails;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnEditsChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            SaveSessionManager.PendingEditsChanged -= OnEditsChanged;
        };
        _ = ViewModel.LoadAsync();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadAsync());

    private void OnEditsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(ViewModel.RefreshState);

    private void Slot_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SquadronSlotViewModel slot) ViewModel.Select(slot.Index);
    }

    /// <summary>The seed boxes follow the selected pilot; the previews rebuild only when what they show changed.</summary>
    private void ShowDetails()
    {
        int i = ViewModel.SelectedIndex;
        NpcSeedBox.Show(i >= 0 ? NmsSquadronPaths.NpcSeedPath(i) : null);
        ShipSeedBox.Show(i >= 0 ? NmsSquadronPaths.ShipSeedPath(i) : null);
        TraitsSeedBox.Show(i >= 0 ? NmsSquadronPaths.TraitsSeedPath(i) : null);
        if (!ViewModel.ShowPreviews) return;

        string pilot = $"{ViewModel.PilotScene}|{ViewModel.PilotSeed}";
        if (pilot != _pilotKey)
        {
            _pilotKey = pilot;
            // A pilot is an NPC like a freighter captain: the seed's first child seed picks its parts (catalog format 104).
            _ = PilotPreview.ShowAsync(ViewModel.PilotScene, ViewModel.PilotSeed, childPartSeed: true, shapeOnly: true);
        }
        string ship = $"{ViewModel.ShipScene}|{ViewModel.ShipSeed}";
        if (ship != _shipKey)
        {
            _shipKey = ship;
            _ = ViewModel.RefreshShipNameAsync();
            _ = ShipPreview.ShowAsync(ViewModel.ShipScene, ViewModel.ShipSeed);
        }
    }

    private void Seed_Changed(object? sender, SeedChange change) => ViewModel.Edited();

    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel.Reset();
}
