using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Selecting a tile (also from the Shopping list) shows the Selected step tab from its top.
        if (e.PropertyName == nameof(RecipePlannerViewModel.ShowShoppingList))
            Tabs.SelectedItem = ViewModel.ShowShoppingList ? ShoppingTab : SelectedTab;
        else if (e.PropertyName == nameof(RecipePlannerViewModel.SelectedItem))
            TabScroller.ChangeView(null, 0, null, disableAnimation: true);
    }
}
