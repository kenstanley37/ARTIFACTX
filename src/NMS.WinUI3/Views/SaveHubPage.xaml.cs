using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using NMS.Core.NmsModels;
using NMS.WinUI3.Models;
using NMS.WinUI3.Services;
using System;
using System.Diagnostics;

namespace NMS.WinUI3.Views;

public sealed partial class SaveHubPage : Page
{
    public SaveHubPage()
    {
        InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;

        Debug.WriteLine("[SaveHubPage] Constructor: Page initialized and event subscribed.");
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e)
    {
        Debug.WriteLine("[SaveHubPage] ActiveSessionChanged FIRED.");

        this.DispatcherQueue.TryEnqueue(() =>
        {
            string? rawJson = SaveSessionManager.GetRawData();

            Debug.WriteLine($"[SaveHubPage] Raw JSON null? {rawJson == null}");
            Debug.WriteLine($"[SaveHubPage] Raw JSON length: {rawJson?.Length}");

            if (string.IsNullOrEmpty(rawJson))
            {
                Debug.WriteLine("[SaveHubPage] Raw JSON empty — aborting.");
                ResetDisplayTokens();
                return;
            }

            Debug.WriteLine("[SaveHubPage] JSON Preview:");
            Debug.WriteLine(rawJson.Substring(0, Math.Min(300, rawJson.Length)));

            try
            {
                var save = JsonConvert.DeserializeObject<NmsSaveFile>(rawJson);

                Debug.WriteLine($"[SaveHubPage] Deserialization result null? {save == null}");

                //
                // --- PRIMARY PATH: CurrencyData (flat NmsCurrencyState) ---
                //
                if (save?.CurrencyData != null)
                {
                    var stats = save.CurrencyData;

                    InspectorUnits.Text = $"{stats.Units:N0} UNITS";
                    InspectorNanites.Text = $"{stats.Nanites:N0} NANITES";
                    InspectorQuicksilver.Text = $"{stats.Quicksilver:N0} QUICKSILVER";

                    return;
                }

                Debug.WriteLine("[SaveHubPage] CurrencyData was NULL.");
                ResetDisplayTokens();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[SaveHubPage] JSON Parsing Exception: {ex.Message}");
                ResetDisplayTokens();
            }
        });
    }

    public void MasterSlotsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MasterSlotsListView.SelectedItem is SaveSlotOverview selectedSlot)
        {
            Debug.WriteLine($"[SaveHubPage] Slot selected: {selectedSlot.SlotId}");
            SaveSessionManager.LoadActiveSessionContextAsync(selectedSlot.OriginalFile).ConfigureAwait(false);
        }
    }

    public void BranchFilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BranchFilesListView.SelectedItem is SaveSlotOverview selectedBranch)
        {
            Debug.WriteLine($"[SaveHubPage] Branch selected: {selectedBranch.OriginalFile}");
            SaveSessionManager.LoadActiveSessionContextAsync(selectedBranch.OriginalFile).ConfigureAwait(false);
        }
    }

    public void ChangeDirectoryBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Debug.WriteLine("[SaveHubPage] ChangeDirectoryBtn clicked.");
        //SaveWorkspaceService.PromptForNewDirectoryAsync().ConfigureAwait(false);
    }

    private void ResetDisplayTokens()
    {
        Debug.WriteLine("[SaveHubPage] ResetDisplayTokens invoked.");

        InspectorUnits.Text = "0 UNITS";
        InspectorNanites.Text = "0 NANITES";
        InspectorQuicksilver.Text = "0 QUICKSILVER";
    }
}
