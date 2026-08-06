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

/// <summary>This save slot's known-technology checklist (NmsCataloguePaths.
/// KnownTechnologyArrayPath, "vLc/6f=/4kj") - a per-slot field, unlike
/// AccountDataPage's account-wide accountdata.hg list, and edited through the
/// normal SaveSessionManager Get/Stage/Revert/Commit flow every other page in
/// this app already uses (title bar Save/Reset), not a separate save button.
/// Built after directly confirming this field's in-game effect (see
/// NmsCataloguePaths' doc comment) - unlike the account-wide list, which
/// remains unconfirmed.</summary>
public sealed partial class CataloguePage : Page
{
    private List<CatalogueRowViewModel>? _allItems;
    private List<CatalogueRowViewModel> _currentlyVisibleRows = new();

    // See AccountDataPage's identically-shaped fields for why both a raw list
    // and a normalized set are kept - _knownIds preserves the exact "^"-
    // prefixed strings the file itself uses for a clean re-stage; _knownIdSet
    // holds the same ids normalized for O(1) lookup against CatalogueRowViewModel.GameId.
    private List<string> _knownIds = new();
    private HashSet<string> _knownIdSet = new(StringComparer.Ordinal);

    public CataloguePage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        Unloaded += Page_Unloaded;

        KnownFilterBox.SelectedIndex = 0;

        _ = LoadCatalogueAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadCatalogueAsync());

    /// <summary>Deliberately NOT a full LoadCatalogueAsync() reload, unlike a plain
    /// per-slot page with a handful of fields - Tile_Tapped's own StageEdit call
    /// fires this same event on every single click, and rebuilding all ~370
    /// non-virtualized icon tiles from scratch on every tap is what caused a
    /// real multi-second lag per click (2026-08-03) before this was split out.
    /// The tapped tile already updates its own bound properties immediately;
    /// only the Reset button's visibility needs to react to the edit here.</summary>
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

    private async Task LoadCatalogueAsync()
    {
        // Building ~370 rows plus warming their icons takes a couple of
        // seconds - without this, the page sits on a blank Grid until it's
        // all done, which reads as unresponsive rather than loading.
        ContentPanel.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        // Forces a real dispatcher yield so the spinner gets a render pass
        // before the work below runs - both GetAllUnlockableItemsAsync and
        // WarmCacheAsync return immediately with no real await reached once
        // their cache is warm (revisiting this page later in the session),
        // so without this the spinner could be "on" the whole time yet never
        // actually paint a frame - see LanguageWordsControl's identical fix
        // for the full writeup of this bug.
        await Task.Yield();

        var catalogItems = await CatalogService.GetAllUnlockableItemsAsync();
        var technologyItems = catalogItems.Where(c => c.CategoryLabel == "Technology").ToList();

        // Small enough list (~370 rows) that warming every icon up front is
        // cheap - unlike AccountDataPage's ~4,700-row list, which skips this.
        await CatalogService.WarmCacheAsync(technologyItems.Select(c => c.GameId));

        var knownArray = SaveSessionManager.GetValue(NmsCataloguePaths.KnownTechnologyArrayPath) as JArray;
        _knownIds = knownArray?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _knownIdSet = new HashSet<string>(_knownIds.Select(CatalogService.NormalizeId), StringComparer.Ordinal);

        // Off the UI thread, same as LanguageWordsControl's identically-shaped
        // row-building step - building ~370 ObservableObject-backed
        // CatalogueRowViewModel instances synchronously here was a real,
        // felt UI-thread allocation burst (2026-08-05 user report: "the
        // catalog is killing it until it has time to process the garbage").
        // WarmCacheAsync has already finished by this point, so reading
        // CatalogService's cache from a background thread here is safe -
        // nothing is still writing to it.
        var knownIdSet = _knownIdSet;
        _allItems = await Task.Run(() => technologyItems.Select(c => new CatalogueRowViewModel
        {
            GameId = c.GameId,
            DisplayName = ToDisplayCase(c.DisplayName),
            IsKnown = knownIdSet.Contains(c.GameId),
            Icon = CatalogService.TryGet(c.GameId)?.Icon
        }).ToList());

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyFilters();
        UpdateResetButton();
    }

    /// <summary>The catalog DB's NameEnglish is the game's own raw ALL-CAPS
    /// string ("ADVANCED EXOCRAFT LASER") - fine as a single detail-panel
    /// title elsewhere in the app, but reads as harsh/dated across ~370 dense
    /// tiles at once. Display-only - GameId (used for matching/staging) is
    /// never touched by this.</summary>
    private static string ToDisplayCase(string rawName) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawName.ToLowerInvariant());
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";
        string? knownFilter = (KnownFilterBox.SelectedItem as ComboBoxItem)?.Content as string;

        IEnumerable<CatalogueRowViewModel> filtered = _allItems;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (knownFilter == "Known Only")
            filtered = filtered.Where(i => i.IsKnown);
        else if (knownFilter == "Unknown Only")
            filtered = filtered.Where(i => !i.IsKnown);

        _currentlyVisibleRows = filtered.ToList();
        ItemsListView.ItemsSource = _currentlyVisibleRows;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_allItems.Count:N0} items";
    }

    private void SetRowKnown(CatalogueRowViewModel row, bool known)
    {
        row.IsKnown = known;

        if (known)
        {
            if (_knownIdSet.Add(row.GameId))
                _knownIds.Add("^" + row.GameId);
        }
        else
        {
            if (_knownIdSet.Remove(row.GameId))
                _knownIds.RemoveAll(id => CatalogService.NormalizeId(id) == row.GameId);
        }
    }

    private void Tile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CatalogueRowViewModel row }) return;

        SetRowKnown(row, !row.IsKnown);
        SaveSessionManager.StageEdit(new JArray(_knownIds), NmsCataloguePaths.KnownTechnologyArrayPath);
        UpdateResetButton();

        string? knownFilter = (KnownFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (knownFilter is "Known Only" or "Unknown Only")
            ApplyFilters();
    }

    private void MarkKnownAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(known: true);

    private void MarkUnknownAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(known: false);

    private void BulkSetVisibleRows(bool known)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
            SetRowKnown(row, known);

        SaveSessionManager.StageEdit(new JArray(_knownIds), NmsCataloguePaths.KnownTechnologyArrayPath);
        UpdateResetButton();
        ApplyFilters();
    }

    private void UpdateResetButton() =>
        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsCataloguePaths.KnownTechnologyArrayPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEditsUnder(NmsCataloguePaths.KnownTechnologyArrayPath);
        _ = LoadCatalogueAsync();
    }
}
