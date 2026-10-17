using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Writing a design into the save (2026-10-16), the way the game stores a ship
/// built at a station (in-game build diff, "The Eagle of Beauty"):
/// - the ship entry: model (NTx.93M), a new seed in NTx.@EL and pMa.@EL, no name
///   of its own (NKm "", so the game names it from the seed);
/// - its customisation entry (l:j[3 + slot], slots 6-11 at 17+): the parts by
///   item id (SMP "^FIGHT_COCKAA"), the swatch set (FCx "^SHIP_METALLIC"), the
///   style (T>1 [{@6c "^SHIP_FIGHT", =Cv "^STEALTH"}]) and all 66 palettes x 5
///   colours (Aak). The builder fills the slots the player doesn't pick from the
///   swatch set; most were the dark metal grey (0.351), which is used here for all.
/// Edits are staged like every other page: nothing is written until Save.
/// </summary>
public sealed partial class ShipDesignerPage
{
    private static readonly string[] Alts = { "Primary", "Alternative1", "Alternative2", "Alternative3", "Alternative4" };

    /// <summary>The ships a design can be applied to: regular ships only - a corvette's
    /// look is its build, and living and Sentinel ships have no builder.</summary>
    private static List<(int Index, string Scene, string Name)> RegularShips()
    {
        var result = new List<(int, string, string)>();
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is not JArray ships) return result;
        for (int i = 0; i < ships.Count; i++)
        {
            string scene = ships[i]?["NTx"]?["93M"]?.Value<string>() ?? "";
            string upper = scene.ToUpperInvariant();
            if (scene.Length == 0 || ShipBaseService.IsCorvetteModel(scene) || upper.Contains("/BIOPARTS/")
                || upper.Contains("/SENTINELSHIP/") || upper.Contains("/VINTAGEINTERCEPTOR.")) continue;
            result.Add((i, scene, ships[i]?["NKm"]?.Value<string>() ?? ""));
        }
        return result;
    }

    private async void AddNewBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null || !SaveSessionManager.IsSaveLoaded) return;
        var owned = RegularShips();
        int active = SaveSessionManager.GetValue(NmsInventoryContainer.ActiveShipIndexPath)?.Value<int>() ?? -1;
        // The new ship starts as a copy of one you own (its tech, so it flies straight away).
        int skeleton = owned.Any(s => s.Index == active) ? active : owned.FirstOrDefault().Index;
        if (owned.Count == 0 || NewFromTemplateService.FirstEmptyShipSlot() is not int slot)
        {
            await Message("No room for another Starship", owned.Count == 0
                ? "This save doesn't have a starship to start from yet. Get one in game first."
                : $"All {NewFromTemplateService.ShipSlotCount} Starship slots are in use. Sell or scrap one in game, or delete one on the Starships page, then try again.");
            return;
        }
        if (!await Confirm($"Add \"{ShipNameTxt.Text}\" as a new Starship?",
                $"It goes in empty slot {slot + 1} with these parts and paint. It starts with the technology of one of your ships so it can fly, and an empty cargo hold. Nothing is written to your save until you Save.",
                "Add")) return;

        var ship = (JObject)SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath.Append(skeleton.ToString()).ToArray())!.DeepClone();
        ship["NKm"] = "";
        WriteLook(ship);
        var cargo = (JObject)ship[";l5"]!;
        cargo[":No"] = new JArray();
        if (ship["gan"] is JObject legacyCargo) legacyCargo[":No"] = new JArray();
        if (ship["FA8"] is JArray) ship["FA8"] = new JArray();
        // The reactor sets the class, as at a station.
        if (ReactorClass() is string cls)
            foreach (var store in new[] { ship["PMT"], ship[";l5"] }.OfType<JObject>()) store["B@N"] = new JObject { ["1o6"] = cls };
        SaveSessionManager.StageEdit(ship, NmsInventoryContainer.ShipArrayPath.Append(slot.ToString()).ToArray());
        SaveSessionManager.StageEdit(CustomisationEntry(), CustomisationPath(slot));
        await Message("Added", $"\"{ShipNameTxt.Text}\" is in slot {slot + 1}. Save from the title bar to write it to your save, then load the save in game.");
        _seed = NewSeed();
        RefreshName();
    }

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null || !SaveSessionManager.IsSaveLoaded) return;
        var owned = RegularShips();
        if (owned.Count == 0) { await Message("No ship to apply it to", "This save has no starship the design can go on."); return; }

        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 360 };
        foreach (var (index, scene, name) in owned)
        {
            string label = name.Length > 0 ? name
                : TryParseSeed(index) is ulong seed && await MultiToolKinds.ShipNameAsync(seed, scene) is { } gameName ? gameName
                : $"Starship {index + 1}";
            list.Items.Add(new ListViewItem { Content = label, Tag = index });
        }
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "The ship gets this design's type, parts, paint and a new seed. Its technology, cargo, class and stats stay as they are, and so does a name you gave it.",
        });
        panel.Children.Add(list);
        var dialog = new ContentDialog
        {
            Title = "Apply the design to which ship?",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot,
        };
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedItem is not null;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedItem is not ListViewItem { Tag: int target }) return;

        var path = NmsInventoryContainer.ShipArrayPath.Append(target.ToString()).ToArray();
        var ship = (JObject)SaveSessionManager.GetValue(path)!.DeepClone();
        WriteLook(ship);
        SaveSessionManager.StageEdit(ship, path);
        SaveSessionManager.StageEdit(CustomisationEntry(), CustomisationPath(target));
        await Message("Applied", "Save from the title bar to write it to your save, then load the save in game.");
        _seed = NewSeed();
        RefreshName();
    }

    /// <summary>Model and seed, as the station builder writes them.</summary>
    private void WriteLook(JObject ship)
    {
        string seed = "0x" + _seed.ToString("X16", CultureInfo.InvariantCulture);
        ship["NTx"]!["93M"] = _type!.Scene;
        ship["NTx"]!["@EL"] = new JArray(true, seed);
        if (ship["pMa"] is JObject layout) layout["@EL"] = new JArray(true, seed);
    }

    /// <summary>The customisation entry for the design (see the class notes).</summary>
    private JObject CustomisationEntry()
    {
        var data = CurrentColours();
        var colour = DesignColours();
        var aak = new JArray();
        foreach (var palette in Enum.GetNames(typeof(libMBIN.NMS.Toolkit.TkPaletteTexture.PaletteEnum)).Take(66))
            foreach (var alt in Alts)
            {
                var rgb = colour(palette, alt) ?? new[] { 0.351f, 0.351f, 0.351f };
                aak.Add(new JObject
                {
                    ["RVl"] = new JObject { ["RVl"] = palette, ["Ty="] = alt, ["3ZH"] = -1 },
                    ["xEg"] = new JArray(rgb[0], rgb[1], rgb[2], 1.0f),
                });
            }
        var styles = new JArray();
        if (_style is not null)
            styles.Add(new JObject { ["@6c"] = "^" + _type!.StyleGroup, ["=Cv"] = "^" + _style.Option });
        return new JObject
        {
            ["VFd"] = "^",
            ["wnR"] = new JObject
            {
                ["SMP"] = new JArray(_type!.Slots.Select(s => _parts.GetValueOrDefault(s.Id)).OfType<string>()
                    // The reactor is a crafting input, not a look part: the game didn't store it.
                    .Where(item => !IsReactor(item)).Select(item => (object)("^" + item)).ToArray()),
                ["FCx"] = "^" + (data?.Swatches ?? "SHIP"),
                ["Aak"] = aak,
                ["T>1"] = styles,
                ["gsg"] = new JArray(),
                ["unY"] = 1.0,
            },
        };
    }

    private bool IsReactor(string item) =>
        _type!.Slots.Any(s => s.Id.EndsWith("_CORE", StringComparison.Ordinal) && s.Items.Any(i => i.Item == item));

    private string? ReactorClass() =>
        _type!.Slots.Where(s => s.Id.EndsWith("_CORE", StringComparison.Ordinal))
            .SelectMany(s => s.Items).FirstOrDefault(i => _parts.ContainsValue(i.Item))?.Cls;

    private static string[] CustomisationPath(int slot) =>
        new[] { "vLc", "6f=", "l:j", NewFromTemplateService.ShipCustomisationIndex(slot).ToString(CultureInfo.InvariantCulture) };

    private static ulong? TryParseSeed(int index)
    {
        string text = SaveSessionManager.GetValue(NmsInventoryContainer.ShipModelSeedPath(index))?.Value<string>()?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong seed) ? seed : null;
    }

    private async Task Message(string title, string text) =>
        await new ContentDialog { Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();

    private async Task<bool> Confirm(string title, string text, string action) =>
        await new ContentDialog
        {
            Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = action, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
        }.ShowAsync() == ContentDialogResult.Primary;
}
