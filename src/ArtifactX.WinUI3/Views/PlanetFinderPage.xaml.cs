using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Views;

/// <summary>Planet Finder and the glyph decoder (PlanetFinderViewModel), following the game's saves.</summary>
public sealed partial class PlanetFinderPage : Page
{
    public PlanetFinderViewModel ViewModel { get; } = new();

    public PlanetFinderPage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        PlanetDetailsService.Changed += OnRecordsChanged;
        PlanetDetailsService.Captured += OnRecordsChanged;
        PlanetWaterService.Changed += OnRecordsChanged;
        CreatureListService.Changed += OnRecordsChanged;
        SystemNotesService.Changed += OnSystemChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
            PlanetDetailsService.Changed -= OnRecordsChanged;
            PlanetDetailsService.Captured -= OnRecordsChanged;
            PlanetWaterService.Changed -= OnRecordsChanged;
            CreatureListService.Changed -= OnRecordsChanged;
            SystemNotesService.Changed -= OnSystemChanged;
            ViewModel.Detach();
        };
        if (SaveSessionManager.IsSaveLoaded) _ = ViewModel.LoadAsync();
    }

    public Visibility IsCollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadAsync());

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        ViewModel.Show(snapshot);
    });

    private void OnRecordsChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(ViewModel.Refresh);
    private void OnSystemChanged(ulong system) => DispatcherQueue.TryEnqueue(ViewModel.Refresh);

    private void ClearFilters_Click(object sender, RoutedEventArgs e) => ViewModel.ClearFilters();

    private void Results_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PlanetRowViewModel row) Frame.Navigate(typeof(ExplorePages.PlanetDetailsPage), row.Planet);
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e) =>
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX planets",
            new[] { "Planet", "Location", "Portal code", "Discovered", "Distance", "Your notes" },
            ViewModel.Results.Select(r => new[] { r.NameLabel, r.LocationLabel, r.PortalCode, r.DiscoveredLabel, r.DistanceLabel, r.NotesLabel }));
}
