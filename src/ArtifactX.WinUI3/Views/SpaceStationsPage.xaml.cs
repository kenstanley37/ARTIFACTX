using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Space station directorship (SpaceStationsViewModel): the station in the player's
/// current system with its claim progress, a one-click claim, the stations they direct
/// and claims in progress. Reloads on every staged edit and save switch.
/// </summary>
public sealed partial class SpaceStationsPage : Page
{
    public SpaceStationsViewModel ViewModel { get; } = new();

    public SpaceStationsPage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionChanged;
        Unloaded += Page_Unloaded;
        ViewModel.Load();
    }

    public string HowToGlyph(bool open) => open ? "–" : "+";

    public Visibility HasDetails(StationDetailsViewModel? details) => details is null ? Visibility.Collapsed : Visibility.Visible;

    private void OnSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(ViewModel.Load);

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            StationDetailsCard.MinHeight = 0; // different save, different stations
            ViewModel.ResetForNewSave();
        });

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionChanged;
    }

    /// <summary>Grow-only height: each station has a different number of fact
    /// rows, and a card that shrinks while the page is scrolled down made the
    /// whole page jump on every selection (user report 2026-10-10).</summary>
    private void StationDetailsCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height > StationDetailsCard.MinHeight)
            StationDetailsCard.MinHeight = e.NewSize.Height;
    }

    private void HowToToggle_Click(object sender, RoutedEventArgs e) => ViewModel.HowToOpen = !ViewModel.HowToOpen;

    private void StationTile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StationTileViewModel tile || !ViewModel.Select(tile)) return;
        // Scroll the page so the details card's top is in view.
        StationDetailsCard.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = true });
    }

    private void ClaimCard_HideToggled(object? sender, StationClaimViewModel claim) => ViewModel.ToggleHidden(claim);

    private void ClaimStationBtn_Click(object sender, RoutedEventArgs e) => ViewModel.Claim();

    private async void ClearFurnitureBtn_Click(object sender, RoutedEventArgs e)
    {
        var kinds = ViewModel.FurnitureSummary();
        if (kinds.Count == 0) return;
        bool confirmed = await DeleteConfirmation.ShowListAsync(XamlRoot,
            $"Clear {ViewModel.FurnitureCount:N0} pieces of default furniture?",
            $"This removes the default furniture {ViewModel.CurrentName} came with when you claimed it. The station's own structure (connectors, core, paragon) stays, and so does your directorship. Anything you build later is managed on Bases and Parts.",
            kinds);
        if (confirmed) ViewModel.ClearFurniture();
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e) => ViewModel.Reset();
}
