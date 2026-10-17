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
    /// <paramref name="rawSeed"/>: starships build the table from the seed itself, not its
    /// first child - six of seven ships checked in game on 2026-10-17 matched that way
    /// (red/white/blue Fighter, yellow/blue Solar, teal/blue and blue Haulers...).</summary>
    public static async Task<Func<string, string, float[]?>?> SeedColoursAsync(ulong seed, bool rawSeed = false)
    {
        if (seed == 0 || await CatalogService.GetPalettesAsync() is not { Count: > 0 } palettes) return null;
        var table = ShipLook.PaletteTable(rawSeed ? seed : ColourSeed(seed), palettes);
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

    /// <summary>Each material painted with the given colours (null where it has nothing to paint with).
    /// <paramref name="styles"/>: texture options a built ship was given ("STEALTH"), taken over the seed's.</summary>
    public static Task<Dictionary<string, ModelTexture?>> PaintAsync(ShipModels models, ShipMesh mesh,
        Func<string, string, float[]?> colour, ulong? textureSeed, ISet<string>? styles = null) =>
        Task.Run(() => mesh.Materials.Distinct().ToDictionary(m => m, m => models.Painted(m, colour, 512, textureSeed, styles)));

    /// <summary>Colours kept in the save (a customisation entry's Aak list: crafted staffs,
    /// ships built at a station), by (palette, slot) - ("Paint", "Alternative1") - as RGB 0-1.</summary>
    public static Dictionary<(string, string), float[]> StoredColours(Newtonsoft.Json.Linq.JToken? entries)
    {
        var result = new Dictionary<(string, string), float[]>();
        if (entries is not Newtonsoft.Json.Linq.JArray list) return result;
        foreach (var e in list)
        {
            string? palette = e["RVl"]?["RVl"]?.ToString(), slot = e["RVl"]?["Ty="]?.ToString();
            if (palette is null || slot is null || e["xEg"] is not Newtonsoft.Json.Linq.JArray rgba || rgba.Count < 3) continue;
            result[(palette, slot)] = new[] { (float)rgba[0], (float)rgba[1], (float)rgba[2] };
        }
        return result;
    }

    /// <summary>A colour lookup over stored colours. The save keeps five slots per palette
    /// (Primary, Alternative1-4); "Unique" reads Alternative3, as seed colours do.</summary>
    public static Func<string, string, float[]?> StoredColourLookup(Dictionary<(string, string), float[]> stored) =>
        (palette, slot) => stored.GetValueOrDefault((palette, slot == "Unique" ? "Alternative3" : slot));
}
