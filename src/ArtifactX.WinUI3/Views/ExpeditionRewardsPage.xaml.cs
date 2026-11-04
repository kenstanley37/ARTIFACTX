using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private readonly AccountUnlocksViewModel _unlocks = new();
    private readonly HashSet<string> _lockedOnly = new(StringComparer.Ordinal);

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
        var rewards = await ExpeditionRewardsService.LoadAsync();

        _unlocks.Load();

        var expeditionNames = CatalogService.BuildExpeditionNameLookup(catalogItems);

        var unlocks = _unlocks;
        // The game's own reward table says which expedition each reward is from,
        // including rewards without an EXPD_ id (SWARM_HAT). Catalogs built before
        // format 60 lack it, so EXPD_ items and their banner/decal/title naming remain.
        _allItems = await Task.Run(() => catalogItems
            .Where(c => rewards.ContainsKey(c.GameId) || c.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(c => c.GameId, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                var (expeditionNumber, expeditionName) = rewards.TryGetValue(c.GameId, out var reward)
                    ? (reward.Expedition, expeditionNames.GetValueOrDefault(reward.Expedition, $"Expedition {reward.Expedition}"))
                    : CatalogService.GetExpeditionInfo(c.GameId, expeditionNames);
                return new AccountItemRowViewModel
                {
                    GameId = c.GameId,
                    DisplayName = c.DisplayName,
                    CategoryLabel = c.CategoryLabel,
                    CatalogGroup = c.CatalogGroup ?? "Uncategorized",
                    ExpeditionNumber = expeditionNumber,
                    ExpeditionName = expeditionName,
                    IsUnlocked = unlocks.IsUnlocked(c.GameId)
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

        // Every card gets the SAME height (user feedback 2026-08-13: mixed
        // heights looked untidy), sized to the largest visible card but capped
        // at MaxCardRows, past which a card scrolls inside (2026-11-03: sizing
        // to Swarm's 24 rewards left most cards half empty). ~48px/row (2
        // stacked TextBlocks + Grid padding) plus a margin - an undershoot
        // shows a scrollbar on a card that fits, as 36px/row once did.
        int maxNumberedCount = numberedGroups.Count > 0 ? numberedGroups.Max(g => g.Count()) : 0;
        double sharedCardHeight = CardHeight(maxNumberedCount);

        var sections = numberedGroups
            .Select(g => new UnlockSectionViewModel($"#{g.Key}", $"#{g.Key}: {g.First().ExpeditionName}",
                g.ToList(), sharedCardHeight, _unlocks, _lockedOnly))
            .ToList();

        var otherItems = _currentlyVisibleRows.Where(i => i.ExpeditionNumber is null).ToList();
        if (otherItems.Count > 0)
        {
            // EXPD_ items the game's reward table doesn't list (a few fireworks),
            // or every unmatched EXPD_ item on a catalog built before format 60.
            sections.Add(new UnlockSectionViewModel("other", "Other Expedition Items", otherItems,
                Math.Max(sharedCardHeight, CardHeight(otherItems.Count)), _unlocks, _lockedOnly));
        }

        CategoriesItemsControl.ItemsSource = sections;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_expeditionSource.Count:N0} items";
    }

    private const int MaxCardRows = 10;

    private static double CardHeight(int rows) => Math.Min(rows, MaxCardRows) * 48 + 24;

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
