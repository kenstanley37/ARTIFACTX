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
/// laggy") - the "TWITCH_" reward items that AccountDataPage used to render
/// as its own section now get their own page. Shares the same
/// AccountSessionManager session and the same NmsAccountData.UnlockedItemsPath
/// array as AccountDataPage/ExpeditionRewardsPage - all 3 are independently-
/// navigable views of one shared piece of account-wide state, not 3 separate
/// arrays.</summary>
public sealed partial class TwitchRewardsPage : Page
{
    private List<AccountItemRowViewModel>? _allItems;
    private List<AccountItemRowViewModel> _twitchSource = new();
    private List<AccountItemRowViewModel> _currentlyVisibleRows = new();

    private List<string> _workingIds = new();
    private HashSet<string> _workingIdSet = new(StringComparer.Ordinal);

    public TwitchRewardsPage()
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

        var workingIdSet = _workingIdSet;
        _allItems = await Task.Run(() => catalogItems
            .Where(c => c.GameId.StartsWith("TWITCH_", StringComparison.OrdinalIgnoreCase))
            .Select(c => new AccountItemRowViewModel
            {
                GameId = c.GameId,
                DisplayName = c.DisplayName,
                CategoryLabel = c.CategoryLabel,
                CatalogGroup = c.CatalogGroup ?? "Uncategorized",
                IsUnlocked = workingIdSet.Contains(c.GameId)
            }).ToList());

        _twitchSource = _allItems;

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ContentPanel.Visibility = Visibility.Visible;

        ApplyFilters();
        UpdateEditButtons();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";

        IEnumerable<AccountItemRowViewModel> filtered = _twitchSource;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _currentlyVisibleRows = filtered.ToList();

        CategoriesItemsControl.ItemsSource = _currentlyVisibleRows.Count > 0
            ? new List<CatalogCategorySectionViewModel> { new() { Header = $"Twitch Drops ({_currentlyVisibleRows.Count})", Items = _currentlyVisibleRows } }
            : new List<CatalogCategorySectionViewModel>();

        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_twitchSource.Count:N0} items";
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
