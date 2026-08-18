using ArtifactX.Almanac.Freighter;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// "Capital Ship Research" blueprint unlocks - see NmsFreighterUpgradePaths
/// for the full discovery writeup (a real controlled in-game test: 4
/// blueprints researched, a before/after save diff found the array and
/// confirmed its shape). Same "stage the whole array" pattern as
/// BaseStoragePage/LanguageWordsControl - vLc/6f=/eZ< holds ~780 entries
/// covering several unrelated unlock systems (its first sampled entry,
/// "BUILD_REFINER1", is a base-building unlock), so every edit here reads
/// the FULL current array and only adds/removes this page's own curated
/// FreighterUpgrades.All entries, leaving everything else untouched.
/// </summary>
public sealed partial class FreighterUpgradesPage : Page
{
    private List<FreighterUpgradeRowViewModel>? _allItems;
    private List<FreighterUpgradeRowViewModel> _currentlyVisibleRows = new();

    // Same shape as CataloguePage's _knownIds/_knownIdSet - _fullIds preserves
    // every "^"-prefixed entry in the real array (including ones this page
    // doesn't know about) for a clean re-stage; _unlockedIdSet holds this
    // page's own curated ids normalized for O(1) lookup.
    private List<string> _fullIds = new();
    private HashSet<string> _unlockedIdSet = new(StringComparer.Ordinal);

    public FreighterUpgradesPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        Unloaded += Page_Unloaded;

        GroupFilterBox.SelectedIndex = 0;
        UnlockedFilterBox.SelectedIndex = 0;

        _ = LoadUpgradesAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadUpgradesAsync());

    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateResetButton);

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
    }

    /// <summary>Same ownership check as FreighterPage/FrigatePage's own
    /// HasFreighter() - Capital Ship Research only exists once you have a
    /// freighter to research it on.</summary>
    private static bool HasFreighter() =>
        !string.IsNullOrEmpty(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTypePath)?.Value<string>());

    private async Task LoadUpgradesAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded || !HasFreighter())
        {
            NoFreighterTxt.Visibility = Visibility.Visible;
            ContentPanel.Visibility = Visibility.Collapsed;
            return;
        }

        NoFreighterTxt.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        var fullArray = SaveSessionManager.GetValue(NmsFreighterUpgradePaths.BlueprintArrayPath) as JArray;
        _fullIds = fullArray?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _unlockedIdSet = new HashSet<string>(_fullIds.Select(CatalogService.NormalizeId), StringComparer.Ordinal);

        // Was missing entirely until 2026-08-13 - TryGet only ever reads CatalogService's
        // own in-memory cache, it never queries the DB itself (see its own doc comment),
        // so every row's Icon silently came out null unless some OTHER page happened to
        // have already warmed the same ids this session (e.g. Catalogue warming every
        // Technology row) - user reported every tile on this page showing a placeholder
        // with no image, confirmed as this exact gap.
        await CatalogService.WarmCacheAsync(FreighterUpgrades.All.Select(u => u.GameId));

        _allItems = FreighterUpgrades.All.Select(u => new FreighterUpgradeRowViewModel
        {
            GameId = u.GameId,
            DisplayName = u.DisplayName,
            Group = u.Group,
            IsUnlocked = _unlockedIdSet.Contains(u.GameId),
            Icon = CatalogService.TryGet(u.GameId)?.Icon
        }).ToList();

        ApplyFilters();
        UpdateResetButton();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";
        string? groupFilter = (GroupFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        string? unlockedFilter = (UnlockedFilterBox.SelectedItem as ComboBoxItem)?.Content as string;

        IEnumerable<FreighterUpgradeRowViewModel> filtered = _allItems;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (groupFilter is not null && groupFilter != "All Groups")
            filtered = filtered.Where(i => i.Group == groupFilter);

        if (unlockedFilter == "Unlocked Only")
            filtered = filtered.Where(i => i.IsUnlocked);
        else if (unlockedFilter == "Locked Only")
            filtered = filtered.Where(i => !i.IsUnlocked);

        _currentlyVisibleRows = filtered.ToList();
        ItemsListView.ItemsSource = _currentlyVisibleRows;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_allItems.Count:N0} items";
    }

    private void SetRowUnlocked(FreighterUpgradeRowViewModel row, bool unlocked)
    {
        row.IsUnlocked = unlocked;

        if (unlocked)
        {
            if (_unlockedIdSet.Add(row.GameId))
                _fullIds.Add("^" + row.GameId);
        }
        else
        {
            if (_unlockedIdSet.Remove(row.GameId))
                _fullIds.RemoveAll(id => CatalogService.NormalizeId(id) == row.GameId);
        }
    }

    private void Tile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FreighterUpgradeRowViewModel row }) return;

        SetRowUnlocked(row, !row.IsUnlocked);
        SaveSessionManager.StageEdit(new JArray(_fullIds), NmsFreighterUpgradePaths.BlueprintArrayPath);
        UpdateResetButton();

        string? unlockedFilter = (UnlockedFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (unlockedFilter is "Unlocked Only" or "Locked Only")
            ApplyFilters();
    }

    private void MarkUnlockedAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(unlocked: true);

    private void MarkLockedAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetVisibleRows(unlocked: false);

    private void BulkSetVisibleRows(bool unlocked)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
            SetRowUnlocked(row, unlocked);

        SaveSessionManager.StageEdit(new JArray(_fullIds), NmsFreighterUpgradePaths.BlueprintArrayPath);
        UpdateResetButton();
        ApplyFilters();
    }

    private void UpdateResetButton() =>
        PageResetBtn.Visibility = SaveSessionManager.HasStagedEditsUnder(NmsFreighterUpgradePaths.BlueprintArrayPath)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEditsUnder(NmsFreighterUpgradePaths.BlueprintArrayPath);
        _ = LoadUpgradesAsync();
    }
}
