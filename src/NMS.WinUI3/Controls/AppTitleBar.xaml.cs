using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using System;
using System.Diagnostics;

namespace NMS.WinUI3.Controls;

public sealed partial class AppTitleBar : UserControl
{
    public AppTitleBar()
    {
        this.InitializeComponent();
        SaveSessionManager.ActiveSessionChanged += OnActiveSessionChanged;

        Debug.WriteLine("[AppTitleBar] Constructor: Title bar initialized and event subscribed.");
    }

    private void OnActiveSessionChanged(object? sender, EventArgs e)
    {
        Debug.WriteLine("[AppTitleBar] ActiveSessionChanged event FIRED.");

        this.DispatcherQueue.TryEnqueue(() =>
        {
            string? rawJson = SaveSessionManager.GetRawData();

            Debug.WriteLine($"[AppTitleBar] Raw JSON null? {rawJson == null}");
            Debug.WriteLine($"[AppTitleBar] Raw JSON length: {rawJson?.Length}");

            if (string.IsNullOrEmpty(rawJson))
            {
                Debug.WriteLine("[AppTitleBar] Raw JSON was empty — aborting.");
                return;
            }

            Debug.WriteLine("[AppTitleBar] JSON Preview:");
            Debug.WriteLine(rawJson.Substring(0, Math.Min(300, rawJson.Length)));

            try
            {
                var save = JsonConvert.DeserializeObject<NmsSaveFile>(rawJson);

                Debug.WriteLine($"[AppTitleBar] Deserialization result null? {save == null}");

                //
                // --- PRIMARY PATH: CurrencyData (shim over vLc) ---
                //
                if (save?.CurrencyData != null)
                {
                    Debug.WriteLine("[AppTitleBar] CurrencyData FOUND.");

                    var stats = save.CurrencyData;

                    Debug.WriteLine($"[AppTitleBar] Currency FOUND: Units={stats.Units}, Nanites={stats.Nanites}, QS={stats.Quicksilver}");

                    UnitsTxt.Text = $"{stats.Units:N0} UNITS";
                    NanitesTxt.Text = $"{stats.Nanites:N0} NANITES";
                    QuicksilverTxt.Text = $"{stats.Quicksilver:N0} QUICKSILVER";
                    return;
                }
                else
                {
                    Debug.WriteLine("[AppTitleBar] CurrencyData is NULL.");
                }

                //
                // --- If currency path fails ---
                //
                Debug.WriteLine("[AppTitleBar] No currency paths succeeded. Resetting display tokens.");
                ResetDisplayTokens();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[AppTitleBar] JSON Parsing Exception: {ex.Message}");
                ResetDisplayTokens();
            }
        });
    }

    private void ResetDisplayTokens()
    {
        Debug.WriteLine("[AppTitleBar] ResetDisplayTokens invoked.");
        UnitsTxt.Text = "0 UNITS";
        NanitesTxt.Text = "0 NANITES";
        QuicksilverTxt.Text = "0 QUICKSILVER";
    }
}
