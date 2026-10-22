using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>Alliance (AllianceViewModel), following the game's saves.</summary>
public sealed partial class AlliancePage : Page
{
    public AllianceViewModel ViewModel { get; } = new();

    public AlliancePage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        FishCatchRecorder.SnapshotSeen += OnSnapshotSeen;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            FishCatchRecorder.SnapshotSeen -= OnSnapshotSeen;
        };
        ViewModel.Show(SaveSessionManager.IsSaveLoaded ? LiveSaveWatcher.FromSession() : null);
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => ViewModel.Show(SaveSessionManager.IsSaveLoaded ? LiveSaveWatcher.FromSession() : null));

    private void OnSnapshotSeen(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        ViewModel.Show(snapshot);
    });
}
