using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ArtifactX.WinUI3.Views;

public sealed partial class SaveFolderSelectPage : Page
{
    public SaveFolderSelectViewModel ViewModel { get; } = new();

    public SaveFolderSelectPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeCommand.ExecuteAsync(null);
            RefreshActivePlatformHighlight();
        };

        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;
        Unloaded += (_, _) =>
        {
            SaveSessionManager.ActiveSessionChanged -= OnActiveSessionChanged;

            // Candidates now persist across page visits (see ViewModel's
            // _cachedCandidates) - without this, every past visit's
            // OnCandidatePropertyChanged handler would stay attached to
            // those shared, surviving candidate objects forever.
            ViewModel.DetachCandidateHandlers();
        };
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshActivePlatformHighlight);

    /// <summary>Marks whichever candidate matches SaveSessionManager's current
    /// platform (or slot, which implies a platform) so the XAML can highlight
    /// it - fired on load and whenever the active session changes (a slot
    /// loads, or a different platform card is expanded elsewhere/earlier).</summary>
    private void RefreshActivePlatformHighlight()
    {
        foreach (var candidate in ViewModel.Candidates)
            candidate.IsActivePlatform = string.Equals(
                candidate.FolderPath, SaveSessionManager.ActivePlatformFolder, StringComparison.OrdinalIgnoreCase);
    }

    private async void BrowseFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        await ViewModel.TryAddCustomFolderAsync(folder.Path);
    }

    private async void SaveSlot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveSlotGroup slot }) return;
        await SaveSessionManager.LoadAsync(slot);
    }

    private void RemoveFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SaveFolderCandidate candidate })
            ViewModel.RemoveCandidateCommand.Execute(candidate);
    }

    /// <summary>Lets the user pick one of this slot's timestamped backups
    /// (see BackupService, populated automatically on every commit) and
    /// restore it - gated on NMS not running, same as every other write in
    /// this app, since overwriting files while the game might touch them
    /// risks the same clobbering this whole feature exists to protect
    /// against. Restoring itself backs up whatever's currently on disk
    /// first, so it's undoable the same way.</summary>
    private async void RestoreBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveSlotGroup slot }) return;

        if (GameProcessMonitorService.IsGameRunning)
        {
            await ShowSimpleDialogAsync("No Man's Sky is running",
                "Close No Man's Sky before restoring a backup - overwriting save files while the game is running risks corrupting them.");
            return;
        }

        string groupKey = BackupService.BuildSlotGroupKey(slot.SourceDisplayName, slot.SlotId);
        var restorePoints = BackupService.GetRestorePoints(groupKey);

        if (restorePoints.Count == 0)
        {
            await ShowSimpleDialogAsync("No backups yet",
                $"No backups have been made for {slot.SlotLabel} yet - one is created automatically the next time you save through ArtifactX.");
            return;
        }

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 300,
            ItemsSource = restorePoints
                .Select(p => $"{p.Timestamp:MMM d, yyyy • h:mm:ss tt}  ({string.Join(", ", p.FileNames)})")
                .ToList()
        };

        var dialog = new ContentDialog
        {
            Title = $"Restore {slot.SlotLabel} From Backup",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Text = "Pick a backup to restore. This overwrites the slot's current save files - whatever's on disk right now is backed up first, so this can be undone the same way."
                    },
                    listView
                }
            },
            PrimaryButtonText = "Restore",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = this.XamlRoot
        };

        listView.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = listView.SelectedIndex >= 0;

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var chosen = restorePoints[listView.SelectedIndex];
        var currentFiles = slot.Files.ToDictionary(f => Path.GetFileName(f.FullPath), f => f.FullPath, StringComparer.OrdinalIgnoreCase);
        BackupService.Restore(chosen, currentFiles, groupKey);

        // If this is the slot ArtifactX currently has loaded, its in-memory
        // state is now stale - force a fresh read so the UI reflects the
        // just-restored files rather than what was in memory beforehand.
        if (ReferenceEquals(SaveSessionManager.ActiveSlot, slot))
            await SaveSessionManager.ReloadFromDiskAsync();

        await ShowSimpleDialogAsync("Restored",
            $"{slot.SlotLabel} was restored from the backup made {chosen.Timestamp:MMM d, yyyy • h:mm:ss tt}.");
    }

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = message },
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    /// <summary>Pure display metadata - never touches the actual folder on
    /// disk. Pre-fills with whatever name (custom or default) is currently
    /// showing, so editing an existing name doesn't mean retyping the whole
    /// thing.</summary>
    private async void RenameFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveFolderCandidate candidate }) return;

        var nameBox = new TextBox { Text = candidate.DisplayName, PlaceholderText = "e.g. \"Backup Drive\"" };
        var dialog = new ContentDialog
        {
            Title = "Name this folder",
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { new TextBlock { Text = "Shown in place of \"Custom Folder\" - doesn't rename anything on disk." }, nameBox }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ViewModel.RenameCandidate(candidate, nameBox.Text?.Trim());
    }
}