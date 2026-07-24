using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using System;
using System.Threading.Tasks;

namespace NMS.WinUI3.Controls;

public sealed partial class AppTitleBar : UserControl
{
    // Suppresses the automatic event-driven Refresh() while a save is in
    // flight - otherwise the moment CommitAsync clears pending edits, the
    // PendingEditsChanged event fires and immediately hides the Save button
    // before the deliberate "Saved" confirmation has had a chance to show.
    private bool _isSaving;

    public AppTitleBar()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Refresh();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e)
    {
        if (_isSaving) return;
        DispatcherQueue.TryEnqueue(Refresh);
    }

    private void Refresh()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ActiveSaveTxt.Text = string.Empty;
            ResetDisplayTokens();
            SaveBtn.Visibility = Visibility.Collapsed;
            ResetBtn.Visibility = Visibility.Collapsed;
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
        SaveBtn.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
        ResetBtn.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetDisplayTokens()
    {
        UnitsTxt.Text = "0 UNITS";
        NanitesTxt.Text = "0 NANITES";
        QuicksilverTxt.Text = "0 QUICKSILVER";
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.DiscardAllEdits();
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
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
            SaveBtn.IsEnabled = true;
            ResetBtn.IsEnabled = true;
            _isSaving = false;
            Refresh();
        }
    }
}