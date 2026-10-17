using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Paint mode (2026-10-17): Starships' "Paint this ship" opens the designer on one
/// of the player's own ships. Only the Paint column is live; the ship keeps its
/// parts, seed and name, and Repaint writes just its customisation entry (the
/// same FCx / T>1 / Aak the station builder writes).
///
/// Found and bought ships can't be repainted in game, but an in-game test showed
/// the game uses paint written this way: a found Hauler (Ashes of the Suitaka) took
/// purple/yellow/white and Titan Stripes, kept its shape, and the game's own later
/// save kept the paint. Its entry was empty before (SMP [] - the parts come from
/// the seed), so Remove paint puts that empty entry back.
/// </summary>
public sealed partial class ShipDesignerPage
{
    private int? _paintShip;
    private string _paintShipName = "";
    // The ship's stored part groups (a station-built ship); empty = parts from its seed.
    private List<string> _paintShipParts = new();
    private JToken? _paintShipSmp;
    private bool _paintShipHasPaint;

    private async Task EnterPaintModeAsync()
    {
        int index = _paintShip!.Value;
        var ship = SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath.Append(index.ToString(CultureInfo.InvariantCulture)).ToArray()) as JObject;
        string scene = ship?["NTx"]?["93M"]?.Value<string>() ?? "";
        var type = _types.FirstOrDefault(t => string.Equals(t.Scene.Replace('\\', '/'), scene.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (ship is null || type is null)
        {
            _paintShip = null;
            await Message("This ship can't be painted here", "Only Fighters, Haulers, Explorers and Solars can be painted - the ship types a space station's fabricator builds.");
            return;
        }

        ulong seed = TryParseSeed(index) ?? 0;
        string name = ship["NKm"]?.Value<string>() ?? "";
        _paintShipName = name.Length > 0 ? name : await MultiToolKinds.ShipNameAsync(seed, scene) ?? $"Starship {index + 1}";
        var data = SaveSessionManager.GetValue(CustomisationPath(index).Append("wnR").ToArray()) as JObject;
        _paintShipSmp = data?["SMP"]?.DeepClone();
        _paintShipParts = (data?["SMP"] as JArray)?.Select(t => t.ToString().TrimStart('^')).Where(t => t.Length > 0).ToList() ?? new();
        _paintShipHasPaint = (data?["Aak"] as JArray)?.Count > 0;

        SetDesign(null);
        SelectType(type);
        _seed = seed;
        if (data is not null && _paintShipHasPaint) PickCurrentPaint(data);

        TypeSection.Visibility = PartsSection.Visibility = PartSearchBox.Visibility = PartsScroll.Visibility = Visibility.Collapsed;
        PaintPartsNote.Visibility = Visibility.Visible;
        PaintPartsTxt.Text = (_paintShipParts.Count > 0
                ? "This ship keeps the parts it was built with."
                : "This ship keeps its own parts - the ones its seed gives.")
            + " Only its paint changes. The game itself doesn't let you repaint a ship you found or bought; this changes its colours in your save.";
        DesignNameTxt.Text = "Repainting your ship · its parts and name stay as they are";
        NewDesignBtn.Visibility = NewNameBtn.Visibility = Visibility.Collapsed;
        DesignActions.Visibility = SaveActions.Visibility = Visibility.Collapsed;
        PaintActions.Visibility = Visibility.Visible;
        RemovePaintBtn.Visibility = _paintShipHasPaint && _paintShipParts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateWriteButtons();
        ShowPaint();
        RefreshName();
        RefreshPreview();
    }

    /// <summary>Starts from the ship's own paint: its style, and for each colour the
    /// nearest swatch to what the save holds.</summary>
    private void PickCurrentPaint(JObject data)
    {
        string option = (data["T>1"] as JArray)?.FirstOrDefault()?["=Cv"]?.ToString().TrimStart('^') ?? "";
        if ((_styles.GetValueOrDefault(_type!.StyleGroup) ?? new()).FirstOrDefault(s => s.Option == option) is { } style)
        {
            _style = style;
            ResetColours();
        }
        var colours = CurrentColours();
        var swatches = colours is null ? new() : _swatches.GetValueOrDefault(colours.Swatches) ?? new();
        var stored = (data["Aak"] as JArray ?? new JArray()).OfType<JObject>().ToList();
        foreach (var group in colours?.Groups ?? new())
        {
            if (group.Slots.FirstOrDefault(s => s.Length > 1) is not { } slot) continue;
            var entry = stored.FirstOrDefault(e => e["RVl"]?["RVl"]?.ToString() == slot[0] && e["RVl"]?["Ty="]?.ToString() == slot[1]);
            if (entry?["xEg"] is not JArray rgb || rgb.Count < 3 || swatches.Count == 0) continue;
            float r = rgb[0].Value<float>(), g = rgb[1].Value<float>(), b = rgb[2].Value<float>();
            _colourPicks[group.Title] = Enumerable.Range(0, swatches.Count).MinBy(i =>
                MathF.Pow(swatches[i].Rgb[0] - r, 2) + MathF.Pow(swatches[i].Rgb[1] - g, 2) + MathF.Pow(swatches[i].Rgb[2] - b, 2));
        }
    }

    private async void RepaintBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_paintShip is not int index || _type is null || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        var entry = CustomisationEntry();
        // The parts stay the ship's own: a built ship's list as it was, otherwise none.
        ((JObject)entry["wnR"]!)["SMP"] = _paintShipSmp?.DeepClone() ?? new JArray();
        SaveSessionManager.StageEdit(entry, CustomisationPath(index));
        _paintShipHasPaint = true;
        RemovePaintBtn.Visibility = _paintShipParts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        await Message("Repainted", $"\"{_paintShipName}\" has its new paint. Save from the title bar to write it to your save, then load the save in game.");
    }

    private async void RemovePaintBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_paintShip is not int index || !SaveSessionManager.IsSaveLoaded || GameProcessMonitorService.IsGameRunning) return;
        if (!await Confirm($"Remove the paint from \"{_paintShipName}\"?",
                "It goes back to the colours it had when you found or bought it. Nothing is written to your save until you Save.", "Remove paint")) return;
        SaveSessionManager.StageEdit(new JObject
        {
            ["VFd"] = "^",
            ["wnR"] = new JObject
            {
                ["SMP"] = new JArray(), ["FCx"] = "^", ["Aak"] = new JArray(), ["T>1"] = new JArray(), ["gsg"] = new JArray(), ["unY"] = 1.0,
            },
        }, CustomisationPath(index));
        _paintShipHasPaint = false;
        RemovePaintBtn.Visibility = Visibility.Collapsed;
        await Message("Paint removed", "Save from the title bar to write it to your save.");
    }

    private void ExitPaintBtn_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(ShipDesignerPage));

    /// <summary>Paint mode: a saved design lends its paint style and colours.</summary>
    private async void UseDesignPaint(ShipDesign design)
    {
        if (_type is null) return;
        var style = (_styles.GetValueOrDefault(_type.StyleGroup) ?? new()).FirstOrDefault(s => s.Option == design.Style);
        if (design.Style is not null && style is null)
        {
            await Message("That paint style isn't offered here", $"\"{design.Name}\" uses a paint style this ship type doesn't have.");
            return;
        }
        _style = style ?? _style;
        ResetColours();
        foreach (var (group, swatch) in design.Colours)
            if (_colourPicks.ContainsKey(group)) _colourPicks[group] = swatch;
        ShowPaint();
        RefreshPreview();
    }
}
