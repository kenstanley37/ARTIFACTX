using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// App-wide settings: where backups go (BackupLocation), backups of the
/// player's own data (UserDatabase - daily automatic ones, manual ones, copies
/// to a file, restore), game save backups (BackupService - how many to keep
/// per slot, what they take up, clean-up), appearance and updates. Backups
/// used to run where nobody could see them (2026-10-12).
/// </summary>
public sealed partial class SettingsPage : Page
{
    private static readonly int[] DataKeepChoices = { 7, 14, 30, 60 };
    private static readonly int[] SaveKeepChoices = { 5, 10, 20, 50, 0 };
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        UpdateCheckService.Changed += OnUpdateCheckChanged;
        Unloaded += (_, _) => UpdateCheckService.Changed -= OnUpdateCheckChanged;

        DailyBackupSwitch.IsOn = UserDatabase.DailyBackups;
        CaptureLogSwitch.IsOn = ScreenCaptureLogService.Enabled;
        RefreshCaptureLogStatus();
        foreach (int keep in DataKeepChoices) DataKeepBox.Items.Add(new ComboBoxItem { Content = $"{keep} backups", Tag = keep });
        DataKeepBox.SelectedItem = DataKeepBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == UserDatabase.BackupsToKeep)
            ?? DataKeepBox.Items[1];
        foreach (int keep in SaveKeepChoices) SaveKeepBox.Items.Add(new ComboBoxItem { Content = keep == 0 ? "Keep them all" : $"{keep} backups", Tag = keep });
        SaveKeepBox.SelectedItem = SaveKeepBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == BackupService.KeepPerSlot)
            ?? SaveKeepBox.Items[2];
        foreach (var theme in ThemeService.Themes) ThemeBox.Items.Add(new ComboBoxItem { Content = theme.DisplayName, Tag = theme.Id });
        ThemeBox.SelectedItem = ThemeBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == ThemeService.Current.Id);
        _loading = false;

        GameDataService.StateChanged += OnGameDataChanged;
        GameDataService.ProgressChanged += OnGameDataProgress;
        Unloaded += (_, _) =>
        {
            GameDataService.StateChanged -= OnGameDataChanged;
            GameDataService.ProgressChanged -= OnGameDataProgress;
        };
        RefreshGameData();
        RefreshFolder();
        RefreshDataBackups();
        RefreshUserDbInfo();
        _ = RefreshSaveBackupsAsync();
        RefreshUpdateStatus();
    }

    // ---------------------------------------------------------------- game data

    private void OnGameDataChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshGameData);
    private void OnGameDataProgress(ArtifactX.GameData.GameDataProgress p) => DispatcherQueue.TryEnqueue(RefreshGameData);

    /// <summary>Where the game is, which build it is, and whether ArtifactX's
    /// game data matches it (GameDataService).</summary>
    private void RefreshGameData()
    {
        var game = GameDataService.Game;
        GameFolderTxt.Text = game is null
            ? "No Man's Sky wasn't found on this PC - choose its folder."
            : $"No Man's Sky: {game.RootFolder} ({game.BuildLabel})";

        var info = GameDataService.Info;
        string built = info?.BuiltAtUtc is DateTime at ? $"Read {at.ToLocalTime():d MMM yyyy, h:mm tt}" : "Read";
        string from = info?.ExeVersion is { Length: > 0 } v ? $" from build {v}" : "";
        GameDataStatusTxt.Text = GameDataService.IsBuilding
            ? $"Reading your game files - {(GameDataService.Progress is { } p ? $"step {p.Step} of {p.Steps}: {p.Label}" : "starting")}..."
            : GameDataService.State switch
            {
                GameDataState.UpToDate => $"{built}{from} - matches your game.",
                GameDataState.NotBuilt => "Not read yet.",
                GameDataState.GameUpdated => $"{built}{from} - your game has updated since, so some items may be missing or out of date.",
                GameDataState.BuilderUpdated => $"{built}{from} - this version of ArtifactX reads more from the game files; update to get it.",
                _ => $"{built}{from}.",
            };
        GameDataBuildBtn.Content = GameDataService.IsBuilding ? "Cancel"
            : GameDataService.State switch
            {
                GameDataState.NotBuilt => "Read my game files",
                GameDataState.UpToDate => "Read them again",
                _ => "Update game data",
            };
        GameDataBuildBtn.IsEnabled = game is not null || GameDataService.IsBuilding;
        GameDataLogBtn.IsEnabled = File.Exists(GameDataService.LogPath);
    }

    private void GameDataBuild_Click(object sender, RoutedEventArgs e)
    {
        if (GameDataService.IsBuilding) GameDataService.CancelBuild();
        else GameDataService.StartBuild();
        RefreshGameData();
    }

    private async void ChangeGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        if (GameDataService.ChooseGameFolder(folder.Path)) RefreshGameData();
        else GameFolderTxt.Text = $"\"{folder.Path}\" isn't a No Man's Sky folder - choose the one the game is installed in (with GAMEDATA and Binaries in it).";
    }

    private void OpenGameDataLog_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(GameDataService.LogPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GameDataService.LogPath) { UseShellExecute = true });
    }

    // ---------------------------------------------------------------- location

    private void RefreshFolder()
    {
        BackupFolderTxt.Text = BackupLocation.Root;
        UseDefaultFolderBtn.Visibility = BackupLocation.IsDefault ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        await ChangeFolderAsync(Path.Combine(folder.Path, "ArtifactX Backups"));
    }

    private async void UseDefaultFolder_Click(object sender, RoutedEventArgs e) => await ChangeFolderAsync(null);

    /// <summary>Asks whether to bring the existing backups along, then switches.</summary>
    private async Task ChangeFolderAsync(string? target)
    {
        var dialog = new ContentDialog
        {
            Title = "Move your backups too?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"New backups will go to {target ?? BackupLocation.DefaultRoot}. Move the backups you already have there as well? "
                    + "Leaving them means they stay where they are now and won't show here.",
            },
            PrimaryButtonText = "Move them",
            SecondaryButtonText = "Leave them",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.None) return;

        Status(FolderStatusTxt, choice == ContentDialogResult.Primary ? "Moving backups..." : "");
        string? error = await BackupLocation.ChangeAsync(target, moveExisting: choice == ContentDialogResult.Primary);
        Status(FolderStatusTxt, error ?? (choice == ContentDialogResult.Primary ? "Done - your backups are in the new folder." : "New backups will go to the new folder."));
        RefreshFolder();
        RefreshDataBackups();
        await RefreshSaveBackupsAsync();
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchFolderPathAsync(BackupLocation.Root);

    // ---------------------------------------------------------------- user.db

    private void CaptureLog_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ScreenCaptureLogService.Enabled = CaptureLogSwitch.IsOn;
        RefreshCaptureLogStatus();
    }

    private void RefreshCaptureLogStatus()
    {
        long bytes = ScreenCaptureLogService.SizeOnDisk();
        CaptureLogStatusTxt.Text = (bytes == 0 ? "Nothing read yet." : $"The reading log uses {bytes / 1048576.0:0.#} MB (the oldest is removed past 200 MB).")
            + (PlanetPageReader.Filled > 0 ? $" {PlanetPageReader.Filled} planet{(PlanetPageReader.Filled == 1 ? "" : "s")} filled in from their Discoveries page this session." : "")
            + (SystemPageReader.Filled > 0 ? $" {SystemPageReader.Filled} system{(SystemPageReader.Filled == 1 ? "" : "s")} filled in from the game's system screens." : "")
            + (ScreenCaptureLogService.Enabled && !GameProcessMonitorService.IsGameRunning ? " Recording starts when you start No Man's Sky." : "");
    }

    private async void ManualCaptureSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        ManualCaptureSettingsBtn.IsEnabled = false;
        try
        {
            var result = await ScreenCaptureLogService.CaptureNowAsync();
            RefreshCaptureLogStatus();
            CaptureLogStatusTxt.Text = result.Message;
        }
        finally
        {
            ManualCaptureSettingsBtn.IsEnabled = true;
        }
    }

    private void OpenCaptureLog_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = ScreenCaptureLogService.Folder, UseShellExecute = true });

    private async void DeleteCaptureLog_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Delete the reading log?",
            Content = "The log of text read from game screens is removed. What was filled in from it (planet details, markets, creatures...), your saves and other ArtifactX data aren't touched.",
            PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        ScreenCaptureLogService.DeleteAll();
        RefreshCaptureLogStatus();
    }

    private void DailyBackup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UserDatabase.DailyBackups = DailyBackupSwitch.IsOn;
    }

    private void DataKeep_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || DataKeepBox.SelectedItem is not ComboBoxItem { Tag: int keep }) return;
        UserDatabase.BackupsToKeep = keep;
    }

    private async void BackUpDataNow_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => UserDatabase.Backup("manual"));
        Status(DataStatusTxt, "Backed up.");
        RefreshDataBackups();
        RefreshUserDbInfo();
    }

    private async void ExportData_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = $"ArtifactX backup {DateTime.Now:yyyy-MM-dd}" };
        picker.FileTypeChoices.Add("ArtifactX backup", new List<string> { ".artifactx" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        try
        {
            string path = file.Path;
            await Task.Run(() => UserDatabase.ExportTo(path));
            Status(DataStatusTxt, $"Saved a copy to {file.Name}.");
        }
        catch (Exception ex)
        {
            Status(DataStatusTxt, $"Couldn't save the copy: {ex.Message}");
        }
    }

    private async void RestoreDataFromFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".artifactx");
        picker.FileTypeFilter.Add(".db"); // exports and backups made before 2026-10-12
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        await RestoreAsync(file.Path, file.Name);
    }

    private async Task RestoreAsync(string path, string label)
    {
        var confirm = new ContentDialog
        {
            Title = "Restore your ArtifactX data?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"Everything ArtifactX keeps for you (templates, settings, nicknames, notes, finds and the rest) will be replaced by the backup from {label}. "
                    + "This PC's window position and save-folder list stay as they are. Your current data is backed up first, so this can be undone.",
            },
            PrimaryButtonText = "Restore",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        string? error = await Task.Run(() => UserDatabase.ImportFrom(path));
        if (error is null)
        {
            // Every page reads afresh when opened; these keep data in memory.
            SystemNotesService.Reload();
            PinnedRecipesService.Reload();
            FindsService.Reload();
            PlanetWaterService.Reload();
            PlanetDetailsService.Reload();
            BodyKindsService.Reload();
            SystemMarketService.Reload();
            FishCatchService.Reload();
            GuidesService.Reload();
            HiddenStationsService.Reload();
            if (AppSettingsService.ThemeId is { Length: > 0 } theme && theme != ThemeService.Current.Id)
                ThemeService.Apply(theme);
        }
        Status(DataStatusTxt, error ?? $"Restored the backup from {label}. Your data from before is in the list below.");
        RefreshDataBackups();
        RefreshUserDbInfo();
    }

    private void RefreshUserDbInfo()
    {
        var (main, wal, total) = UserDatabase.GetDatabaseSizeDetails();
        string sizeText = wal > 0
            ? $"{Size(total)} (main: {Size(main)}, WAL: {Size(wal)})"
            : Size(total);

        UserDbInfoTxt.Text = $"Database: {Path.GetFileName(UserDatabase.FilePath)} ({sizeText})  ·  Schema v{UserDatabase.SchemaVersion}\nLocation: {UserDatabase.FilePath}";
    }

    private async void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchFolderPathAsync(LocalAppDataPaths.RootFolder);

    private void RefreshDataBackups()
    {
        DataBackupsPanel.Children.Clear();
        List<UserDatabase.DataBackup> backups;
        try { backups = UserDatabase.ListBackups(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { backups = new(); }

        if (backups.Count == 0)
        {
            DataBackupsPanel.Children.Add(new TextBlock { Text = "No backups yet.", FontSize = 12, Opacity = 0.7 });
            return;
        }
        foreach (var backup in backups)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = $"{backup.Created:ddd MMM d, yyyy  h:mm tt}  ·  {ReasonLabel(backup.Reason)}  ·  {Size(backup.Bytes)}",
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var restore = new Button { Content = "Restore", FontSize = 12, Padding = new Thickness(10, 3, 10, 3) };
            restore.Click += async (_, _) => await RestoreAsync(backup.Path, $"{backup.Created:MMM d, h:mm tt}");
            Grid.SetColumn(restore, 1);
            row.Children.Add(restore);
            var delete = new Button { Content = "Delete", FontSize = 12, Padding = new Thickness(10, 3, 10, 3) };
            delete.Click += (_, _) =>
            {
                try { UserDatabase.DeleteBackup(backup.Path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status(DataStatusTxt, $"Couldn't delete it: {ex.Message}"); }
                RefreshDataBackups();
            };
            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);
            DataBackupsPanel.Children.Add(row);
        }
    }

    private static string ReasonLabel(string reason) => reason switch
    {
        "daily" => "Automatic (daily)",
        "manual" => "Made by you",
        "before-import" => "Before a restore",
        _ when reason.StartsWith("before-v", StringComparison.Ordinal) => "Before an ArtifactX update",
        "" => "Backup",
        _ => reason,
    };

    // ---------------------------------------------------------------- game saves

    private async Task RefreshSaveBackupsAsync()
    {
        int keep = BackupService.KeepPerSlot;
        List<BackupService.BackupGroupInfo> groups;
        try { groups = await Task.Run(() => BackupService.Summarize(keep)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { groups = new(); }

        int count = groups.Sum(g => g.Count);
        long bytes = groups.Sum(g => g.Bytes);
        SaveBackupsTotalTxt.Text = count == 0 ? "No game save backups yet." : $"{count:N0} backups · {Size(bytes)}";

        long freeable = groups.Sum(g => g.BytesBeyondKeep);
        int extra = groups.Sum(g => g.CountBeyondKeep);
        CleanUpBtn.Content = extra > 0 ? $"Clean up now — frees {Size(freeable)}" : "Nothing to clean up";
        CleanUpBtn.IsEnabled = extra > 0;

        SaveBackupsPanel.Children.Clear();
        foreach (var account in groups.GroupBy(g => g.AccountFolder))
        {
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(new TextBlock { Text = BackupService.AccountLabel(account.Key), FontWeight = FontWeights.SemiBold, FontSize = 13 });
            foreach (var group in account.OrderBy(g => GroupOrder(g.Group)))
            {
                string beyond = group.CountBeyondKeep > 0 ? $"  ·  {group.CountBeyondKeep} older than the newest {keep}" : "";
                block.Children.Add(new TextBlock
                {
                    Text = $"{GroupLabel(group.Group)}  ·  {group.Count} backups  ·  {Size(group.Bytes)}  ·  newest {group.Newest:MMM d, h:mm tt}{beyond}",
                    FontSize = 12,
                    Opacity = 0.85,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
            SaveBackupsPanel.Children.Add(block);
        }
    }

    private static string GroupLabel(string group) =>
        group == "AccountData" ? "Account data (shared by every save)"
        : group.StartsWith("Slot", StringComparison.Ordinal) ? $"Slot {group[4..]}" : group;

    private static int GroupOrder(string group) =>
        group.StartsWith("Slot", StringComparison.Ordinal) && int.TryParse(group[4..], out int slot) ? slot : 0;

    private async void SaveKeep_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SaveKeepBox.SelectedItem is not ComboBoxItem { Tag: int keep }) return;
        BackupService.KeepPerSlot = keep;
        Status(SaveBackupsStatusTxt, keep == 0
            ? "Every backup will be kept."
            : "Older backups are removed when a save gets a new one, or now with Clean up.");
        await RefreshSaveBackupsAsync();
    }

    private async void CleanUp_Click(object sender, RoutedEventArgs e)
    {
        int keep = BackupService.KeepPerSlot;
        if (keep <= 0) return;
        var confirm = new ContentDialog
        {
            Title = "Delete older game save backups?",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"Keeps the newest {keep} backups of each save slot and deletes the rest. Your saves themselves aren't touched.",
            },
            PrimaryButtonText = "Delete older backups",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        CleanUpBtn.IsEnabled = false;
        int removed = await Task.Run(() => BackupService.PruneAll(keep));
        Status(SaveBackupsStatusTxt, $"Deleted {removed:N0} older backups.");
        await RefreshSaveBackupsAsync();
    }

    // ---------------------------------------------------------------- appearance, updates

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string id } || id == ThemeService.Current.Id) return;
        ThemeService.Apply(id);
    }

    private void OnUpdateCheckChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshUpdateStatus);

    private void RefreshUpdateStatus()
    {
        VersionTxt.Text = $"ArtifactX v{AppVersionService.DisplayVersion}";
        var result = UpdateCheckService.LastResult;
        UpdateStatusTxt.Text = result?.Message ?? "";
        ReleasePageLink.Visibility = result?.Status == UpdateCheckStatus.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesBtn.IsEnabled = false;
        UpdateStatusTxt.Text = "Checking...";
        try { await UpdateCheckService.RefreshAsync(); }
        finally { CheckForUpdatesBtn.IsEnabled = true; }
    }

    private async void ReleasePageLink_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateCheckService.LastResult?.ReleaseUrl is string url) await Launcher.LaunchUriAsync(new Uri(url));
    }

    /// <summary>Status lines only take up room when they have something to say.</summary>
    private static void Status(TextBlock line, string text)
    {
        line.Text = text;
        line.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.#", CultureInfo.CurrentCulture) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0.#", CultureInfo.CurrentCulture) + " MB",
        >= 1L << 10 => (bytes / (double)(1L << 10)).ToString("0.#", CultureInfo.CurrentCulture) + " KB",
        _ => $"{bytes} bytes",
    };
}
