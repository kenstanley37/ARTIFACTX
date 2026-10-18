using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class AppTitleBar : UserControl
{
    private bool _isSaving;

    // Tracks the previously-shown label so Refresh() can tell "the active save
    // actually switched to a different one" apart from "Refresh ran again for
    // some other reason" (an edit, a Save, GameProcessMonitorService ticking) -
    // only the former should play the switch-flash animation.
    private string? _lastActiveLabel;

    public AppTitleBar()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        SaveSessionManager.ExternalChangeDetected += OnExternalChangeDetected;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;

        VersionTxt.Text = $"v{AppVersionService.DisplayVersion}";
        RefreshGameLabel();
        GameDataService.StateChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshGameLabel);
        // No Unloaded/unsub here - AppTitleBar is created once for the
        // lifetime of MainWindow, never re-created via Frame navigation like
        // a Page, so this is safe the same way the subscriptions above are.
        UpdateCheckService.Changed += OnUpdateCheckChanged;
        RefreshUpdateBadge();
        ThemeBtnTxt.Text = $"Theme: {ThemeService.Current.DisplayName}";
        FishCatchRecorder.ActiveChanged += RefreshRecording;
        RefreshRecording();
        ScreenCaptureLogService.RecordingChanged += () => DispatcherQueue.TryEnqueue(RefreshScreenRecording);
        RefreshScreenRecording();

        // Parts placed follow the game's saves live, like the Bases page (2026-10-18).
        FishCatchRecorder.SnapshotSeen += snapshot =>
            DispatcherQueue.TryEnqueue(() => { if (SaveSessionManager.IsSaveLoaded) ShowParts(snapshot.Bases, snapshot.LooseObjects); });

        Refresh();
    }

    /// <summary>Every part placed (all bases and loose objects), with the breakdown on hover.
    /// The game caps them per base and overall but shows no count; its caps aren't in its data
    /// files, so this shows the count alone (NmsBuildCounts).</summary>
    private void ShowParts(Newtonsoft.Json.Linq.JArray? bases, Newtonsoft.Json.Linq.JArray? loose)
    {
        PartsTxt.Text = $"{NmsBuildCounts.Total(bases, loose):N0} PARTS PLACED";
        ToolTipService.SetToolTip(PartsRegion, "Building parts you've placed - the game caps them per base and in total.\n"
            + NmsBuildCounts.Breakdown(bases, loose).Replace(" · ", "\n"));
    }

    private void RefreshScreenRecording()
    {
        ScreenRecordingBtn.Visibility = ScreenCaptureLogService.IsRecording ? Visibility.Visible : Visibility.Collapsed;
        // Beside the fish "Recording as you play" label when that shows, in its place when not.
        ScreenRecordingBtn.Margin = new Thickness(0, 0, RecordingBtn.Visibility == Visibility.Visible ? 170 : 12, 0);
    }

    private void StopScreenRecording_Click(object sender, RoutedEventArgs e)
    {
        ScreenRecordingFlyout.Hide();
        ScreenCaptureLogService.Enabled = false;
    }

    private void RefreshRecording()
    {
        RecordingBtn.Visibility = FishCatchRecorder.IsActive ? Visibility.Visible : Visibility.Collapsed;
        RefreshScreenRecording();
    }

    /// <summary>The installed game's own build, read from the install (never a
    /// version typed into ArtifactX - the old hand-written "NMS: SWARM" went a
    /// whole major update out of date), and whether the game data matches it.</summary>
    private void RefreshGameLabel()
    {
        var game = GameDataService.Game;
        NmsCompatTxt.Text = game is null ? "NMS: not found" : $"NMS {game.BuildLabel}";
        string data = GameDataService.State switch
        {
            GameDataState.UpToDate => "ArtifactX's game data matches it.",
            GameDataState.NotBuilt => "ArtifactX hasn't read its game files yet.",
            GameDataState.GameUpdated => "The game has updated since ArtifactX last read its files.",
            GameDataState.BuilderUpdated => "This version of ArtifactX collects more from the game files - update the game data.",
            _ => "",
        };
        ToolTipService.SetToolTip(NmsCompatTxt, game is null
            ? "No Man's Sky wasn't found on this PC - choose its folder in Settings."
            : $"Your No Man's Sky: {game.BuildLabel}{(game.SteamBuildId is { } s ? $" (Steam build {s})" : "")}, at {game.RootFolder}. {data}");
    }

    private void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout();
        foreach (var theme in ThemeService.Themes)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = theme.DisplayName,
                GroupName = "Theme",
                IsChecked = theme == ThemeService.Current,
            };
            item.Click += (_, _) =>
            {
                ThemeService.Apply(theme.Id);
                ThemeBtnTxt.Text = $"Theme: {ThemeService.Current.DisplayName}";
            };
            menu.Items.Add(item);
        }
        menu.ShowAt(ThemeBtn);
    }

    private void OnUpdateCheckChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshUpdateBadge);

    private void RefreshUpdateBadge()
    {
        bool hasUpdate = UpdateCheckService.LastResult?.Status == UpdateCheckStatus.UpdateAvailable;
        UpdateAvailableBtn.Visibility = hasUpdate ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void UpdateAvailableBtn_Click(object sender, RoutedEventArgs e)
    {
        string? url = UpdateCheckService.LastResult?.ReleaseUrl;
        if (url is null) return;
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (_isSaving) return;
        DispatcherQueue.TryEnqueue(Refresh);
    }

    private void OnGameRunningStateChanged(object? sender, bool isRunning)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Refresh();
            if (!isRunning)
                SaveSessionManager.CheckForExternalChanges();
        });
    }

    private void OnExternalChangeDetected(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            // Say what Reload costs: a player who applied a ship design while the game ran
            // reloaded, lost the design and took it for a missing Save button (2026-10-16).
            int pending = SaveSessionManager.PendingEditCount;
            string lost = pending == 0 ? ""
                : $"\n\nReloading discards your {(pending == 1 ? "1 unsaved change" : $"{pending} unsaved changes")} - make them again after it reloads. Nothing has been written to your save.";
            var dialog = new ContentDialog
            {
                Title = "No Man's Sky was closed",
                Content = "The save file on disk has changed since you loaded it - most likely from " +
                          "an in-game autosave. Continuing to edit and save now could overwrite that " +
                          "recent progress.\n\nWe recommend reloading this save before making further changes." + lost,
                PrimaryButtonText = pending == 0 ? "Reload (recommended)" : "Reload and discard changes (recommended)",
                SecondaryButtonText = "Keep editing anyway",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await SaveSessionManager.ReloadFromDiskAsync();

            Refresh();
        });
    }

    private void Refresh()
    {
        bool gameRunning = GameProcessMonitorService.IsGameRunning;

        // Falls back to just the platform name (e.g. "Steam", no slot) when a
        // platform is selected but no save slot is loaded yet - Account Data
        // is reachable in exactly that state (see SaveSessionManager.HasActivePlatform),
        // and previously showed nothing at all here, giving no indication of
        // which platform account's accountdata.hg was being edited.
        string? currentLabel = SaveSessionManager.IsSaveLoaded
            ? SaveSessionManager.ActiveLabel
            : SaveSessionManager.ActivePlatformDisplayName;

        if (currentLabel is not null && _lastActiveLabel is not null && currentLabel != _lastActiveLabel)
            SaveSwitchFlash.Begin();
        _lastActiveLabel = currentLabel;

        ActiveSaveTxt.Text = currentLabel is null
            ? string.Empty
            : SaveSessionManager.IsExpeditionActive ? $"•  {currentLabel} (Expedition)" : $"•  {currentLabel}";

        if (!SaveSessionManager.IsSaveLoaded)
        {
            ResetDisplayTokens();
            CurrencyDisplayRegion.Visibility = Visibility.Collapsed;
            SaveBtn.Visibility = Visibility.Collapsed;
            ResetBtn.Visibility = Visibility.Collapsed;
            PendingChangesTxt.Visibility = Visibility.Collapsed;
            return;
        }

        CurrencyDisplayRegion.Visibility = Visibility.Visible;

        // ToDisplayValue undoes the uint32 wrap the game itself writes for a
        // balance over ~2.1 billion - see its doc comment. No-op for the
        // vast majority of saves that never get that high.
        long units = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0);
        long nanites = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0);
        long quicksilver = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0);

        ShowParts(SaveSessionManager.GetValue("vLc", "6f=", "F?0") as Newtonsoft.Json.Linq.JArray,
            SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) as Newtonsoft.Json.Linq.JArray);
        UnitsTxt.Text = $"{units:N0} UNITS";
        NanitesTxt.Text = $"{nanites:N0} NANITES";
        QuicksilverTxt.Text = $"{quicksilver:N0} QUICKSILVER";

        bool hasChanges = SaveSessionManager.HasUnsavedChanges;
        ResetBtn.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
        SaveBtn.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
        SaveBtn.IsEnabled = hasChanges && !gameRunning;

        int pendingCount = SaveSessionManager.PendingEditCount;
        PendingChangesTxt.Text = pendingCount == 1 ? "1 change pending" : $"{pendingCount} changes pending";
        PendingChangesTxt.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetDisplayTokens()
    {
        UnitsTxt.Text = "0 UNITS";
        NanitesTxt.Text = "0 NANITES";
        QuicksilverTxt.Text = "0 QUICKSILVER";
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e) => SaveSessionManager.DiscardAllEdits();

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (GameProcessMonitorService.IsGameRunning) return;

        _isSaving = true;
        SaveBtn.IsEnabled = false;
        ResetBtn.IsEnabled = false;
        SaveSpinner.IsActive = true;
        SaveBtnText.Text = "Saving...";

        try
        {
            await SaveSessionManager.CommitAsync();
            SaveBtnText.Text = "Saved";
            await Task.Delay(1200);
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Save failed",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            SaveSpinner.IsActive = false;
            SaveBtnText.Text = "Save";
            ResetBtn.IsEnabled = true;
            _isSaving = false;
            Refresh();
        }
    }
}