using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class VehicleUnlockPanel : UserControl
{
    public VehicleUnlockViewModel ViewModel { get; } = new();
    private ExocraftType _type;
    public VehicleUnlockPanel()
    {
        InitializeComponent();
        Loaded += PanelLoaded;
        Unloaded += PanelUnloaded;
    }
    public void Initialize(ExocraftType type) => _type = type;
    private async void PanelLoaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded += CatalogReloaded;
        SaveSessionManager.ActiveSessionChanged += SessionChanged;
        FishCatchRecorder.SnapshotSeen += SnapshotSeen;
        await ViewModel.LoadAsync(_type);
    }
    private void PanelUnloaded(object sender, RoutedEventArgs e)
    {
        CatalogService.Reloaded -= CatalogReloaded;
        SaveSessionManager.ActiveSessionChanged -= SessionChanged;
        FishCatchRecorder.SnapshotSeen -= SnapshotSeen;
        ViewModel.Stop();
    }
    private void CatalogReloaded() => DispatcherQueue.TryEnqueue(async () => { if (IsLoaded) await ViewModel.LoadAsync(_type); });
    private async void SessionChanged(object? sender, EventArgs e) => await ViewModel.RefreshAsync();
    private async void SnapshotSeen(PlanetSnapshot snapshot) => await ViewModel.RefreshAsync(snapshot);
}
