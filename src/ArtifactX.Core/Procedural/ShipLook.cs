using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArtifactX.Core.Procedural;

// Ported to C# from NMS Shipwright's nms_procgen/textures.py (MIT, (c) 2026
// Shikhar Tiwari): https://github.com/S-T-0-7/NMS-Shipwright - see
// THIRD-PARTY-NOTICES.md. Their notes: "seed -> ship colours, texture modes +
// decals, following NMS.exe ... all maths is float32 like the game. dont switch
// to float64, picks will drift." Kept that way here.

/// <summary>One option of a texture layer: a pattern painted with one palette colour.</summary>
public sealed record TextureOption(string Name, int Alt, int Palette, float Weight, int Use);

/// <summary>One layer of a procedural texture list (BASE, then overlays).</summary>
public sealed record TextureLayer(string Name, string Group, float Probability, bool MatchBase, IReadOnlyList<TextureOption> Options);

/// <summary>A pruned scene node: its material, the scene it references, and its children.</summary>
public sealed record SceneNode(string Name, string Material, string Reference, IReadOnlyList<SceneNode> Children);

/// <summary>What the seed paints on one layer of one texture list: the pattern and its colour.</summary>
public sealed record SurfacePick(string List, string Layer, string Option, int Palette, int Alt, float[]? Rgb);

/// <summary>Where the game data comes from (catalog-backed in the app).</summary>
public interface IShipLookData
{
    IReadOnlyList<ColourPalette> Palettes { get; }
    SceneNode? Scene(string path);
    IReadOnlyList<string> MaterialLists(string material);
    IReadOnlyList<TextureLayer>? TextureList(string path);
}

public static class ShipLook
{
    private const ulong M32 = 0xFFFFFFFF;
    private const double Inv = 1.0 / 4294967295.0;
    private static readonly Regex LodSuffix = new(@"LOD\d$", RegexOptions.IgnoreCase);

    /// <summary>Each texture layer the seed paints on this ship, first pick per
    /// (list, layer) as the game shows it. <paramref name="optionIds"/> are all
    /// the part ids the ship type can have, <paramref name="chosen"/> the ones
    /// this seed picked (ShipParts). Both compare case-sensitively, like the
    /// original: a node is dropped when its upper-cased name is an option id
    /// that wasn't chosen (mixed-case ids such as _WingJointL_A never match, so
    /// those nodes are always kept).</summary>
    public static List<SurfacePick> Generate(string rootScene, ulong seed, ISet<string> optionIds, ISet<string> chosen, IShipLookData data)
    {
        var table = PaletteTable(seed, data.Palettes);
        var picks = new List<SurfacePick>();
        var seenKeys = new HashSet<(string, string)>();
        foreach (var material in Materials(rootScene, optionIds, chosen, data))
        {
            var lists = data.MaterialLists(material);
            if (lists.Count == 0) continue;
            var merged = Merge(lists.Select(l => data.TextureList(l)).Where(l => l is not null)!);
            string listName = System.IO.Path.GetFileName(lists[0]).Split('.')[0];
            foreach (var (layer, option) in Pick(merged, seed))
            {
                if (option is null || !seenKeys.Add((listName, layer.Name))) continue;
                picks.Add(new SurfacePick(listName, layer.Name, option.Name, option.Palette, option.Alt, Colour(table, option.Palette, option.Alt)));
            }
        }
        return picks;
    }

    // ------------------------------------------------------------ materials

    /// <summary>Materials on the parts the seed builds: the root scene and
    /// every scene it references, without the options it didn't pick.</summary>
    private static List<string> Materials(string root, ISet<string> optionIds, ISet<string> chosen, IShipLookData data)
    {
        bool Keep(string name)
        {
            string up = name.ToUpperInvariant();
            return !optionIds.Contains(up) || chosen.Contains(LodSuffix.Replace(up, "")) || chosen.Contains(up);
        }
        var materials = new SortedSet<string>(StringComparer.Ordinal);
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var todo = new Stack<string>();
        todo.Push(root);
        void Walk(SceneNode node)
        {
            if (!Keep(node.Name)) return;
            if (node.Material.Length > 0) materials.Add(node.Material);
            if (node.Reference.Length > 0) refs.Add(node.Reference);
            foreach (var child in node.Children) Walk(child);
        }
        while (todo.Count > 0)
        {
            string path = todo.Pop();
            if (!seen.Add(path)) continue;
            if (data.Scene(path) is { } scene) Walk(scene);
            foreach (var r in refs.Where(r => !seen.Contains(r)).ToList()) todo.Push(r);
        }
        return materials.ToList();
    }

    // -------------------------------------------------------- texture picks

    private sealed class MergedLayer
    {
        public required TextureLayer Layer;
        public float ProbabilitySum;
        public int Count;
        public readonly List<(TextureOption Option, float WeightSum, int Count)> Options = new();
    }

    /// <summary>Layers of several lists (one per material sampler) merged as
    /// the game does: by name and group, probabilities and weights averaged.</summary>
    private static List<MergedLayer> Merge(IEnumerable<IReadOnlyList<TextureLayer>> lists)
    {
        var merged = new List<MergedLayer>();
        foreach (var layers in lists)
            foreach (var layer in layers)
            {
                if (layer.Options.Count == 0) continue;
                var slot = merged.FirstOrDefault(m => m.Layer.Name == layer.Name && m.Layer.Group == layer.Group);
                if (slot is null) merged.Add(slot = new MergedLayer { Layer = layer });
                slot.ProbabilitySum += layer.Probability;
                slot.Count++;
                foreach (var o in layer.Options)
                {
                    int at = slot.Options.FindIndex(x => x.Option.Name == o.Name && x.Option.Alt == o.Alt && x.Option.Palette == o.Palette);
                    if (at < 0)
                    {
                        if (o.Use != 0) continue; // only for a requested name, never on ships
                        slot.Options.Add((o, 0f, 0));
                        at = slot.Options.Count - 1;
                    }
                    var have = slot.Options[at];
                    slot.Options[at] = (have.Option, have.WeightSum + o.Weight, have.Count + 1);
                }
            }
        return merged;
    }

    private static List<(TextureLayer Layer, TextureOption? Option)> Pick(List<MergedLayer> merged, ulong seed)
    {
        var (s0, s1) = State(seed);
        var rows = new List<(TextureLayer Layer, TextureOption? Option, bool Match)>();
        foreach (var m in merged)
        {
            float prob = m.ProbabilitySum / (float)m.Count;
            TextureOption? chosen = null;
            bool match = false;
            if (prob > 0)
            {
                float x = (float)(Step(ref s0, ref s1) * Inv);
                if (prob > x && m.Layer.MatchBase) match = true;
                else if (prob > x)
                {
                    var weights = m.Options.Select(o => o.WeightSum / (float)o.Count).ToList();
                    float total = 0f;
                    foreach (float w in weights) total += w;
                    float r = (float)(Step(ref s0, ref s1) * Inv) * total;
                    float acc = 0f;
                    for (int i = 0; i < weights.Count; i++)
                    {
                        acc += weights[i];
                        if (acc > r) { chosen = m.Options[i].Option; break; }
                    }
                }
            }
            rows.Add((m.Layer, chosen, match));
        }
        var baseOption = rows.FirstOrDefault(r => r.Layer.Name == "BASE" && r.Option is not null).Option;
        return rows.Select(r => (r.Layer, r.Match
            ? merged.First(m => m.Layer == r.Layer).Options.Select(o => o.Option).FirstOrDefault(o => baseOption is not null && o.Name == baseOption.Name)
            : r.Option)).ToList();
    }

    // ------------------------------------------------------------- palettes

    /// <summary>All 66 palettes' colours for this seed, in the game's order:
    /// 0-51 in turn, the race palettes from one child seed, Grass and GrassAlt
    /// from another, the rest after that (BioShip_Underbelly reusing Paint's start).</summary>
    public static float[][][] PaletteTable(ulong seed, IReadOnlyList<ColourPalette> palettes)
    {
        var table = new float[palettes.Count][][];
        var (s0, s1) = State(seed);
        (ulong, ulong) saved = default;
        for (int p = 0; p < 52 && p < palettes.Count; p++)
        {
            if (p == ShipColours.PaintPalette) saved = (s0, s1);
            table[p] = Palette(palettes[p], ref s0, ref s1, 5);
        }
        var (c1, rest) = Child(s0, s1);
        var (c2, _) = Child(rest.Item1, rest.Item2);
        foreach (int p in new[] { 32, 35, 34, 37, 33, 36 })
        {
            var (r0, r1) = State(c1);
            table[p] = Palette(palettes[p], ref r0, ref r1, 5);
        }
        var (g0, g1) = State(c2);
        table[0] = Palette(palettes[0], ref g0, ref g1, 5);
        (g0, g1) = State(c2);
        table[51] = Palette(palettes[51], ref g0, ref g1, 5);
        var (tail0, tail1) = State(Child(g0, g1).Seed);
        for (int p = 52; p < palettes.Count; p++)
        {
            if (p == 56) { var (a, b) = saved; table[p] = Palette(palettes[p], ref a, ref b, 5); }
            else table[p] = Palette(palettes[p], ref tail0, ref tail1, 5);
        }
        return table;
    }

    /// <summary>A palette colour: alt 0-3 the seed's colours (4/5 use the 4th), 6 an override (none here), 7 none.</summary>
    private static float[]? Colour(float[][][] table, int palette, int alt) =>
        alt is 6 or 7 || palette < 0 || palette >= table.Length || table[palette] is null ? null : table[palette][Math.Min(alt, 3)];

    private static float[][] Palette(ColourPalette palette, ref ulong s0, ref ulong s1, int n)
    {
        var picked = new List<float[]>();
        for (int k = 0; k < n; k++)
        {
            int a = (int)(Step(ref s0, ref s1) >> 29), b = (int)(Step(ref s0, ref s1) >> 29);
            int i = palette.NumColours switch
            {
                "_1" => 0,
                "_4" => ((b >> 2) + (a >> 2) * 8) * 4,
                "_8" => b,
                "_16" => ((b >> 1) + (a >> 1) * 8) * 2,
                _ => b + a * 8,
            };
            float[] c = palette.Colours[Normalise(palette.NumColours, i)];
            for (int tries = 0; tries < 64; tries++)
            {
                if (!picked.Any(d => Close(c, d))) break;
                i = (i + 1) % 64;
                c = palette.Colours[Normalise(palette.NumColours, i)];
            }
            picked.Add(c);
        }
        return picked.ToArray();
    }

    private static int Normalise(string mode, int i) => mode switch
    {
        "_1" => 0,
        "_4" => ((i % 8) / 4 + (i / 32) * 8) * 4,
        "_8" => i % 8,
        "_16" => ((i % 8) / 2 + (i / 16) * 8) * 2,
        _ => i,
    };

    private static bool Close(float[] c, float[] d)
    {
        float r = c[0] - d[0], g = c[1] - d[1], b = c[2] - d[2];
        float sum = r * r;
        sum += g * g;
        sum += b * b;
        return sum < 2.3283064365386963e-10f;
    }

    // ------------------------------------------------------------------ RNG

    private static (ulong, ulong) State(ulong seed)
    {
        ulong lo = seed & M32, hi = seed >> 32;
        return (lo == 0 ? 1 : lo, (((lo >> 16) | (lo << 16)) & M32) ^ hi ^ lo);
    }

    private static ulong Step(ref ulong s0, ref ulong s1)
    {
        ulong t = s0 * 0x5A76F899UL + s1;
        s0 = t & M32;
        s1 = t >> 32;
        return s0;
    }

    /// <summary>Two steps on a copy of the state, mixed into a child seed; also returns the advanced copy.</summary>
    private static (ulong Seed, (ulong, ulong) After) Child(ulong s0, ulong s1)
    {
        ulong a = Step(ref s0, ref s1);
        ulong b = Step(ref s0, ref s1);
        ulong v = (b << 32) | a;
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return (v ^ (v >> 33), (s0, s1));
    }

    // ----------------------------------------------------------- catalog JSON

    public static SceneNode ParseScene(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Node(doc.RootElement);
        static SceneNode Node(JsonElement e) => e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("n", out var n)
            ? new SceneNode("", "", "", Array.Empty<SceneNode>())
            : new SceneNode(n.GetString() ?? "", e.GetProperty("m").GetString() ?? "", e.GetProperty("r").GetString() ?? "",
                e.GetProperty("c").EnumerateArray().Select(Node).ToList());
    }

    public static List<TextureLayer> ParseTextureList(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(l => new TextureLayer(
            l.GetProperty("n").GetString() ?? "", l.GetProperty("g").GetString() ?? "", l.GetProperty("p").GetSingle(), l.GetProperty("m").GetBoolean(),
            l.GetProperty("o").EnumerateArray().Select(o => new TextureOption(
                o.GetProperty("n").GetString() ?? "", o.GetProperty("a").GetInt32(), o.GetProperty("pal").GetInt32(),
                o.GetProperty("w").GetSingle(), o.GetProperty("u").GetInt32())).ToList())).ToList();
    }
}
