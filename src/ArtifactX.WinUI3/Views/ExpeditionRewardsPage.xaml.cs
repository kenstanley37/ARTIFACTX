using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>Split out of AccountDataPage 2026-08-13 (user feedback: "It feels
/// like we have too much on Catalog... It's just the catalog page is a little
/// laggy") - the "EXPD_" reward items (Banner/Decal/Title/etc.) that
/// AccountDataPage used to render as its own section now get their own page,
/// so Catalog's own load no longer also builds ~22 Expedition cards it
/// wasn't even showing. Shares the same AccountSessionManager session and the
/// same NmsAccountData.UnlockedItemsPath array as AccountDataPage/
/// TwitchRewardsPage - all 3 are independently-navigable views of one shared
/// piece of account-wide state, not 3 separate arrays.</summary>
public sealed partial class ExpeditionRewardsPage : Page
{
    private List<AccountItemRowViewModel>? _allItems;
    private List<AccountItemRowViewModel> _expeditionSource = new();
    private List<AccountItemRowViewModel> _currentlyVisibleRows = new();

    private List<string> _workingIds = new();
    private HashSet<string> _workingIdSet = new(StringComparer.Ordinal);

    public ExpeditionRewardsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        Unloaded += Page_Unloaded;

        _ = LoadAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateEditButtons);

    private void OnGameRunningStateChanged(object? sender, bool isRunning) =>
        DispatcherQueue.TryEnqueue(UpdateEditButtons);

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
        GameProcessMonitorService.RunningStateChanged -= OnGameRunningStateChanged;
    }

    private async Task LoadAsync()
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        NoAccountDataTxt.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        await Task.Yield();

        bool loaded = await AccountSessionManager.LoadAsync();
        if (!loaded)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            NoAccountDataTxt.Visibility = Visibility.Visible;
            return;
        }

        ActivePlatformTxt.Text = SaveSessionManager.ActivePlatformDisplayName ?? "";

        var catalogItems = await CatalogService.GetAllUnlockableItemsAsync();

        var unlockedArray = AccountSessionManager.GetValue(NmsAccountData.UnlockedItemsPath) as JArray;
        _workingIds = unlockedArray?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _workingIdSet = new HashSet<string>(_workingIds.Select(CatalogService.NormalizeId), StringComparer.Ordinal);

        var expeditionNames = CatalogService.BuildExpeditionNameLookup(catalogItems);

        var workingIdSet = _workingIdSet;
        _allItems = await Task.Run(() => catalogItems
            .Where(c => c.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase))
            .Select(c =>
            {
                var (expeditionNumber, expeditionName) = CatalogService.GetExpeditionInfo(c.GameId, expeditionNames);
                return new AccountItemRowViewModel
                {
                    GameId = c.GameId,
                    DisplayName = c.DisplayName,
                    CategoryLabel = c.CategoryLabel,
                    CatalogGroup = c.CatalogGroup ?? "Uncategorized",
                    ExpeditionNumber = expeditionNumber,
                    ExpeditionName = expeditionName,
                    IsUnlocked = workingIdSet.Contains(c.GameId)
                };
            }).ToList());

        _expeditionSource = _allItems;

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        PopulateExpeditionFilter(expeditionNames);
        ApplyFilters();
        UpdateEditButtons();
    }

    /// <summary>Rebuilds ExpeditionFilterBox's items from the same live-derived
    /// expeditionNames lookup used for the row data itself - never a hardcoded
    /// expedition list, so a future game update's new expedition shows up
    /// automatically after the next DataCataloger catalog rebuild.</summary>
    private void PopulateExpeditionFilter(Dictionary<int, string> expeditionNames)
    {
        int? previouslySelected = (ExpeditionFilterBox.SelectedItem as ComboBoxItem)?.Tag as int?;

        ExpeditionFilterBox.Items.Clear();
        ExpeditionFilterBox.Items.Add(new ComboBoxItem { Content = "All Expeditions", Tag = null });
        foreach (var kvp in expeditionNames.OrderBy(kvp => kvp.Key))
            ExpeditionFilterBox.Items.Add(new ComboBoxItem { Content = $"#{kvp.Key}: {kvp.Value}", Tag = kvp.Key });

        int indexToSelect = 0;
        if (previouslySelected is int number)
        {
            for (int i = 0; i < ExpeditionFilterBox.Items.Count; i++)
            {
                if (((ComboBoxItem)ExpeditionFilterBox.Items[i]).Tag as int? == number)
                {
                    indexToSelect = i;
                    break;
                }
            }
        }
        ExpeditionFilterBox.SelectedIndex = indexToSelect;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void ExpeditionFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";

        IEnumerable<AccountItemRowViewModel> filtered = _expeditionSource;

        if ((ExpeditionFilterBox.SelectedItem as ComboBoxItem)?.Tag is int selectedExpedition)
            filtered = filtered.Where(i => i.ExpeditionNumber == selectedExpedition);

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _currentlyVisibleRows = filtered.ToList();

        var numberedGroups = _currentlyVisibleRows
            .Where(i => i.ExpeditionNumber is int)
            .GroupBy(i => i.ExpeditionNumber!.Value)
            .OrderBy(g => g.Key)
            .ToList();

        // Every real per-expedition card gets the SAME height - tall enough to
        // fit the largest one currently visible in full, so none of them ever
        // needs to scroll internally (user feedback 2026-08-13: cards had
        // inconsistent heights, and one with only 4 items still showed a
        // scrollbar because the per-row pixel estimate undershot the row's
        // real rendered height). ~48px/row (2 stacked TextBlocks + Grid
        // padding) plus a safety margin, not measured exactly - a little
        // extra blank space at the bottom of a shorter card is harmless; an
        // undershoot reproduces the exact scrollbar bug being fixed here.
        int maxNumberedCount = numberedGroups.Count > 0 ? numberedGroups.Max(g => g.Count()) : 0;
        double sharedCardHeight = maxNumberedCount * 48 + 24;

        var sections = numberedGroups
            .Select(g => new CatalogCategorySectionViewModel
            {
                Header = $"#{g.Key}: {g.First().ExpeditionName} ({g.Count()})",
                Items = g.ToList(),
                ListHeight = sharedCardHeight
            })
            .ToList();

        var otherItems = _currentlyVisibleRows.Where(i => i.ExpeditionNumber is null).ToList();
        if (otherItems.Count > 0)
        {
            // Deliberately NOT matched to sharedCardHeight - this catch-all can
            // hold well over a hundred items (every EXPD_ item with no
            // expedition-number signal), so giving it the same height as a
            // 3-9 item card would force every OTHER card to also grow
            // enormous to match it. Keeps the original capped/scrollable
            // treatment instead - the one deliberate exception to "same
            // height, no scrollbar."
            sections.Add(new CatalogCategorySectionViewModel
            {
                Header = $"Other Expedition Items ({otherItems.Count})",
                Items = otherItems,
                ListHeight = Math.Min(otherItems.Count * 36 + 8, 420)
            });
        }

        CategoriesItemsControl.ItemsSource = sections;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_expeditionSource.Count:N0} items";
    }

    private void SetRowUnlocked(AccountItemRowViewModel row, bool unlock)
    {
        row.IsUnlocked = unlock;

        if (unlock)
        {
            if (_workingIdSet.Add(row.GameId))
                _workingIds.Add("^" + row.GameId);
        }
        else
        {
            if (_workingIdSet.Remove(row.GameId))
                _workingIds.RemoveAll(id => CatalogService.NormalizeId(id) == row.GameId);
        }
    }

    private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not AccountItemRowViewModel row) return;

        SetRowUnlocked(row, cb.IsChecked ?? false);
        AccountSessionManager.StageEdit(new JArray(_workingIds), NmsAccountData.UnlockedItemsPath);
    }

    private void UnlockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(unlock: true);

    private void LockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(unlock: false);

    /// <summary>Scoped to whatever's currently visible (search + expedition
    /// filter) rather than every expedition item, mirroring a reference tool's
    /// own per-section Unlock All/Lock All buttons.</summary>
    private void BulkSetRows(bool unlock)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
            SetRowUnlocked(row, unlock);

        AccountSessionManager.StageEdit(new JArray(_workingIds), NmsAccountData.UnlockedItemsPath);
        ApplyFilters();
    }

    private void UpdateEditButtons()
    {
        bool dirty = AccountSessionManager.HasUnsavedChanges;
        bool gameRunning = GameProcessMonitorService.IsGameRunning;

        SaveChangesBtn.IsEnabled = dirty && !gameRunning;
        DiscardChangesBtn.IsEnabled = dirty;
        PendingEditsTxt.Text = dirty
            ? (gameRunning ? "Unsaved changes - close No Man's Sky to save" : "Unsaved changes")
            : "";
    }

    private async void SaveChangesBtn_Click(object sender, RoutedEventArgs e)
    {
        if (GameProcessMonitorService.IsGameRunning) return;

        try
        {
            await AccountSessionManager.CommitAsync();
        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "Save failed",
                Content = $"{ex.Message}\n\nYour changes are still staged and haven't been lost.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }

    private void DiscardChangesBtn_Click(object sender, RoutedEventArgs e)
    {
        AccountSessionManager.RevertEditsUnder(NmsAccountData.UnlockedItemsPath);
        _ = LoadAsync();
    }
}
