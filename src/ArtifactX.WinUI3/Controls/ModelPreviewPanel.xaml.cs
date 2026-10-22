using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class ModelPreviewPanel : UserControl
{
    private int _version;

    public ModelPreviewPanel() => InitializeComponent();

    /// <summary>Half a turn (π) for models whose bow points along -Z, like freighters.</summary>
    public double StartYawOffset
    {
        get => Preview.StartYawOffset;
        set => Preview.StartYawOffset = (float)value;
    }

    /// <summary>What to call the model in messages ("freighter").</summary>
    public string Subject { get; set; } = "model";

    /// <summary>Builds and shows the model for a scene and seed ("0x2B2F...", null = the scene's
    /// default parts). A newer call replaces one still building.</summary>
    /// <param name="customColours">Colours the player picked in game, from the save's customisation
    /// entry (StoredColours) - shown painted when there are any, else as a hologram.</param>
    public async Task ShowAsync(string? scene, string? seedText, Dictionary<(string, string), float[]>? customColours = null)
    {
        int version = ++_version;
        if (string.IsNullOrEmpty(scene) || GameDataService.Game is not { } game)
        {
            Preview.ShowMessage(string.IsNullOrEmpty(scene) ? $"No {Subject} model in this save." : "Update your game data (Settings › Game data) to see the model.");
            NoteTxt.Text = "";
            return;
        }
        Preview.ShowMessage($"Building this {Subject}'s model from your game files...");
        NoteTxt.Text = "";
        bool hasData = await ShipPartsService.EnsureLoadedAsync();
        await ShipPartsService.EnsureLookAsync();
        var models = await ModelPreviewService.ModelsAsync(game);
        if (version != _version) return;
        if (!hasData || models is null)
        {
            Preview.ShowMessage("This model couldn't be read from your game files.");
            return;
        }

        ulong seed = ShipPartsService.TryParseSeed(seedText, out ulong s) ? s : 0;
        var root = ShipPartsService.LoadTree(ShipParts.DescriptorFor(scene));
        var options = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ModelPreviewService.AllOptionIds(root, ShipPartsService.LoadTree, StringComparer.Ordinal);
        var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ShipParts.Generate(scene, seed, ShipPartsService.LoadTree).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        // A freighter is ~280,000 triangles - built off the UI thread.
        var mesh = await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _version) return;
        if (mesh is null)
        {
            Preview.ShowMessage("This model couldn't be read from your game files.");
            return;
        }
        if (customColours is { Count: > 0 })
        {
            var textures = await ModelPreviewService.PaintAsync(models, mesh, ModelPreviewService.StoredColourLookup(customColours), seed);
            if (version != _version) return;
            Preview.Show(mesh, material => ShipPartsService.MaterialColour(material, ShipPartsService.Look), m => textures.GetValueOrDefault(m));
            NoteTxt.Text = $"This {Subject}'s own shape in the colours you picked in game. Drag to turn.";
            return;
        }
        Preview.Show(mesh, material => ShipPartsService.MaterialColour(material, ShipPartsService.Look), hologram: true);
        NoteTxt.Text = $"Shape scan - the parts are this {Subject}'s own, from its seed and your game files. Its colours show once you pick custom colours for it in game (Customisation). Drag to turn.";
    }
}
