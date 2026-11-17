using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views.ProgressionPages;

/// <summary>This save slot's fishing-record checklist. Editing goes through <see cref="FishingRecordEditor"/>,
/// which keeps the save's three parallel 256-slot arrays aligned (title bar Save/Reset).</summary>
public sealed partial class FishingRecordsPage : Page
{
    private List<FishingRecordRowViewModel>? _allItems;
    private List<FishingRecordRowViewModel> _currentlyVisibleRows = new();

    private FishingRecordEditor _records = FishingRecordEditor.Load();

    public FishingRecordsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        Unloaded += Page_Unloaded;

        CaughtFilterBox.SelectedIndex = 0;

        _ = LoadFishingRecordsAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadFishingRecordsAsync());

    /// <summary>Deliberately NOT a full reload - see CataloguePage's identical
    /// comment on why a per-tap full rebuild of ~210 tiles would lag.</summary>
    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateResetButton);

    /// <summary>See project_static_event_leak_fix.md - Frame.Navigate creates a
    /// brand-new Page instance every visit, so these static-service event
    /// subscriptions must be torn down here or they pile up across navigations.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
    }

    private async Task LoadFishingRecordsAsync()
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        // Forces a real dispatcher yield so the spinner gets a render pass -
        // see CataloguePage/LanguageWordsControl's identical fix for the full
        // writeup of why this matters once the catalog cache is warm.
        await Task.Yield();

        var catalogItems = await CatalogService.GetAllUnlockableItemsAsync();
        var fishItems = CatalogService.FilterFishSpecies(catalogItems);
        var flotsamItems = CatalogService.FilterFlotsam(catalogItems);

        await CatalogService.WarmCacheAsync(fishItems.Concat(flotsamItems).Select(c => c.GameId));

        _records = FishingRecordEditor.Load();

        _allItems = fishItems.Select(c => new FishingRecordRowViewModel
        {
            GameId = c.GameId,
            DisplayName = ToDisplayCase(c.DisplayName),
            IsCaught = _records.IsCaught(c.GameId),
            Icon = CatalogService.TryGet(c.GameId)?.Icon
        }).Concat(flotsamItems.Select(c => new FishingRecordRowViewModel
        {
            GameId = c.GameId,
            DisplayName = ToDisplayCase(c.DisplayName),
            IsCaught = _records.IsCaught(c.GameId),
            Icon = CatalogService.TryGet(c.GameId)?.Icon,
            IsFlotsam = true,
        })).ToList();

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyFilters();
        UpdateResetButton();
    }

    /// <summary>The catalog DB's NameEnglish is the game's own raw ALL-CAPS
    /// string - see CataloguePage's identical helper for why title-casing it
    /// reads better across many dense tiles at once.</summary>
    private static string ToDisplayCase(string rawName) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawName.ToLowerInvariant());

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";
        string? caughtFilter = (CaughtFilterBox.SelectedItem as ComboBoxItem)?.Content as string;

        IEnumerable<FishingRecordRowViewModel> filtered = _allItems;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (caughtFilter == "Caught Only")
            filtered = filtered.Where(i => i.IsCaught);
        else if (caughtFilter == "Uncaught Only")
            filtered = filtered.Where(i => !i.IsCaught);

        _currentlyVisibleRows = filtered.ToList();
        // Fish, then the game's "Collected Flotsam" in a section of its own (2026-10-22).
        ItemsListView.ItemsSource = _currentlyVisibleRows.Where(r => !r.IsFlotsam).ToList();
        var flotsam = _currentlyVisibleRows.Where(r => r.IsFlotsam).ToList();
        FlotsamList.ItemsSource = flotsam;
        FlotsamHeader.Visibility = flotsam.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        int fish = _allItems.Count(r => !r.IsFlotsam);
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count(r => !r.IsFlotsam):N0} of {fish:N0} species · {flotsam.Count} of {_allItems.Count - fish} flotsam";
    }

    private void SetRowCaught(FishingRecordRowViewModel row, bool caught)
    {
        row.IsCaught = caught;
        _records.SetCaught(row.GameId, caught);
    }

    private void Tile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FishingRecordRowViewModel row }) return;

        SetRowCaught(row, !row.IsCaught);
        _records.Stage();
        UpdateResetButton();

        string? caughtFilter = (CaughtFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (caughtFilter is "Caught Only" or "Uncaught Only")
            ApplyFilters();
    }

    private void MarkCaughtAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(caught: true);

    private void MarkUncaughtAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(caught: false);

    private void BulkSetVisibleRows(bool caught)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
            SetRowCaught(row, caught);

        _records.Stage();
        UpdateResetButton();
        ApplyFilters();
    }

    private void UpdateResetButton() =>
        PageResetBtn.Visibility = FishingRecordEditor.HasStagedEdits
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        FishingRecordEditor.RevertEdits();
        _ = LoadFishingRecordsAsync();
    }
}
