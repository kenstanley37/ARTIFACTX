using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class AppTitleBar : UserControl
{
    private bool _isSaving;

    public AppTitleBar()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        SaveSessionManager.ExternalChangeDetected += OnExternalChangeDetected;
        GameProcessMonitorService.RunningStateChanged += OnGameRunningStateChanged;
        Refresh();
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

        if (!SaveSessionManager.IsSaveLoaded)
        {
            ActiveSaveTxt.Text = string.Empty;
            ResetDisplayTokens();
            SaveBtn.Visibility = Visibility.Collapsed;
            ResetBtn.Visibility = Visibility.Collapsed;
            PendingChangesTxt.Visibility = Visibility.Collapsed;
            return;
        }

        ActiveSaveTxt.Text = $"•  {SaveSessionManager.ActiveLabel}";

        long units = SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0;
        long nanites = SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0;
        long quicksilver = SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0;

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