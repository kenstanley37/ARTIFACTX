using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The selected ship's parts (PartsPanel, ShipPartsService) and its 3D preview, built
/// from the player's own game files.
/// </summary>
public sealed partial class ShipsPage
{
    private int _previewVersion;

    private string? CurrentScene() => _selectedIndex < 0 ? null
        : SaveSessionManager.GetValue("vLc", "6f=", "@Cs", _selectedIndex.ToString(CultureInfo.InvariantCulture), "NTx", "93M")?.ToString();

    private bool IsCorvetteSelected() =>
        _ships.FirstOrDefault(s => s.Index == _selectedIndex)?.IsCorvette ?? false;

    /// <summary>Rebuilds the parts list and preview for the selected ship's type and the seed in the box.</summary>
    private async void RefreshShipParts()
    {
        ShowFoldedName(); // the closed band names the ship shown
        PartsPanel.ViewModel.Clear();
        // Hide the last ship's model (and drop a build still running for it), or a ship
        // that shows no parts kept the previous ship's model (2026-10-15).
        int refresh = ++_previewVersion;
        ShipPreviewGroup.Visibility = Visibility.Collapsed;
        string? scene = CurrentScene();
        PaintShipBtn.Visibility = scene is not null && ShipPartsService.IsPaintable(scene) ? Visibility.Visible : Visibility.Collapsed;
        if (scene is null) return;

        bool hasData = await ShipPartsService.EnsureLoadedAsync();
        // A newer refresh started while this one waited (switching ships fires two):
        // let that one fill the panel, or the parts list showed twice (2026-10-17).
        if (refresh != _previewVersion) return;
        if (!hasData)
        {
            PartsPanel.ViewModel.ShowMessage("Update your game data (Settings › Game data) to see which parts a seed gives.");
            return;
        }

        var root = ShipPartsService.LoadTree(ShipParts.DescriptorFor(scene));
        bool hasSeed = ShipPartsService.TryParseSeed(SeedBox.Text, out ulong seed);
        var built = ShipPartsService.BuiltLookOf(_selectedIndex);
        // Every ship type has a model, parts or not - except a corvette, whose look is
        // the modules built onto it, stored apart from the ship.
        if (!IsCorvetteSelected() && (root is null || hasSeed))
        {
            await ShipPartsService.EnsureLookAsync();
            if (refresh != _previewVersion) return;
            ShowPreview(scene, hasSeed ? seed : 0, root, built);
        }
        PartsPanel.ViewModel.Show(scene, root, hasSeed ? seed : null, built);
    }

    /// <summary>A found seed's Use: stages it; Seed_Changed refreshes the parts.</summary>
    private void PartsPanel_SeedChosen(object? sender, string seed)
    {
        if (_selectedIndex >= 0) SeedBox.Set(seed);
    }

    /// <summary>The 3D preview: the seed's parts built from the player's own game files
    /// (ShipModels). A ship built at a station (or repainted) shows its saved paint;
    /// found and bought ships show as a hologram scan.</summary>
    private async void ShowPreview(string scene, ulong seed, IReadOnlyList<PartGroup>? root, BuiltLook? built = null)
    {
        int version = ++_previewVersion;
        if (GameDataService.Game is not { } game) { ShipPreviewGroup.Visibility = Visibility.Collapsed; return; }
        // Cleared first: the previous ship stayed on screen while this one built (2026-10-17).
        ShipPreviewView.ShowMessage("Building this ship's model from your game files...");
        ShipPreviewNote.Text = "";
        ShipPreviewGroup.Visibility = Visibility.Visible;
        var models = await ModelPreviewService.ModelsAsync(game);
        if (models is null || version != _previewVersion) return;
        // A fixed-look type has no part options: every mesh in its scene is kept.
        // Option ids compare exactly here, as the original look code does (ShipLook.Generate).
        var options = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ModelPreviewService.AllOptionIds(root, ShipPartsService.LoadTree, StringComparer.Ordinal);
        var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ShipParts.Generate(scene, seed, ShipPartsService.LoadTree, ShipPartsService.StoredOptions(built)).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var mesh = await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _previewVersion) return;
        var look = ShipPartsService.Look;
        // A built ship's colours are in the save, so it is shown as the game shows it.
        if (mesh is not null && built is { Colours.Count: > 0 })
        {
            var textures = await ModelPreviewService.PaintAsync(models, mesh, ModelPreviewService.StoredColourLookup(built.Colours), seed, built.Styles);
            if (version != _previewVersion) return;
            ShipPreviewView.Show(mesh, material => ShipPartsService.MaterialColour(material, look), m => textures.GetValueOrDefault(m));
            ShipPreviewNote.Text = "Shape from your game files, in the paint saved for this ship - drag to turn.";
            return;
        }
        // Found and bought ships: shown as a hologram for now. Test 2026-10-17 (Colour
        // Test 1-5 on Slot 8, Gamada, Ashes - seeds in the procedural-ships notes): the
        // palette table from the seed ITSELF (SeedColoursAsync rawSeed) holds the right
        // colours, but which slot lands on the hull comes from texture-option picks we
        // don't reproduce yet (Hauler blue hull came out red, Explorers' black hulls
        // white/olive) - 2 of 6 right is too few to show as the ship's real colours.
        // A fixed-look ship (Boundary Herald, Starborn Runner, The Wraith...) keeps its
        // colours in its own textures, so it can be shown as the game shows it.
        if (mesh is not null && await ModelPreviewService.FixedLookAsync(models, mesh) is { } own)
        {
            if (version != _previewVersion) return;
            ShipPreviewView.Show(mesh, material => ShipPartsService.MaterialColour(material, look), m => own.GetValueOrDefault(m));
            ShipPreviewNote.Text = "Shape and colours from your game files - drag to turn.";
            return;
        }
        if (version != _previewVersion) return;
        // A hologram, not grey - plain grey read as a broken paint job (2026-10-17).
        if (mesh is null) ShipPreviewView.ShowMessage("This ship's model couldn't be read from your game files.");
        else ShipPreviewView.Show(mesh, material => ShipPartsService.MaterialColour(material, look), hologram: true);
        ShipPreviewNote.Text = mesh is null ? ""
            : "Shape scan - the parts are this ship's own, but its colours can't be shown yet (only ships built at a station or painted). Drag to turn.";
    }

    private void PaintShipBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex >= 0) Frame.Navigate(typeof(ShipDesignerPage), _selectedIndex);
    }
}
