using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The selected tool in 3D (2026-10-16), built from the player's own game
/// files the way Starships builds a ship: its model, with the parts its seed
/// picks (SuJ[i].@EL[1], the same pick as MultiToolKinds) - every model has a
/// part tree, the staffs and Royal tools included. Neutral greys: a seed's
/// colours aren't known yet, for tools as for ships.
/// </summary>
public sealed partial class MultiToolPage
{
    private int _previewVersion;

    private async void ShowToolPreview()
    {
        int version = ++_previewVersion;
        string scene = _selectedIndex < 0 ? ""
            : SaveSessionManager.GetValue("vLc", "6f=", "SuJ", _selectedIndex.ToString(CultureInfo.InvariantCulture), "NTx", "93M")?.Value<string>() ?? "";
        if (string.IsNullOrEmpty(scene) || GameDataService.Game is not { } game)
        {
            ToolPreviewGroup.Visibility = Visibility.Collapsed;
            return;
        }
        ToolPreviewGroup.Visibility = Visibility.Visible;
        ToolPreviewNote.Text = "Building the model from your game files...";

        var models = await ModelPreviewService.ModelsAsync(game);
        await MultiToolKinds.EnsureTreesAsync();
        if (version != _previewVersion) return;

        ulong seed = ParseSeed(SaveSessionManager.GetValue("vLc", "6f=", "SuJ", _selectedIndex.ToString(CultureInfo.InvariantCulture), "@EL", "1")?.Value<string>());
        var root = MultiToolKinds.Load(ShipParts.DescriptorFor(scene));
        // No part tree: a fixed-look model, every mesh in its scene is kept.
        var options = root is null ? new() : ModelPreviewService.AllOptionIds(root, MultiToolKinds.Load, StringComparer.Ordinal);
        var chosen = root is null ? new() : ShipParts.Generate(scene, seed, MultiToolKinds.Load).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var mesh = models is null ? null : await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _previewVersion) return;

        ToolPreviewView.Show(mesh, MaterialColour);
        ToolPreviewNote.Text = mesh is null
            ? "This tool's model couldn't be read from your game files."
            : "Shape from your game files - drag to turn. Shown in neutral grey: the colours a seed gives can't be predicted yet.";
    }

    private static ulong ParseSeed(string? text)
    {
        text = text?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong seed) ? seed : 0;
    }

    /// <summary>Greys by material name, so the parts read apart: glass and lit
    /// parts by what they are, dark trims darker.</summary>
    private static uint MaterialColour(string material)
    {
        string name = System.IO.Path.GetFileName(material).ToUpperInvariant();
        static uint Rgb(int r, int g, int b) => (uint)(r << 16 | g << 8 | b);
        if (Regex.IsMatch(name, "GLASS|SCREEN|LENS|TRANS_")) return Rgb(32, 62, 82);
        if (Regex.IsMatch(name, "LIGHT|GLOW|EMIS|LASER")) return Rgb(255, 214, 140);
        if (Regex.IsMatch(name, "RUBBER|DARK|BLACK|GRIP|GRILL|VENT")) return Rgb(46, 48, 53);
        if (Regex.IsMatch(name, "TRIM|METAL|CHROME")) return Rgb(122, 127, 136);
        return Rgb(158, 162, 170);
    }
}
