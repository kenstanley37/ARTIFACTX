using ArtifactX.Almanac.Guide;
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

/// <summary>The in-game Guide screen's per-topic Unlocked/Seen tracking - see
/// NmsAccountData.GuideUnlockedPath/GuideSeenPath for the discovery writeup
/// (a real controlled test diffing accountdata.hg before/after). Account-wide
/// like AccountDataPage, not tied to any one save slot - uses
/// AccountSessionManager and its own separate Save/Discard flow rather than
/// the shared per-slot title bar, for the same reason AccountDataPage does.
/// A separate page from AccountDataPage rather than a second section on it:
/// Guide's rows need TWO independent checkboxes (Unlocked/Seen) per item,
/// a genuinely different shape from that page's single-checkbox rows.</summary>
public sealed partial class GuidePage : Page
{
    private List<GuideTopicRowViewModel>? _allItems;
    private List<GuideTopicRowViewModel> _currentlyVisibleRows = new();

    private List<string> _unlockedFullIds = new();
    private HashSet<string> _unlockedIdSet = new(StringComparer.Ordinal);
    private List<string> _seenFullIds = new();
    private HashSet<string> _seenIdSet = new(StringComparer.Ordinal);

    public GuidePage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        AccountSessionManager.PendingEditsChanged += OnPendingEditsChanged;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        Unloaded += Page_Unloaded;

        _ = LoadGuideAsync();
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = LoadGuideAsync());

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

    private async Task LoadGuideAsync()
    {
        ContentPanel.Visibility = Visibility.Collapsed;
        NoAccountDataTxt.Visibility = Visibility.Collapsed;

        bool loaded = await AccountSessionManager.LoadAsync();
        if (!loaded)
        {
            NoAccountDataTxt.Visibility = Visibility.Visible;
            return;
        }

        ActivePlatformTxt.Text = SaveSessionManager.ActivePlatformDisplayName ?? "";

        var unlockedArray = AccountSessionManager.GetValue(NmsAccountData.GuideUnlockedPath) as JArray;
        _unlockedFullIds = unlockedArray?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _unlockedIdSet = new HashSet<string>(_unlockedFullIds.Select(CatalogService.NormalizeId), StringComparer.Ordinal);

        var seenArray = AccountSessionManager.GetValue(NmsAccountData.GuideSeenPath) as JArray;
        _seenFullIds = seenArray?.Select(t => t.Value<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new();
        _seenIdSet = new HashSet<string>(_seenFullIds.Select(CatalogService.NormalizeId), StringComparer.Ordinal);

        _allItems = GuideTopics.All.Select(t => new GuideTopicRowViewModel
        {
            GameId = t.GameId,
            DisplayName = t.DisplayName,
            IsUnlocked = _unlockedIdSet.Contains(t.GameId),
            IsSeen = _seenIdSet.Contains(t.GameId)
        }).ToList();

        ContentPanel.Visibility = Visibility.Visible;
        ApplyFilters();
        UpdateEditButtons();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (_allItems is null) return;

        string query = SearchBox.Text?.Trim() ?? "";

        IEnumerable<GuideTopicRowViewModel> filtered = _allItems;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.GameId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _currentlyVisibleRows = filtered.ToList();
        ItemsListView.ItemsSource = _currentlyVisibleRows;
        ResultCountTxt.Text = $"{_currentlyVisibleRows.Count:N0} of {_allItems.Count:N0} topics";
    }

    private void SetRowUnlocked(GuideTopicRowViewModel row, bool unlocked)
    {
        row.IsUnlocked = unlocked;

        if (unlocked)
        {
            if (_unlockedIdSet.Add(row.GameId))
                _unlockedFullIds.Add("^" + row.GameId);
        }
        else
        {
            if (_unlockedIdSet.Remove(row.GameId))
                _unlockedFullIds.RemoveAll(id => CatalogService.NormalizeId(id) == row.GameId);
        }
    }

    private void SetRowSeen(GuideTopicRowViewModel row, bool seen)
    {
        row.IsSeen = seen;

        if (seen)
        {
            if (_seenIdSet.Add(row.GameId))
                _seenFullIds.Add("^" + row.GameId);
        }
        else
        {
            if (_seenIdSet.Remove(row.GameId))
                _seenFullIds.RemoveAll(id => CatalogService.NormalizeId(id) == row.GameId);
        }
    }

    private void UnlockedCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not GuideTopicRowViewModel row) return;

        SetRowUnlocked(row, cb.IsChecked ?? false);
        AccountSessionManager.StageEdit(new JArray(_unlockedFullIds), NmsAccountData.GuideUnlockedPath);
    }

    private void SeenCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not GuideTopicRowViewModel row) return;

        SetRowSeen(row, cb.IsChecked ?? false);
        AccountSessionManager.StageEdit(new JArray(_seenFullIds), NmsAccountData.GuideSeenPath);
    }

    private void MarkAllUnknownBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
        {
            SetRowUnlocked(row, false);
            SetRowSeen(row, false);
        }

        AccountSessionManager.StageEdit(new JArray(_unlockedFullIds), NmsAccountData.GuideUnlockedPath);
        AccountSessionManager.StageEdit(new JArray(_seenFullIds), NmsAccountData.GuideSeenPath);
    }

    private void MarkAllKnownBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentlyVisibleRows.Count == 0) return;

        foreach (var row in _currentlyVisibleRows)
        {
            SetRowUnlocked(row, true);
            SetRowSeen(row, true);
        }

        AccountSessionManager.StageEdit(new JArray(_unlockedFullIds), NmsAccountData.GuideUnlockedPath);
        AccountSessionManager.StageEdit(new JArray(_seenFullIds), NmsAccountData.GuideSeenPath);
    }

    /// <summary>See AccountDataPage's identical gating - accountdata.hg is
    /// just as unsafe to overwrite while NMS is running as a per-slot save.</summary>
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
        AccountSessionManager.RevertEditsUnder(NmsAccountData.GuideUnlockedPath);
        AccountSessionManager.RevertEditsUnder(NmsAccountData.GuideSeenPath);
        _ = LoadGuideAsync();
    }
}
