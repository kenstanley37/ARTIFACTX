using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>Current System (CurrentSystemViewModel), following the game's saves
/// (FishCatchRecorder reads each one for the whole app).</summary>
public sealed partial class CurrentSystemPage : Page
{
    private Storyboard? _bannerStoryboard;

    public CurrentSystemViewModel ViewModel { get; } = new();

    public CurrentSystemPage()
    {
        InitializeComponent();
        ViewModel.SystemArrived += ShowSystemChange;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        PlanetDetailsService.Captured += OnPlanetChanged;
        PlanetDetailsService.Changed += OnPlanetChanged;
        PlanetWaterService.Changed += OnPlanetChanged;
        FindsService.Changed += OnFindsChanged;
        SystemNotesService.Changed += OnSystemChanged;
        SystemPageReader.Captured += OnSystemChanged;
        BodyKindsService.Captured += OnSystemChanged;
        UnmatchedBodiesService.Changed += OnSystemChanged;
        SystemMarketService.Changed += OnMarketChanged;
        ScreenCaptureLogService.SystemPictureSaved += OnPictureSaved;
        PlanetTypesService.Loaded += OnPlanetTypesLoaded;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        Unloaded += (_, _) =>
        {
            ViewModel.SystemArrived -= ShowSystemChange;
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
            PlanetDetailsService.Captured -= OnPlanetChanged;
            PlanetDetailsService.Changed -= OnPlanetChanged;
            PlanetWaterService.Changed -= OnPlanetChanged;
            FindsService.Changed -= OnFindsChanged;
            SystemNotesService.Changed -= OnSystemChanged;
            SystemPageReader.Captured -= OnSystemChanged;
            BodyKindsService.Captured -= OnSystemChanged;
            UnmatchedBodiesService.Changed -= OnSystemChanged;
            SystemMarketService.Changed -= OnMarketChanged;
            ScreenCaptureLogService.SystemPictureSaved -= OnPictureSaved;
            PlanetTypesService.Loaded -= OnPlanetTypesLoaded;
            GameProcessMonitorService.RunningStateChanged -= OnGameRunningChanged;
            SaveSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
            _bannerStoryboard?.Stop();
        };
        if (SaveSessionManager.IsSaveLoaded) _ = ViewModel.LoadAsync();
        _ = PlanetTypesService.EnsureLoadedAsync();
    }

    /// <summary>The planet type tables arrived: show the bodies nobody has discovered yet.</summary>
    private void OnPlanetTypesLoaded() => DispatcherQueue.TryEnqueue(() => ViewModel.Refresh(notes: false));

    /// <summary>The game started or stopped, or a marker was staged or saved: the planet cards' marker buttons follow.</summary>
    private void OnGameRunningChanged(object? sender, bool running) => DispatcherQueue.TryEnqueue(() => ViewModel.RefreshMarkers());
    private void OnPendingEditsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => ViewModel.RefreshMarkers());

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = ViewModel.LoadAsync());

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        ViewModel.Show(snapshot, live: true);
    });

    private bool IsThisSystem(ulong system) => system == 0 || system == ViewModel.SystemAddress;

    private void OnPlanetChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() => { if (IsThisSystem(system)) ViewModel.Refresh(notes: false); });
    private void OnSystemChanged(ulong system) => DispatcherQueue.TryEnqueue(() => { if (IsThisSystem(system)) ViewModel.Refresh(notes: true); });
    private void OnFindsChanged() => DispatcherQueue.TryEnqueue(() => ViewModel.Refresh(notes: false));
    private void OnMarketChanged(ulong system) => DispatcherQueue.TryEnqueue(() => { if (IsThisSystem(system)) ViewModel.ShowMarket(); });
    private void OnPictureSaved(ulong system) => DispatcherQueue.TryEnqueue(() => { if (IsThisSystem(system)) _ = ViewModel.RefreshPictureAsync(); });

    private void ShowHelp_Click(object sender, RoutedEventArgs e) => ViewModel.SetHelpVisible(true);
    private void HideHelp_Click(object sender, RoutedEventArgs e) => ViewModel.SetHelpVisible(false);
    private void Compact_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleCompact();

    private void Tile_OpenRequested(object? sender, PlanetTileViewModel tile) =>
        Frame.Navigate(typeof(ExplorePages.PlanetDetailsPage), tile.Planet);

    private void Map_BodyClicked(object? sender, SystemMapBody body) =>
        Frame.Navigate(typeof(ExplorePages.PlanetDetailsPage), body.Planet);

    private void AssignUnmatched_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is UnmatchedBodyViewModel row) ViewModel.AssignUnmatched(row);
    }

    private void DismissUnmatched_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is UnmatchedBodyViewModel row) ViewModel.DismissUnmatched(row);
    }

    private async void CaptureSystem_Click(object sender, RoutedEventArgs e)
    {
        CaptureResult result;
        try { result = await ViewModel.CaptureSystemAsync(); }
        catch (Exception ex) { result = new CaptureResult(false, ex.Message); }
        CaptureFlyoutIcon.Glyph = result.Success ? "" : "";
        CaptureFlyoutIcon.Foreground = ThemeResources.Brush(result.Success ? "ThemeAccentBrush" : "ThemeWarningBrush");
        CaptureFlyoutTitle.Text = !result.Success ? "Couldn't read the screen" : result.ReadInfo is not null ? "System read" : "Picture taken";
        CaptureFlyoutDetails.Text = result.Message;
        CaptureFlyout.ShowAt(sender as FrameworkElement ?? CaptureSystemBtn);
    }

    /// <summary>A bold banner over the card for a few seconds after live follow sees a new
    /// system, so the switch can't be missed, and the card fading in.</summary>
    private void ShowSystemChange(string detail)
    {
        SystemChangeDetailTxt.Text = detail;
        SystemChangeOverlay.Visibility = Visibility.Visible;
        _bannerStoryboard?.Stop();

        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            SystemChangeOverlay.Opacity = 1;
            var hide = DispatcherQueue.CreateTimer();
            hide.Interval = TimeSpan.FromSeconds(4);
            hide.IsRepeating = false;
            hide.Tick += (_, _) => SystemChangeOverlay.Visibility = Visibility.Collapsed;
            hide.Start();
            return;
        }

        // Pop in, hold ~2.5s, then fade out over 1.5s; the card fades in underneath.
        var banner = new DoubleAnimationUsingKeyFrames();
        banner.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        banner.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(250), Value = 1 });
        banner.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(2750), Value = 1 });
        banner.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(4250), Value = 0 });
        Storyboard.SetTarget(banner, SystemChangeOverlay);
        Storyboard.SetTargetProperty(banner, "Opacity");
        var body = new DoubleAnimation { From = 0.15, To = 1, Duration = TimeSpan.FromMilliseconds(450) };
        Storyboard.SetTarget(body, SystemBody);
        Storyboard.SetTargetProperty(body, "Opacity");
        _bannerStoryboard = new Storyboard();
        _bannerStoryboard.Children.Add(banner);
        _bannerStoryboard.Children.Add(body);
        _bannerStoryboard.Completed += (_, _) => SystemChangeOverlay.Visibility = Visibility.Collapsed;
        _bannerStoryboard.Begin();
    }
}
