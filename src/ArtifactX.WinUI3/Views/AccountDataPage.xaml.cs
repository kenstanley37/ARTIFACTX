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
/// discussion this followed): a single flat browsable list of every catalog
/// item the account could plausibly have unlocked (NmsAccountData.UnlockedItemsPath,
/// "B89/B1h" - the master unlock list), with a checkbox to toggle each one.
/// Only this one list is exposed - the smaller category-specific lists
/// (blueprints/banners/base parts) are left alone since it's unconfirmed
/// whether the game needs them kept in sync with B1h.</summary>
public sealed partial class AccountDataPage : Page
{
    // Cached across page instances/navigations - the catalog's own rows
    // (GameId/DisplayName/CategoryLabel) never change within an app session,
    // only which ones are unlocked for the currently active account, so
    // there's no reason to re-query SQLite for ~4,700 rows on every visit.
    private static List<Models.CatalogUnlockableItem>? _catalogCache;

    private List<AccountItemRowViewModel>? _allItems;
    private List<string> _workingIds = new();
    private HashSet<string> _workingIdSet = new(StringComparer.Ordinal);

    public AccountDataPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        Unloaded += Page_Unloaded;

        CategoryFilterBox.SelectedIndex = 0;
        UnlockFilterBox.SelectedIndex = 0;

        _ = LoadAccountDataAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadAccountDataAsync());

    private void OnPendingEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateEditButtons);

    /// <summary>See project_static_event_leak_fix.md - Frame.Navigate creates a
    /// brand-new Page instance every visit, so these static-service event
    /// subscriptions must be torn down here or they pile up across navigations.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged -= OnPendingEditsChanged;
    }

    private async Task LoadAccountDataAsync()
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        NoAccountDataTxt.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;

        bool loaded = await AccountSessionManager.LoadAsync();
        if (!loaded)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            NoAccountDataTxt.Visibility = Visibility.Visible;
            return;
        }

        _catalogCache ??= await CatalogService.GetAllUnlockableItemsAsync();

        var unlockedArray = AccountSessionManager.GetValue(NmsAccountData.UnlockedItemsPath) as JArray;
        _workingIds = unlockedArray?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _workingIdSet = new HashSet<string>(_workingIds, StringComparer.Ordinal);

        _allItems = _catalogCache.Select(c => new AccountItemRowViewModel
        {
            GameId = c.GameId,
            DisplayName = c.DisplayName,
            CategoryLabel = c.CategoryLabel,
            IsUnlocked = _workingIdSet.Contains(c.GameId)
        }).ToList();

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyFilters();
        UpdateEditButtons();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";
        string? categoryFilter = (CategoryFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        string? unlockFilter = (UnlockFilterBox.SelectedItem as ComboBoxItem)?.Content as string;

        IEnumerable<AccountItemRowViewModel> filtered = _allItems;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (categoryFilter is "Technology" or "Product" or "Substance")
            filtered = filtered.Where(i => i.CategoryLabel == categoryFilter);

        if (unlockFilter == "Unlocked Only")
            filtered = filtered.Where(i => i.IsUnlocked);
        else if (unlockFilter == "Locked Only")
            filtered = filtered.Where(i => !i.IsUnlocked);

        var resultList = filtered.ToList();
        ItemsListView.ItemsSource = resultList;
        ResultCountTxt.Text = $"{resultList.Count:N0} of {_allItems.Count:N0} items";
    }

    private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not AccountItemRowViewModel row) return;

        bool newValue = cb.IsChecked ?? false;
        row.IsUnlocked = newValue;

        if (newValue)
        {
            if (_workingIdSet.Add(row.GameId)) _workingIds.Add(row.GameId);
        }
        else
        {
            if (_workingIdSet.Remove(row.GameId)) _workingIds.Remove(row.GameId);
        }

        AccountSessionManager.StageEdit(new JArray(_workingIds), NmsAccountData.UnlockedItemsPath);

        // Only re-run the filter when the active unlock filter would actually
        // hide/show this row - re-filtering on every click otherwise would be
        // pure overhead for the (default) "All Items" view.
        string? unlockFilter = (UnlockFilterBox.SelectedItem as ComboBoxItem)?.Content as string;
        if (unlockFilter is "Unlocked Only" or "Locked Only")
            ApplyFilters();
    }

    private void UpdateEditButtons()
    {
        bool dirty = AccountSessionManager.HasUnsavedChanges;
        SaveChangesBtn.IsEnabled = dirty;
        DiscardChangesBtn.IsEnabled = dirty;
        PendingEditsTxt.Text = dirty ? "Unsaved changes" : "";
    }

    private async void SaveChangesBtn_Click(object sender, RoutedEventArgs e)
    {
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
