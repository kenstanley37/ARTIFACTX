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

/// <summary>The general Catalog list - every catalog item the account could
/// plausibly have unlocked (NmsAccountData.UnlockedItemsPath, "B89/B1h" - the
/// master unlock list) EXCEPT Expedition/Twitch reward items, which moved to
/// their own dedicated pages (ExpeditionRewardsPage/TwitchRewardsPage)
/// 2026-08-13 - user feedback: "It feels like we have too much on Catalog...
/// It's just the catalog page is a little laggy." All 3 pages share the same
/// AccountSessionManager session and stage into the same B1h array (see that
/// class's own doc comment) - they're independently-navigable views of one
/// shared piece of account-wide state, not 3 separate arrays. Only B1h is
/// exposed here - the smaller category-specific lists (blueprints/banners/
/// base parts) are left alone since it's unconfirmed whether the game needs
/// them kept in sync with B1h.</summary>
public sealed partial class AccountDataPage : Page
{
    private List<AccountItemRowViewModel>? _allItems;
    private List<AccountItemRowViewModel> _catalogSource = new();

    // _workingIds keeps the exact raw ("^"-prefixed) strings the file itself uses, so
    // re-staging round-trips cleanly; _workingIdSet holds the same ids normalized
    // (CatalogService.NormalizeId - strips the leading "^") for O(1) lookup against
    // AccountItemRowViewModel.GameId, which is already normalized since it comes
    // straight from the catalog DB.
    private List<string> _workingIds = new();
    private HashSet<string> _workingIdSet = new(StringComparer.Ordinal);

    // Fixed display order for the Catalog section's per-category cards - not
    // alphabetical or count-based, so the grouping reads the same way every
    // time regardless of which categories happen to be non-empty.
    private static readonly string[] CatalogGroupOrder =
    {
        "Raw Materials", "Crafted Products", "Equipment", "Constructed Technology",
        "Construction Parts", "Trade Goods", "Curiosities", "Cooking Products", "Uncategorized"
    };

    public AccountDataPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        Unloaded += Page_Unloaded;

        _ = LoadAccountDataAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadAccountDataAsync());

    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateEditButtons);

    private void OnGameRunningStateChanged(object? sender, bool isRunning) =>
        DispatcherQueue.TryEnqueue(UpdateEditButtons);

    /// <summary>See project_static_event_leak_fix.md - Frame.Navigate creates a
    /// brand-new Page instance every visit, so these static-service event
    /// subscriptions must be torn down here or they pile up across navigations.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
        GameProcessMonitorService.RunningStateChanged -= OnGameRunningStateChanged;
    }

    private async Task LoadAccountDataAsync()
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        NoAccountDataTxt.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        // Forces a real dispatcher yield so the spinner gets a render pass
        // before the work below runs - see LanguageWordsControl's identical
        // fix for the full writeup.
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

        var workingIdSet = _workingIdSet;
        _allItems = await Task.Run(() => catalogItems.Select(c => new AccountItemRowViewModel
        {
            GameId = c.GameId,
            DisplayName = c.DisplayName,
            CategoryLabel = c.CategoryLabel,
            CatalogGroup = c.CatalogGroup ?? "Uncategorized",
            IsUnlocked = workingIdSet.Contains(c.GameId)
        }).ToList());

        // EXPD_/TWITCH_ items excluded - they live on their own dedicated pages now.
        _catalogSource = _allItems.Where(i =>
            !i.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase) &&
            !i.GameId.StartsWith("TWITCH_", StringComparison.OrdinalIgnoreCase)).ToList();

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        RebuildCatalogCards();
        UpdateEditButtons();
    }

    /// <summary>Builds the Catalog section's cards once from the full, unfiltered
    /// _catalogSource - no search box/category checkboxes/Unlocked-Locked filter
    /// exist anymore (removed 2026-08-13 - toggling a category checkbox rebuilt
    /// all 9 cards synchronously on the UI thread, visibly freezing the page).</summary>
    private void RebuildCatalogCards()
    {
        if (_allItems is null) return;

        CatalogCategoriesItemsControl.ItemsSource = CatalogGroupOrder
            .Select(group =>
            {
                var items = _catalogSource.Where(i => i.CatalogGroup == group).ToList();
                return new CatalogCategorySectionViewModel
                {
                    Header = $"{group} ({items.Count})",
                    Items = items,
                    ListHeight = Math.Min(items.Count * 36 + 8, 420)
                };
            })
            .Where(section => section.Items.Count > 0)
            .ToList();

        CatalogResultCountTxt.Text = $"{_catalogSource.Count:N0} items";
    }

    /// <summary>Adds/removes one row's id from the working unlock set - shared by
    /// the per-row checkbox handler and the Unlock All/Lock All bulk actions so
    /// both stay in sync with exactly one implementation of the raw-id bookkeeping
    /// (see the _workingIds/_workingIdSet field comment).</summary>
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

    private void CatalogUnlockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_catalogSource, unlock: true, RebuildCatalogCards);

    private void CatalogLockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_catalogSource, unlock: false, RebuildCatalogCards);

    private void BulkSetRows(List<AccountItemRowViewModel> visibleRows, bool unlock, Action reapplyFilters)
    {
        if (visibleRows.Count == 0) return;

        foreach (var row in visibleRows)
            SetRowUnlocked(row, unlock);

        AccountSessionManager.StageEdit(new JArray(_workingIds), NmsAccountData.UnlockedItemsPath);
        reapplyFilters();
    }

    /// <summary>Mirrors AppTitleBar's own SaveBtn gating exactly - accountdata.hg
    /// is just as unsafe to overwrite while NMS is running as a per-slot save is
    /// (see AccountSessionManager's doc comment).</summary>
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
        _ = LoadAccountDataAsync();
    }
}
