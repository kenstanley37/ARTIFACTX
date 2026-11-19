using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Controls;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Views.SaveEditorPages;

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
        ViewModel.PreviewChanged += ShowPreview;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnEditsChanged;
        LiveItemsService.Changed += OnLiveItemsChanged;
        // Released on leave: each visit makes a new page, and a dead one left subscribed
        // reran its reload on every edit anywhere (the cause of an earlier slowdown).
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            SaveSessionManager.PendingEditsChanged -= OnEditsChanged;
            LiveItemsService.Changed -= OnLiveItemsChanged;
        };
        _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        await ViewModel.LoadAsync();
        ShowSeeds();
        BringSelectedIntoView();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private string? _previewKey;

    /// <summary>The selected frigate's hull: its class's model (FrigatesViewModel.PreviewScene), with the parts and
    /// texture choices its Model Seed picks and the paint its Home Seed gives - as freighters are painted. Rebuilt only
    /// when one of the three changed. Checked 2026-11-19 against the owner's photo-mode shots: CV-7 Keharaso and CS-6
    /// Oishiz share Home Seed 0x980906A7A427, whose colours are their blue panels (Paint #3D8ECA / #446087) and the
    /// slate and brown roof plates (Freighter #2D4B60 / #613C3E) - Oishiz's roof slate, Keharaso's brown, as each
    /// Model Seed picks the plates. Their own Model Seeds' colours (pink, steel blue) fitted neither.</summary>
    private void ShowPreview()
    {
        string? scene = ViewModel.PreviewScene, seed = ViewModel.ModelSeedText, home = ViewModel.HomeSeedText;
        string key = $"{scene}|{seed}|{home}";
        if (key == _previewKey) return;
        _previewKey = key;
        _ = FrigatePreview.ShowAsync(scene, seed, colourSeedText: string.IsNullOrEmpty(home) ? null : home, texturesFromModelSeed: true);
    }

    /// <summary>An edit (here or the title bar's Reset): refresh the cards, not the details being typed in.</summary>
    private void OnEditsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(ViewModel.RefreshCards);

    private void OnLiveItemsChanged(LiveCollection list)
    {
        if (list != LiveCollection.Frigates) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.RefreshFromGame()) ShowSeeds();
        });
    }

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

    private void FleetToggle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleFleet();
        BringSelectedIntoView();
    }

    /// <summary>In one-row view, scroll the selected frigate's card into sight.</summary>
    private void BringSelectedIntoView()
    {
        if (!ViewModel.FleetCollapsed) return;
        int position = -1;
        for (int i = 0; i < ViewModel.Cards.Count; i++) if (ViewModel.Cards[i].IsSelected) { position = i; break; }
        if (position < 0) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (FleetRowItems.ContainerFromIndex(position) is UIElement card) card.StartBringIntoView();
        });
    }

    private void Name_LostFocus(object sender, RoutedEventArgs e) => ViewModel.CommitName();

    private void UseGeneratedName_Click(object sender, RoutedEventArgs e) => ViewModel.UseGeneratedName();

    private void Seed_Changed(object? sender, SeedChange change)
    {
        ViewModel.Edited();
        if (ReferenceEquals(sender, ModelSeedBox)) ViewModel.RefreshGeneratedName();
        else ViewModel.RefreshPreview();
    }

    public Visibility HasGeneratedName(string value) => string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Reset();
        ShowSeeds();
    }
}
