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

/// <summary>Phase 1 of the account-wide unlocks editor (see
/// project_milestones_page.md / AccountSessionManager for the scoping
/// discussion this followed): a browsable view of every catalog item the
/// account could plausibly have unlocked (NmsAccountData.UnlockedItemsPath,
/// "B89/B1h" - the master unlock list), with a checkbox to toggle each one.
/// Only this one array is exposed - the smaller category-specific lists
/// (blueprints/banners/base parts) are left alone since it's unconfirmed
/// whether the game needs them kept in sync with B1h.
///
/// Split into 3 independent sections (Catalog/Expedition Rewards/Twitch
/// Rewards), each its own filtered subset of the same _allItems with its
/// own ListView, search box, and Unlock All/Lock All - previously one list
/// behind a single "Source" dropdown, changed 2026-08-12 per user feedback
/// ("group our checkboxes like NomNom") to show all 3 simultaneously,
/// matching how a reference tool keeps its own Expedition/Twitch sections
/// visually separate rather than folded into one switchable list. The
/// general Catalog list (~4,600 items) deliberately stays a single list
/// with its own category-checkbox/Unlocked-Locked filtering - Expedition/
/// Twitch are both much smaller and don't need that same filter surface.</summary>
public sealed partial class AccountDataPage : Page
{
    private List<AccountItemRowViewModel>? _allItems;

    // Each section's own filtered SOURCE subset of _allItems (computed once
    // per load, not per filter-pass - the EXPD_/TWITCH_ split itself never
    // changes, only search/category/unlock filters within each do).
    private List<AccountItemRowViewModel> _catalogSource = new();
    private List<AccountItemRowViewModel> _expeditionSource = new();
    private List<AccountItemRowViewModel> _twitchSource = new();

    // Each section's own currently-visible rows (after its own filters),
    // used by that section's Unlock All/Lock All to know what "visible"
    // means for bulk actions.
    private List<AccountItemRowViewModel> _catalogVisible = new();
    private List<AccountItemRowViewModel> _expeditionVisible = new();
    private List<AccountItemRowViewModel> _twitchVisible = new();

    // _workingIds keeps the exact raw ("^"-prefixed) strings the file itself uses, so
    // re-staging round-trips cleanly; _workingIdSet holds the same ids normalized
    // (CatalogService.NormalizeId - strips the leading "^") for O(1) lookup against
    // AccountItemRowViewModel.GameId, which is already normalized since it comes
    // straight from the catalog DB. Shared across all 3 sections - they all stage
    // into the same single B1h array regardless of which section a row is in.
    private List<string> _workingIds = new();
    private HashSet<string> _workingIdSet = new(StringComparer.Ordinal);

    public AccountDataPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        Unloaded += Page_Unloaded;

        UnlockFilterBox.SelectedIndex = 0;

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
        // before the work below runs - AccountSessionManager.LoadAsync and
        // GetAllUnlockableItemsAsync both return immediately with no real
        // await reached once already loaded/cached (revisiting this page
        // later in the session), and this page's ~4,700-row build is the
        // largest of the app's three catalog-backed pages - see
        // LanguageWordsControl's identical fix for the full writeup.
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

        // Banner/Decal/Title expedition-reward items spell their own expedition out
        // in their names ("PIONEERS EXPEDITION BANNER") - derived live from the
        // catalog data itself, not a hardcoded/external list. See
        // CatalogService.BuildExpeditionNameLookup for why only these 3 item
        // families get this annotation, not every EXPD_ item.
        var expeditionNames = CatalogService.BuildExpeditionNameLookup(catalogItems);

        var workingIdSet = _workingIdSet;
        _allItems = await Task.Run(() => catalogItems.Select(c =>
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

        // Mutually exclusive - both id prefixes ("EXPD_"/"TWITCH_") are Hello
        // Games' own naming convention, confirmed present on every expedition/
        // Twitch-drop item found in the account's real B1h list (2026-08-04).
        _expeditionSource = _allItems.Where(i => i.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase)).ToList();
        _twitchSource = _allItems.Where(i => i.GameId.StartsWith("TWITCH_", StringComparison.OrdinalIgnoreCase)).ToList();
        _catalogSource = _allItems.Where(i =>
            !i.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase) &&
            !i.GameId.StartsWith("TWITCH_", StringComparison.OrdinalIgnoreCase)).ToList();

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        PopulateExpeditionFilter(expeditionNames);
        ApplyCatalogFilters();
        ApplyExpeditionFilters();
        ApplyTwitchFilters();
        UpdateEditButtons();
    }

    /// <summary>Rebuilds ExpeditionFilterBox's items from the same live-derived
    /// expeditionNames lookup used for the row data itself (BuildExpeditionNameLookup) -
    /// never a hardcoded expedition list, so a future game update's new expedition
    /// shows up automatically after the next DataCataloger catalog rebuild. Re-run on
    /// every load (not just once) since a different save's B1h could theoretically be
    /// paired with a different catalog DB. Preserves the current selection by number
    /// where it still exists, otherwise falls back to "All Expeditions".</summary>
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

    private void CatalogSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyCatalogFilters();

    private void UnlockFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyCatalogFilters();

    private void CategoryCheck_Click(object sender, RoutedEventArgs e) => ApplyCatalogFilters();

    private void ApplyCatalogFilters()
    {
        if (_allItems is null) return;

        string query = CatalogSearchBox.Text?.Trim() ?? "";
        string? unlockFilter = (UnlockFilterBox.SelectedItem as ComboBoxItem)?.Content as string;

        var allowedGroups = new List<string>();
        if (RawMaterialsCategoryCheck.IsChecked == true) allowedGroups.Add("Raw Materials");
        if (CraftedProductsCategoryCheck.IsChecked == true) allowedGroups.Add("Crafted Products");
        if (EquipmentCategoryCheck.IsChecked == true) allowedGroups.Add("Equipment");
        if (ConstructedTechnologyCategoryCheck.IsChecked == true) allowedGroups.Add("Constructed Technology");
        if (ConstructionPartsCategoryCheck.IsChecked == true) allowedGroups.Add("Construction Parts");
        if (TradeGoodsCategoryCheck.IsChecked == true) allowedGroups.Add("Trade Goods");
        if (CuriositiesCategoryCheck.IsChecked == true) allowedGroups.Add("Curiosities");
        if (CookingProductsCategoryCheck.IsChecked == true) allowedGroups.Add("Cooking Products");
        if (UncategorizedCategoryCheck.IsChecked == true) allowedGroups.Add("Uncategorized");

        IEnumerable<AccountItemRowViewModel> filtered = _catalogSource.Where(i => allowedGroups.Contains(i.CatalogGroup));

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (unlockFilter == "Unlocked Only")
            filtered = filtered.Where(i => i.IsUnlocked);
        else if (unlockFilter == "Locked Only")
            filtered = filtered.Where(i => !i.IsUnlocked);

        _catalogVisible = filtered.ToList();
        CatalogListView.ItemsSource = _catalogVisible;
        CatalogResultCountTxt.Text = $"{_catalogVisible.Count:N0} of {_catalogSource.Count:N0} items";
    }

    private void ExpeditionSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyExpeditionFilters();

    private void ExpeditionFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyExpeditionFilters();

    private void ApplyExpeditionFilters()
    {
        if (_allItems is null) return;

        string query = ExpeditionSearchBox.Text?.Trim() ?? "";

        IEnumerable<AccountItemRowViewModel> filtered = _expeditionSource;

        if ((ExpeditionFilterBox.SelectedItem as ComboBoxItem)?.Tag is int selectedExpedition)
            filtered = filtered.Where(i => i.ExpeditionNumber == selectedExpedition);

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _expeditionVisible = filtered.ToList();
        ExpeditionListView.ItemsSource = _expeditionVisible;
        ExpeditionResultCountTxt.Text = $"{_expeditionVisible.Count:N0} of {_expeditionSource.Count:N0} items";
    }

    private void TwitchSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyTwitchFilters();

    private void ApplyTwitchFilters()
    {
        if (_allItems is null) return;

        string query = TwitchSearchBox.Text?.Trim() ?? "";

        IEnumerable<AccountItemRowViewModel> filtered = _twitchSource;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _twitchVisible = filtered.ToList();
        TwitchListView.ItemsSource = _twitchVisible;
        TwitchResultCountTxt.Text = $"{_twitchVisible.Count:N0} of {_twitchSource.Count:N0} items";
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

    /// <summary>Shared across all 3 sections' checkboxes - only the Catalog
    /// section has an Unlocked/Locked filter that could hide/show the row just
    /// clicked, so that's the only one worth re-running here (Expedition/Twitch
    /// have no such filter, re-running their filters on every click would be
    /// pure overhead for no visible effect).</summary>
    private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not AccountItemRowViewModel row) return;

        SetRowUnlocked(row, cb.IsChecked ?? false);
        AccountSessionManager.StageEdit(new JArray(_workingIds), NmsAccountData.UnlockedItemsPath);

        bool isCatalogRow =
            !row.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase) &&
            !row.GameId.StartsWith("TWITCH_", StringComparison.OrdinalIgnoreCase);

        string? unlockFilter = (UnlockFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (isCatalogRow && unlockFilter is "Unlocked Only" or "Locked Only")
            ApplyCatalogFilters();
    }

    private void CatalogUnlockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_catalogVisible, unlock: true, ApplyCatalogFilters);

    private void CatalogLockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_catalogVisible, unlock: false, ApplyCatalogFilters);

    private void ExpeditionUnlockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_expeditionVisible, unlock: true, ApplyExpeditionFilters);

    private void ExpeditionLockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_expeditionVisible, unlock: false, ApplyExpeditionFilters);

    private void TwitchUnlockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_twitchVisible, unlock: true, ApplyTwitchFilters);

    private void TwitchLockAllBtn_Click(object sender, RoutedEventArgs e) => BulkSetRows(_twitchVisible, unlock: false, ApplyTwitchFilters);

    /// <summary>Scoped to whatever's currently visible in ONE section (search +
    /// that section's own filters) rather than the full ~4,700-item catalog,
    /// mirroring a reference tool's own per-section Unlock All/Lock All buttons -
    /// safer than a single "unlock literally everything" action, and makes the
    /// filters double as a way to target a bulk change (e.g. search "portal"
    /// then Unlock All).</summary>
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
    /// (see AccountSessionManager's doc comment), and the game rewriting it later
    /// from its own in-memory copy after ArtifactX already wrote to it will
    /// silently clobber the edit with no error on either side - confirmed
    /// 2026-08-02 when a user-tested unlock never actually stuck (the on-disk
    /// unlock list still matched the pre-edit content byte-for-byte afterward),
    /// most likely explained by exactly this race since this gate didn't exist yet.</summary>
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
