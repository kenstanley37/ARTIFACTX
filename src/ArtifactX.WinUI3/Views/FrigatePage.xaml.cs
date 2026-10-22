using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Owned Frigates - FIRST PASS (2026-07-30), field names confirmed by exact
/// positional cross-reference against libMBIN's GcFleetFrigateSaveData AND
/// by matching all 29 real frigates in the test save against 4 screenshots
/// of the in-game Frigate List panel (see NmsFrigatePaths for the full
/// map). Unlike Settlements' GQA array, FleetFrigates (;Du) is directly and
/// solely owned by the player - no ownership filter needed.
/// </summary>
public sealed partial class FrigatePage : Page
{
    public FrigatesViewModel ViewModel { get; } = new();

    public FrigatePage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnEditsChanged;
        // Released on leave: each visit makes a new page, and a dead one left subscribed
        // reran its reload on every edit anywhere (the cause of an earlier slowdown).
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            SaveSessionManager.PendingEditsChanged -= OnEditsChanged;
        };
        _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        await ViewModel.LoadAsync();
        ShowSeeds();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    /// <summary>An edit (here or the title bar's Reset): refresh the cards, not the details being typed in.</summary>
    private void OnEditsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(ViewModel.RefreshCards);

    private void ShowSeeds()
    {
        int i = ViewModel.SelectedIndex;
        ModelSeedBox.Show(i >= 0 ? NmsFrigatePaths.ModelSeedPath(i) : null);
        HomeSeedBox.Show(i >= 0 ? NmsFrigatePaths.HomeSeedPath(i) : null);
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not FrigateCardViewModel card) return;
        ViewModel.Select(card.Index);
        ShowSeeds();
    }

    private void Name_LostFocus(object sender, RoutedEventArgs e) => ViewModel.CommitName();

    private void Seed_Changed(object? sender, SeedChange change) => ViewModel.Edited();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Reset();
        ShowSeeds();
    }
}
