using Microsoft.UI.Xaml.Controls;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using System;

namespace NMS.WinUI3.Controls;

public sealed partial class AppTitleBar : UserControl
{
    public AppTitleBar()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        Refresh();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ActiveSaveTxt.Text = string.Empty;
            ResetDisplayTokens();
            return;
        }

        ActiveSaveTxt.Text = $"•  {SaveSessionManager.ActiveLabel}";

        long units = SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0;
        long nanites = SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0;
        long quicksilver = SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0;

        UnitsTxt.Text = $"{units:N0} UNITS";
        NanitesTxt.Text = $"{nanites:N0} NANITES";
        QuicksilverTxt.Text = $"{quicksilver:N0} QUICKSILVER";
    }

    private void ResetDisplayTokens()
    {
        UnitsTxt.Text = "0 UNITS";
        NanitesTxt.Text = "0 NANITES";
        QuicksilverTxt.Text = "0 QUICKSILVER";
    }
}