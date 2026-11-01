using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>Wires the cached planner to the app-wide catalog and save feeds.
/// Navigation inside a recipe never creates another page.</summary>
public sealed partial class RecipesPage : Page
{
    public RecipePlannerViewModel ViewModel { get; } = new();
    public RecipesPage() => InitializeComponent();
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        CatalogService.Reloaded += CatalogReloaded;
        FishCatchRecorder.SnapshotSeen += SnapshotSeen;
        SaveSessionManager.ActiveSessionChanged += SessionChanged;
        if (!ViewModel.IsReady) await ViewModel.LoadAsync();
        else await ViewModel.ResumeAsync();
        ViewModel.RefreshOwned();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CatalogService.Reloaded -= CatalogReloaded;
        FishCatchRecorder.SnapshotSeen -= SnapshotSeen;
        SaveSessionManager.ActiveSessionChanged -= SessionChanged;
        ViewModel.Stop();
        base.OnNavigatedFrom(e);
    }
    private void CatalogReloaded() => DispatcherQueue.TryEnqueue(async () => await ViewModel.LoadAsync());
    private void SnapshotSeen(PlanetSnapshot snapshot) => ViewModel.RefreshOwned(snapshot.ExosuitCargo);
    private void SessionChanged(object? sender, EventArgs e) => ViewModel.RefreshOwned();
    private void RecipeChanged(string item, string recipe) => ViewModel.ChooseRecipe(item, recipe);
}
