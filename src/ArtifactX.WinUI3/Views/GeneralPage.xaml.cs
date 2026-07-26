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
        LoadValues();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadValues);

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
