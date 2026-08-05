using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace ArtifactX.WinUI3.Views;

public sealed partial class GeneralPage : Page
{
    private bool _suppressChangeEvents;

    public GeneralPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        UpdateCheckService.Changed += OnUpdateCheckChanged;
        Unloaded += Page_Unloaded;
        LoadValues();
        RefreshUpdateStatus();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadValues);

    private void OnUpdateCheckChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshUpdateStatus);

    private void RefreshUpdateStatus()
    {
        VersionTxt.Text = $"ArtifactX v{AppVersionService.DisplayVersion}";

        var result = UpdateCheckService.LastResult;
        UpdateStatusTxt.Text = result?.Message ?? "";
        ReleasePageLink.Visibility = result?.Status == UpdateCheckStatus.UpdateAvailable
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void CheckForUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesBtn.IsEnabled = false;
        UpdateStatusTxt.Text = "Checking...";
        try
        {
            await UpdateCheckService.RefreshAsync();
        }
        finally
        {
            CheckForUpdatesBtn.IsEnabled = true;
        }
    }

    private async void ReleasePageLink_Click(object sender, RoutedEventArgs e)
    {
        string? url = UpdateCheckService.LastResult?.ReleaseUrl;
        if (url is null) return;
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    /// <summary>Frame.Navigate creates a fresh Page instance on every visit
    /// (no NavigationCacheMode set anywhere in this app) - without this,
    /// the constructor's subscriptions above would never be released, so
    /// every past visit to this page leaves a dead instance permanently
    /// subscribed to these static events. Confirmed as the real cause of a
    /// reported slowdown (edits on unrelated pages taking multi-second
    /// delays after enough navigation) - each StageEdit anywhere in the app
    /// was re-running every accumulated dead page's full reload logic.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        UpdateCheckService.Changed -= OnUpdateCheckChanged;
    }

    private void LoadValues()
    {
        _suppressChangeEvents = true;

        if (!SaveSessionManager.IsSaveLoaded)
        {
            UnitsBox.Value = double.NaN;
            NanitesBox.Value = double.NaN;
            QuicksilverBox.Value = double.NaN;
            PageResetBtn.Visibility = Visibility.Collapsed;
            _suppressChangeEvents = false;
            return;
        }

        // ToDisplayValue undoes the uint32 wrap the game itself writes for a
        // balance over ~2.1 billion - see its doc comment. No-op for the
        // vast majority of saves that never get that high.
        UnitsBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0);
        NanitesBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0);
        QuicksilverBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0);

        _suppressChangeEvents = false;
    }

    // ToRawValue re-wraps a displayed balance back to the exact bit pattern
    // the game itself already uses once it's over ~2.1 billion, so staged
    // edits round-trip the same way the game's own save data already does -
    // see NmsPlayerStateData.ToDisplayValue's doc comment.
    private void StageUnits(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit(NmsPlayerStateData.ToRawValue((long)value), NmsPlayerStateData.UnitsPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void StageNanites(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit(NmsPlayerStateData.ToRawValue((long)value), NmsPlayerStateData.NanitesPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void StageQuicksilver(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit(NmsPlayerStateData.ToRawValue((long)value), NmsPlayerStateData.QuicksilverPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnitsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StageUnits(args.NewValue);
    private void UnitsBox_LostFocus(object sender, RoutedEventArgs e) => StageUnits(UnitsBox.Value);

    private void NanitesBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StageNanites(args.NewValue);
    private void NanitesBox_LostFocus(object sender, RoutedEventArgs e) => StageNanites(NanitesBox.Value);

    private void QuicksilverBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StageQuicksilver(args.NewValue);
    private void QuicksilverBox_LostFocus(object sender, RoutedEventArgs e) => StageQuicksilver(QuicksilverBox.Value);

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEdit(NmsPlayerStateData.UnitsPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.NanitesPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.QuicksilverPath);
        LoadValues();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
