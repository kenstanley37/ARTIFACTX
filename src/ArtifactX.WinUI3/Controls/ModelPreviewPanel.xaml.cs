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
    /// <param name="glowColour">Colour (0xRRGGBB) for engine glow materials - a freighter's engine
    /// flare. Glow parts aren't painted: the paint texture turned them dark grey (2026-10-22).</param>
    /// <param name="built">Built ship parts, colours and styles, for fabricated starships.</param>
    public async Task ShowAsync(
        string? scene,
        string? seedText,
        Dictionary<(string, string), float[]>? customColours = null,
        uint? glowColour = null,
        BuiltLook? built = null)
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
            : ShipParts.Generate(scene, seed, ShipPartsService.LoadTree, ShipPartsService.StoredOptions(built)).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        // A freighter is ~280,000 triangles - built off the UI thread.
        var mesh = await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _version) return;
        if (mesh is null)
        {
            Preview.ShowMessage("This model couldn't be read from your game files.");
            return;
        }
        uint Colour(string material) => glowColour is uint glow && IsGlow(material) ? glow : ShipPartsService.MaterialColour(material, ShipPartsService.Look);
        if (built is { Colours.Count: > 0 })
        {
            var textures = await ModelPreviewService.PaintAsync(models, mesh, ModelPreviewService.StoredColourLookup(built.Colours), seed, built.Styles);
            if (version != _version) return;
            Preview.Show(mesh, Colour, m => IsGlow(m) ? null : textures.GetValueOrDefault(m));
            NoteTxt.Text = $"Shape from your game files, in the paint saved for this {Subject} - drag to turn.";
            return;
        }
        if (customColours is { Count: > 0 })
        {
            var textures = await ModelPreviewService.PaintAsync(models, mesh, ModelPreviewService.StoredColourLookup(customColours), seed);
            if (version != _version) return;
            Preview.Show(mesh, Colour, m => IsGlow(m) ? null : textures.GetValueOrDefault(m));
            NoteTxt.Text = $"This {Subject}'s own shape in the colours you picked in game. Drag to turn.";
            return;
        }
        if (ShipBaseService.IsCorvetteModel(scene))
        {
            Preview.Show(mesh, Colour, hologram: true);
            NoteTxt.Text = "Corvette base chassis. Custom modular parts (cockpit, wings, thrusters) are placed in game — full modular 3D assembly in development. Drag to turn.";
            return;
        }
        if (await ModelPreviewService.FixedLookAsync(models, mesh) is { } own)
        {
            if (version != _version) return;
            Preview.Show(mesh, Colour, m => IsGlow(m) ? null : own.GetValueOrDefault(m));
            NoteTxt.Text = "Shape and colours from your game files - drag to turn.";
            return;
        }
        if (await ModelPreviewService.SeedColoursAsync(seed, rawSeed: true) is { } seedColours)
        {
            var textures = await ModelPreviewService.PaintAsync(models, mesh, seedColours, seed);
            if (version != _version) return;
            Preview.Show(mesh, Colour, m => IsGlow(m) ? null : textures.GetValueOrDefault(m));
            NoteTxt.Text = "Shape and colours from your game files and the ship's seed - drag to turn.";
            return;
        }
        Preview.Show(mesh, Colour, hologram: true);
        NoteTxt.Text = $"Shape scan - the parts are this {Subject}'s own, from its seed and your game files. Drag to turn.";
    }

    /// <summary>Engine glow: ENGINEGLOW materials, and a Dreadnought's thruster glow
    /// (PIRATEFREIGHTERTHRUSTER0/REDGLOWGRAD_MAT).</summary>
    private static bool IsGlow(string material)
    {
        string name = System.IO.Path.GetFileName(material);
        return name.Contains("ENGINEGLOW", StringComparison.OrdinalIgnoreCase)
            || (material.Contains("THRUSTER", StringComparison.OrdinalIgnoreCase) && name.Contains("GLOW", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Builds and shows a creature or companion model from its rig and chosen descriptors.</summary>
    public async Task ShowCreatureAsync(string rigId, IEnumerable<string>? chosenDescriptors = null, bool hologram = false)
    {
        int version = ++_version;
        if (GameDataService.Game is not { } game)
        {
            Preview.ShowMessage("Update your game data (Settings › Game data) to see the model.");
            NoteTxt.Text = "";
            return;
        }
        Preview.ShowMessage($"Building this {Subject}'s model from your game files...");
        NoteTxt.Text = "";

        var mesh = await CreatureModelService.BuildCreatureMeshAsync(game, rigId, chosenDescriptors);
        if (version != _version) return;
        if (mesh is null)
        {
            Preview.ShowMessage($"This {Subject}'s 3D model couldn't be built from your game files.");
            return;
        }

        uint Colour(string material) => CreatureModelService.CreatureMaterialColour(material);
        Preview.Show(mesh, Colour, hologram: hologram);
        NoteTxt.Text = hologram
            ? $"Hologram scan — shape and anatomy from your game files. Drag to turn."
            : $"Shape and anatomy from your game files and saved traits — drag to turn.";
    }
}
