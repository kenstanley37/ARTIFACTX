using ArtifactX.Core.Procedural;
using ArtifactX.GameData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// What the 3D previews (Starships, Multi-Tool) share: the model reader for
/// the player's game files, and the part ids a model's tree can hold.
/// </summary>
public static class ModelPreviewService
{
    // Indexing every PAK takes a moment, so the reader is made once per session.
    private static Task<ShipModels?>? _models;

    public static Task<ShipModels?> ModelsAsync(GameInstall game) =>
        _models ??= Task.Run(() => { try { return new ShipModels(game); } catch { return (ShipModels?)null; } });

    /// <summary>Every option id reachable from a tree, through nested groups and
    /// the trees of the scenes its options use - which parts the model can have at all.</summary>
    public static HashSet<string> AllOptionIds(IReadOnlyList<PartGroup> root, Func<string, IReadOnlyList<PartGroup>?> loadTree,
        StringComparer comparer)
    {
        var ids = new HashSet<string>(comparer);
        var seenTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(IReadOnlyList<PartGroup> groups)
        {
            foreach (var g in groups)
                foreach (var o in g.Options)
                {
                    ids.Add(o.Id);
                    foreach (var kid in o.Children) Walk(kid);
                    foreach (var scene in o.Scenes)
                    {
                        string path = ShipParts.DescriptorFor(scene);
                        if (seenTrees.Add(path) && loadTree(path) is { } sub) Walk(sub);
                    }
                }
        }
        Walk(root);
        return ids;
    }

    /// <summary>A seed's colours, the game's way (NMS.exe, 2026-10-16): the palette
    /// table is built from the first child seed of the tool's or ship's own seed.
    /// Gives a (palette, slot) colour - ("Paint", "Alternative1") - as RGB 0-1, or null.
    /// Multi-tools only so far: ships don't follow it reliably (see ShipsPage.ShowPreview).</summary>
    public static async Task<Func<string, string, float[]?>?> SeedColoursAsync(ulong seed)
    {
        if (seed == 0 || await CatalogService.GetPalettesAsync() is not { Count: > 0 } palettes) return null;
        var table = ShipLook.PaletteTable(ColourSeed(seed), palettes);
        return (palette, slot) =>
        {
            int p = ShipModels.PaletteIndex(palette), a = ShipModels.SlotIndex(slot);
            return p < 0 || p >= table.Length || table[p] is null || a is < 0 or > 5 ? null : table[p][Math.Min(a, 3)];
        };
    }

    /// <summary>The seed colours come from: the first child seed of the item's own
    /// (two draws of its generator, mixed) - NMS.exe 0x14165d840.</summary>
    public static ulong ColourSeed(ulong seed)
    {
        ulong lo = seed & 0xFFFFFFFF, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo, s1 = (((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        ulong Next() { ulong t = s0 * 0x5A76F899UL + s1; s0 = t & 0xFFFFFFFF; s1 = t >> 32; return s0; }
        ulong a = Next();
        ulong v = (Next() << 32) | a;
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    /// <summary>Each material painted with the given colours (null where it has nothing to paint with).</summary>
    public static Task<Dictionary<string, ModelTexture?>> PaintAsync(ShipModels models, ShipMesh mesh,
        Func<string, string, float[]?> colour, ulong? textureSeed) =>
        Task.Run(() => mesh.Materials.Distinct().ToDictionary(m => m, m => models.Painted(m, colour, 512, textureSeed)));
}
