using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>Regions (RegionsViewModel), following the game's saves and screen recording.</summary>
public sealed partial class RegionsPage : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _refresh;

    public RegionsViewModel ViewModel { get; } = new();

    public RegionsPage()
    {
        InitializeComponent();
        // Screen recording can save a dozen readings in a few seconds: refresh once they settle.
        _refresh = DispatcherQueue.CreateTimer();
        _refresh.Interval = TimeSpan.FromSeconds(1.5);
        _refresh.IsRepeating = false;
        _refresh.Tick += (_, _) => _ = ViewModel.RefreshAsync();

        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        PlanetDetailsService.Changed += OnPlanetChanged;
        PlanetDetailsService.Captured += OnPlanetChanged;
        SystemNotesService.Changed += OnSystemChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
            PlanetDetailsService.Changed -= OnPlanetChanged;
            PlanetDetailsService.Captured -= OnPlanetChanged;
            SystemNotesService.Changed -= OnSystemChanged;
            _refresh.Stop();
        };
        if (SaveSessionManager.IsSaveLoaded) _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync() => await ViewModel.ShowAsync(await LiveSaveWatcher.GetLatestAsync());

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        _ = ViewModel.ShowAsync(snapshot);
    });

    private void OnPlanetChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(Restart);
    private void OnSystemChanged(ulong system) => DispatcherQueue.TryEnqueue(Restart);

    private void Restart()
    {
        _refresh.Stop();
        _refresh.Start();
    }

    private void Body_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BodyChipViewModel { Body.Planet: { } planet })
            Frame.Navigate(typeof(ExplorePages.PlanetDetailsPage), planet);
    }
}
