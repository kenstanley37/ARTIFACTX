using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.Views.ExplorePages;

public sealed partial class GalaxyTransferPage : Page
{
    public GalaxyTransferViewModel ViewModel { get; } = new();

    public GalaxyTransferPage()
    {
        InitializeComponent();
        FishCatchRecorder.SnapshotSeen += OnLiveSnapshot;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        SaveSessionManager.ExternalChangeDetected += OnExternalChangeDetected;

        Unloaded += (_, _) =>
        {
            FishCatchRecorder.SnapshotSeen -= OnLiveSnapshot;
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            SaveSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
            SaveSessionManager.ExternalChangeDetected -= OnExternalChangeDetected;
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadAsync();
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot) =>
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCurrentGalaxy(snapshot));

    private void OnSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadAsync());

    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCurrentGalaxy());

    private void OnExternalChangeDetected(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => ViewModel.UpdateCurrentGalaxy());

    // Presence Filters
    private void FilterAll_Click(object sender, RoutedEventArgs e) => ViewModel.SetPresenceFilter("All");
    private void FilterUnvisited_Click(object sender, RoutedEventArgs e) => ViewModel.SetPresenceFilter("Unvisited");
    private void FilterVisited_Click(object sender, RoutedEventArgs e) => ViewModel.SetPresenceFilter("Visited");
    private void FilterBases_Click(object sender, RoutedEventArgs e) => ViewModel.SetPresenceFilter("Bases");
    private void FilterStations_Click(object sender, RoutedEventArgs e) => ViewModel.SetPresenceFilter("Stations");

    // Biome/Category Filters
    private void FilterTypeAll_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryFilter("All");
    private void FilterLush_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryFilter("Lush");
    private void FilterHarsh_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryFilter("Harsh");
    private void FilterEmpty_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryFilter("Empty");
    private void FilterNormal_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryFilter("Normal");

    private async void Transfer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGalaxy is not { } target) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Confirm Transfer to {target.Name} (Galaxy {target.Number})",
            Content = new TextBlock
            {
                Text = $"Are you sure you want to stage a transfer to {target.Name}?\n\n" +
                       $"• Destination: the space station in {ViewModel.GatewayCoordinatesText} (~{ViewModel.DistanceToCoreText} from the Galactic Core)\n" +
                       $"• Spawn: Inside the station docking bay walkway next to your starship\n" +
                       $"• Network: Station will be registered in your Teleport Network\n\n" +
                       $"Edits are staged in memory. Click Save at the top of the window to write changes when No Man's Sky is closed.",
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            },
            PrimaryButtonText = $"Transfer to {target.Name}",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.StageTransfer();
        }
    }

    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RevertTransfer();
    }

    private void CopyGlyphs_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.GatewayPortalGlyphs);
        Clipboard.SetContent(package);
    }
}
