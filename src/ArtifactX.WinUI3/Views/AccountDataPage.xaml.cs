using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Helpers.AccountData;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private readonly AccountUnlocksViewModel _unlocks = new();
    private readonly HashSet<string> _lockedOnly = new(StringComparer.Ordinal);

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
        var types = await ProductTypesService.LoadAsync();
        var rewards = await ExpeditionRewardsService.LoadAsync();

        _unlocks.Load();
        var unlocks = _unlocks;
        var products = catalogItems.Where(c => c.TemplateType == "GcProductTable").Select(c => c.GameId).ToHashSet(StringComparer.Ordinal);
        _allItems = await Task.Run(() => catalogItems.Select(c => new AccountItemRowViewModel
        {
            GameId = c.GameId,
            DisplayName = CatalogGroups.DisplayName(c.DisplayName),
            CategoryLabel = c.CategoryLabel,
            CatalogGroup = CatalogGroups.For(c.CatalogGroup, types.GetValueOrDefault(c.GameId)),
            IsUnlocked = unlocks.IsUnlocked(c.GameId)
        }).ToList());

        // Products only: on the owner's account (2026-11-03) B1h held 4,281 of the
        // catalog's 4,508 products and none of its 372 technologies, 254 procedural
        // upgrade modules or 82 substances - known technology is per save (Catalogue
        // page), so ticking those here wrote ids the game never reads from this list.
        // Expedition and Twitch rewards live on their own pages - including season
        // rewards without an EXPD_ id (SWARM_HAT), once the catalog lists them.
        _catalogSource = _allItems.Where(i => products.Contains(i.GameId) &&
            !i.GameId.StartsWith("EXPD_", StringComparison.OrdinalIgnoreCase) &&
            !i.GameId.StartsWith("TWITCH_", StringComparison.OrdinalIgnoreCase) &&
            !rewards.ContainsKey(i.GameId)).ToList();

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

        CatalogCategoriesItemsControl.ItemsSource = CatalogGroups.Order
            .Select(group =>
            {
                var items = _catalogSource.Where(i => i.CatalogGroup == group).ToList();
                return new UnlockSectionViewModel(group, group, items, Math.Min(items.Count * 48 + 24, 420), _unlocks, _lockedOnly);
            })
            .Where(section => section.Items.Count > 0)
            .ToList();

        CatalogResultCountTxt.Text = $"{_catalogSource.Count:N0} items";
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
