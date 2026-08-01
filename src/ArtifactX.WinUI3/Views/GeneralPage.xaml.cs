using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System;

namespace ArtifactX.WinUI3.Views;

public sealed partial class GeneralPage : Page
{
    private bool _suppressChangeEvents;

    public GeneralPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Unloaded += Page_Unloaded;
        LoadValues();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadValues);

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

        UnitsBox.Value = SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0;
        NanitesBox.Value = SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0;
        QuicksilverBox.Value = SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0;

        _suppressChangeEvents = false;
    }

    private void StageUnits(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit((long)value, NmsPlayerStateData.UnitsPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void StageNanites(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit((long)value, NmsPlayerStateData.NanitesPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void StageQuicksilver(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit((long)value, NmsPlayerStateData.QuicksilverPath);
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
