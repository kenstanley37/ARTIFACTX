using System;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class IndustrialCleanupPanel : UserControl
{
    public IndustrialCleanupViewModel ViewModel { get; } = new();
    public IndustrialCleanupPanel()
    {
        InitializeComponent();
        Loaded += PanelLoaded;
        Unloaded += PanelUnloaded;
    }
    private async void PanelLoaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded += CatalogReloaded;
        SaveSessionManager.ActiveSessionChanged += SessionChanged;
        SaveSessionManager.PendingEditsChanged += SessionChanged;
        FishCatchRecorder.SnapshotSeen += SnapshotSeen;
        await ViewModel.LoadAsync();
    }
    private void PanelUnloaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded -= CatalogReloaded;
        SaveSessionManager.ActiveSessionChanged -= SessionChanged;
        SaveSessionManager.PendingEditsChanged -= SessionChanged;
        FishCatchRecorder.SnapshotSeen -= SnapshotSeen;
        ViewModel.Stop();
    }
    private void CatalogReloaded() => DispatcherQueue.TryEnqueue(async () => { if (IsLoaded) await ViewModel.LoadAsync(); });
    private void SessionChanged(object? sender, EventArgs e) => Refresh();
    private void SnapshotSeen(PlanetSnapshot snapshot) => Refresh();
    private void Refresh() => DispatcherQueue.TryEnqueue(() => { if (IsLoaded) ViewModel.Refresh(); });
}
