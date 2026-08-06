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
        NmsCompatTxt.Text = $"NMS: {AppVersionService.VerifiedNmsUpdate}";
        // No Unloaded/unsub here - AppTitleBar is created once for the
        // lifetime of MainWindow, never re-created via Frame navigation like
        // a Page, so this is safe the same way the subscriptions above are.
        UpdateCheckService.Changed += OnUpdateCheckChanged;
        RefreshUpdateBadge();

        Refresh();
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
            var dialog = new ContentDialog
            {
                Title = "No Man's Sky was closed",
                Content = "The save file on disk has changed since you loaded it - most likely from " +
                          "an in-game autosave. Continuing to edit and save now could overwrite that " +
                          "recent progress.\n\nWe recommend reloading this save before making further changes.",
                PrimaryButtonText = "Reload (recommended)",
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

        ActiveSaveTxt.Text = currentLabel is not null ? $"•  {currentLabel}" : string.Empty;

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