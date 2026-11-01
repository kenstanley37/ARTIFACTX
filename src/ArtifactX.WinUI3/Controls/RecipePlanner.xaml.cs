using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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
        if (!ViewModel.IsReady) await ViewModel.LoadAsync();
        else await ViewModel.ResumeAsync();
        if (IsLoaded) ViewModel.RefreshOwned(LiveSaveWatcher.LatestSnapshot?.ExosuitCargo);
    }
    private void Planner_Unloaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded -= CatalogReloaded;
        FishCatchRecorder.SnapshotSeen -= SnapshotSeen;
        SaveSessionManager.ActiveSessionChanged -= SessionChanged;
        ViewModel.Stop();
    }
    private void CatalogReloaded() => DispatcherQueue.TryEnqueue(async () => { if (IsLoaded) await ViewModel.LoadAsync(); });
    private void SnapshotSeen(PlanetSnapshot snapshot) => ViewModel.RefreshOwned(snapshot.ExosuitCargo);
    private void SessionChanged(object? sender, EventArgs e) => ViewModel.RefreshOwned();
    private void RecipeChanged(string item, string recipe) => ViewModel.ChooseRecipe(item, recipe);
    private void DetailsSelectionChanged() => Details.ChangeView(null, 0, null, disableAnimation: true);
}
