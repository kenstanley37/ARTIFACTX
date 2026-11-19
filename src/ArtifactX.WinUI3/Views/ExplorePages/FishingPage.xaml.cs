using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>
/// Tools > Fishing: wiring only. The filters, fish cards, tiles and Where to catch panel bind to <see cref="ViewModel"/>
/// (FishingPageViewModel). This page passes on the game's saves (FishCatchRecorder.SnapshotSeen) and the planet-water and
/// planet-details changes, keeps the fish grid's scroll position when the list is rebuilt, and handles the buttons.
/// </summary>
public sealed partial class FishingPage : Page
{
    public FishingPageViewModel ViewModel { get; } = new();

    private double _scrollOffset;

    public FishingPage()
    {
        InitializeComponent();
        // The app-wide feed of the game's saves (FishCatchRecorder): one reader for every page.
        FishCatchRecorder.SnapshotSeen += OnLiveSnapshot;
        JumpPlanner.RangeChanged += ViewModel.UpdateWhere;
        PlanetWaterService.Changed += OnPlanetChanged;
        PlanetDetailsService.Captured += OnPlanetChanged;
        PlanetDetailsService.Changed += OnPlanetChanged;
        ViewModel.PropertyChanging += OnViewModelChanging;
        ViewModel.PropertyChanged += OnViewModelChanged;
        Unloaded += (_, _) =>
        {
            JumpPlanner.RangeChanged -= ViewModel.UpdateWhere;
            PlanetWaterService.Changed -= OnPlanetChanged;
            PlanetDetailsService.Captured -= OnPlanetChanged;
            PlanetDetailsService.Changed -= OnPlanetChanged;
            FishCatchRecorder.SnapshotSeen -= OnLiveSnapshot;
        };
        _ = ViewModel.LoadAsync();
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot) => ViewModel.OnSnapshot(snapshot);

    private void OnPlanetChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() => ViewModel.UpdateWhere());

    // A rebuilt fish list (the order changed, e.g. a fish was just caught) keeps the scroll position.
    private void OnViewModelChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(FishingPageViewModel.Cards)) _scrollOffset = FindScrollViewer(FishList)?.VerticalOffset ?? 0;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FishingPageViewModel.Cards) || _scrollOffset <= 0 || FindScrollViewer(FishList) is not { } scroller) return;
        double offset = _scrollOffset;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => scroller.ChangeView(null, offset, null, disableAnimation: true));
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer) return viewer;
            if (FindScrollViewer(child) is ScrollViewer found) return found;
        }
        return null;
    }

    private void NextType_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FishProgressTileViewModel tile) ViewModel.ShowTile(tile);
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearFilters();
        PlanetTypeBox.Focus(FocusState.Programmatic); // the button hides; keep keyboard focus on the filters
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var (headers, rows) = ViewModel.CsvRows();
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX fish", headers, rows);
    }

    private async void FishList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FishCardViewModel card) await ViewModel.SelectFishAsync(card);
    }

    private void WhereClose_Click(object sender, RoutedEventArgs e) => ViewModel.CloseWhere();

    private void Water_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string key) ViewModel.ToggleNoWater(key);
    }

    private void Mark_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string key) ViewModel.ToggleCaughtHere(key);
    }

    private void OverviewToggle_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleOverview();
}
