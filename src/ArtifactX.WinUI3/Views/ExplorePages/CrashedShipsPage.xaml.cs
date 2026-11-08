using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ArtifactX.WinUI3.Views.ExplorePages;

public sealed partial class CrashedShipsPage : Page
{
    public CrashedShipsPageViewModel ViewModel { get; } = new();

    public CrashedShipsPage()
    {
        InitializeComponent();
        FishCatchRecorder.SnapshotSeen += OnLiveSnapshot;
        Unloaded += (_, _) => FishCatchRecorder.SnapshotSeen -= OnLiveSnapshot;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ulong? address = e.Parameter as ulong?;
        _ = ViewModel.InitializeAsync(address);
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _ = ViewModel.InitializeAsync();
            ViewModel.RefreshMarkers();
        });
    }
}
