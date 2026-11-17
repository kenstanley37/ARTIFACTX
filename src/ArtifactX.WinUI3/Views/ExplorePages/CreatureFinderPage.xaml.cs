using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>The Creature Finder (CreatureFinderViewModel), following the game's saves.</summary>
public sealed partial class CreatureFinderPage : Page
{
    public CreatureFinderViewModel ViewModel { get; } = new();

    public CreatureFinderPage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
        };
        if (SaveSessionManager.IsSaveLoaded) _ = ViewModel.LoadAsync();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadAsync());

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        _ = ViewModel.ShowAsync(snapshot);
    });

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => ViewModel.ClearFilters();

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CreatureFinderRowViewModel row) ViewModel.TogglePin(row);
    }

    private void Results_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CreatureFinderRowViewModel row) Frame.Navigate(typeof(PlanetDetailsPage), row.Planet);
    }
}
