using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Shared planner presentation and app-wide feed wiring. Each cached
/// page owns its own ViewModel, keeping cooking and refining navigation separate.</summary>
public sealed partial class RecipePlanner : UserControl
{
    public RecipePlannerViewModel ViewModel
    {
        get => (RecipePlannerViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RecipePlannerViewModel), typeof(RecipePlanner), new PropertyMetadata(null));

    public RecipePlanner()
    {
        InitializeComponent();
        Loaded += Planner_Loaded;
        Unloaded += Planner_Unloaded;
    }
    private async void Planner_Loaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded += CatalogReloaded;
        FishCatchRecorder.SnapshotSeen += SnapshotSeen;
        SaveSessionManager.ActiveSessionChanged += SessionChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        if (!ViewModel.IsReady) await ViewModel.LoadAsync();
        else await ViewModel.ResumeAsync();
        if (IsLoaded) await ViewModel.RefreshOwnedAsync(LiveSaveWatcher.LatestSnapshot);
    }
    private void Planner_Unloaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded -= CatalogReloaded;
        FishCatchRecorder.SnapshotSeen -= SnapshotSeen;
        SaveSessionManager.ActiveSessionChanged -= SessionChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Stop();
    }
    private void CatalogReloaded() => DispatcherQueue.TryEnqueue(async () => { if (IsLoaded) await ViewModel.LoadAsync(); });
    private async void SnapshotSeen(PlanetSnapshot snapshot) => await ViewModel.RefreshOwnedAsync(snapshot);
    private async void SessionChanged(object? sender, EventArgs e) => await ViewModel.RefreshOwnedAsync();

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) =>
        ViewModel.ShowShoppingList = ReferenceEquals(sender.SelectedItem, ShoppingTab);

    // Drag the tree's background with the mouse to pan it (touch pans natively). ChangeView keeps it inside the
    // scrollable area. The move cursor shows only over empty background of a tree bigger than its panel.
    private static readonly InputCursor MoveCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    private static readonly InputCursor ArrowCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private bool _panning;
    private Windows.Foundation.Point _panStart;
    private double _panStartX, _panStartY;

    private bool CanPan => TreeScroller.ScrollableWidth > 0 || TreeScroller.ScrollableHeight > 0;

    private bool OverTile(object source)
    {
        for (var node = source as DependencyObject; node is not null && node != TreeCanvas; node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase) return true;
        return false;
    }

    private void TreeCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || !CanPan || OverTile(e.OriginalSource)) return;
        if (!e.GetCurrentPoint(TreeScroller).Properties.IsLeftButtonPressed) return;
        _panning = TreeCanvas.CapturePointer(e.Pointer);
        if (!_panning) return;
        _panStart = e.GetCurrentPoint(TreeScroller).Position;
        _panStartX = TreeScroller.HorizontalOffset;
        _panStartY = TreeScroller.VerticalOffset;
        ProtectedCursor = MoveCursor;
        e.Handled = true;
    }

    private void TreeCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) return;
        if (!_panning)
        {
            ProtectedCursor = CanPan && !OverTile(e.OriginalSource) ? MoveCursor : ArrowCursor;
            return;
        }
        var at = e.GetCurrentPoint(TreeScroller).Position;
        TreeScroller.ChangeView(_panStartX - (at.X - _panStart.X), _panStartY - (at.Y - _panStart.Y), null, disableAnimation: true);
        e.Handled = true;
    }

    private void TreeCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_panning) return;
        TreeCanvas.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void TreeCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => _panning = false;

    private void TreeCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_panning) ProtectedCursor = ArrowCursor;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Selecting a tile (also from the Shopping list) shows the Selected step tab from its top.
        if (e.PropertyName == nameof(RecipePlannerViewModel.ShowShoppingList))
            Tabs.SelectedItem = ViewModel.ShowShoppingList ? ShoppingTab : SelectedTab;
        else if (e.PropertyName == nameof(RecipePlannerViewModel.SelectedItem))
            TabScroller.ChangeView(null, 0, null, disableAnimation: true);
        // A different recipe starts the tree from its top again.
        else if (e.PropertyName == nameof(RecipePlannerViewModel.TargetName))
            TreeScroller.ChangeView(0, 0, null, disableAnimation: true);
    }
}
