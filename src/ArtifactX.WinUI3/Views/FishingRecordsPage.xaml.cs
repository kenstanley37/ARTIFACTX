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

namespace ArtifactX.WinUI3.Views;

/// <summary>This save slot's fishing-record checklist (NmsFishingPaths, "vLc/6f=/bTf") -
/// three PARALLEL fixed 256-slot arrays (ProductList/ProductCountList/LargestCatchList),
/// unlike CataloguePage's single flat array. A caught species is present (non-"^")
/// in ProductList; toggling here keeps all three arrays index-aligned and exactly
/// 256 long, matching the shape every real save uses (see NmsFishingPaths' doc
/// comment). Edited through the normal SaveSessionManager Get/Stage/Revert/Commit
/// flow every other per-slot page in this app already uses (title bar Save/Reset).</summary>
public sealed partial class FishingRecordsPage : Page
{
    private List<FishingRecordRowViewModel>? _allItems;
    private List<FishingRecordRowViewModel> _currentlyVisibleRows = new();

    // The three parallel arrays, kept in lockstep - see SetRowCaught. _caughtIdSet
    // mirrors _productIds' non-empty entries (normalized) for O(1) lookup against
    // FishingRecordRowViewModel.GameId, the same shape CataloguePage's
    // _knownIds/_knownIdSet pair uses for its own single array.
    private List<string> _productIds = new();
    private List<uint> _productCounts = new();
    private List<float> _largestCatches = new();
    private HashSet<string> _caughtIdSet = new(StringComparer.Ordinal);

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

        await CatalogService.WarmCacheAsync(fishItems.Select(c => c.GameId));

        LoadFishingRecordArrays();

        _allItems = fishItems.Select(c => new FishingRecordRowViewModel
        {
            GameId = c.GameId,
            DisplayName = ToDisplayCase(c.DisplayName),
            IsCaught = _caughtIdSet.Contains(c.GameId),
            Icon = CatalogService.TryGet(c.GameId)?.Icon
        }).ToList();

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyFilters();
        UpdateResetButton();
    }

    /// <summary>Reads all three parallel arrays fresh from the active session,
    /// defaulting to 256 empty slots if this save has never fished at all
    /// (the field may not exist yet in that case).</summary>
    private void LoadFishingRecordArrays()
    {
        var productIdsArray = SaveSessionManager.GetValue(NmsFishingPaths.ProductListPath) as JArray;
        var productCountsArray = SaveSessionManager.GetValue(NmsFishingPaths.ProductCountListPath) as JArray;
        var largestCatchesArray = SaveSessionManager.GetValue(NmsFishingPaths.LargestCatchListPath) as JArray;

        _productIds = productIdsArray?.Select(t => t.Value<string>() ?? "^").ToList()
            ?? Enumerable.Repeat("^", NmsFishingPaths.SlotCount).ToList();
        _productCounts = productCountsArray?.Select(t => t.Value<uint>()).ToList()
            ?? Enumerable.Repeat(0u, NmsFishingPaths.SlotCount).ToList();
        _largestCatches = largestCatchesArray?.Select(t => t.Value<float>()).ToList()
            ?? Enumerable.Repeat(0f, NmsFishingPaths.SlotCount).ToList();

        _caughtIdSet = new HashSet<string>(
            _productIds.Where(id => id.Length > 0 && id != "^").Select(CatalogService.NormalizeId),
            StringComparer.Ordinal);
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
        ItemsListView.ItemsSource = _currentlyVisibleRows;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_allItems.Count:N0} species";
    }

    /// <summary>Keeps all three parallel arrays in lockstep and exactly
    /// NmsFishingPaths.SlotCount long, matching every real save's shape - see
    /// NmsFishingPaths' doc comment. Marking caught fills the first open
    /// ("^") slot with a plausible-but-not-authentic count/size (1 catch,
    /// 0.0 largest); marking uncaught removes that slot and re-appends an
    /// empty one at the tail, keeping every other entry's index (and the
    /// game's own observed "packed from index 0, no gaps" shape) intact.</summary>
    private void SetRowCaught(FishingRecordRowViewModel row, bool caught)
    {
        row.IsCaught = caught;

        if (caught)
        {
            if (!_caughtIdSet.Add(row.GameId)) return;

            int emptyIndex = _productIds.FindIndex(id => id.Length == 0 || id == "^");
            if (emptyIndex < 0) return; // all 256 slots already full - don't corrupt the array

            _productIds[emptyIndex] = "^" + row.GameId;
            _productCounts[emptyIndex] = 1;
            _largestCatches[emptyIndex] = 0f;
        }
        else
        {
            if (!_caughtIdSet.Remove(row.GameId)) return;

            int idx = _productIds.FindIndex(id => CatalogService.NormalizeId(id) == row.GameId);
            if (idx < 0) return;

            _productIds.RemoveAt(idx);
            _productCounts.RemoveAt(idx);
            _largestCatches.RemoveAt(idx);

            _productIds.Add("^");
            _productCounts.Add(0u);
            _largestCatches.Add(0f);
        }
    }

    private void StageFishingRecordArrays()
    {
        SaveSessionManager.StageEdit(new JArray(_productIds), NmsFishingPaths.ProductListPath);
        SaveSessionManager.StageEdit(new JArray(_productCounts), NmsFishingPaths.ProductCountListPath);
        SaveSessionManager.StageEdit(new JArray(_largestCatches), NmsFishingPaths.LargestCatchListPath);
    }

    private void Tile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FishingRecordRowViewModel row }) return;

        SetRowCaught(row, !row.IsCaught);
        StageFishingRecordArrays();
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

        StageFishingRecordArrays();
        UpdateResetButton();
        ApplyFilters();
    }

    private void UpdateResetButton() =>
        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsFishingPaths.RecordContainerPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEditsUnder(NmsFishingPaths.RecordContainerPath);
        _ = LoadFishingRecordsAsync();
    }
}
