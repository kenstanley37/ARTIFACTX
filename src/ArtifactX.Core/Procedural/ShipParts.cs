using System.Text.Json;

namespace ArtifactX.Core.Procedural;

// Ported to C# from NMS Shipwright's nms_procgen/generator.py (MIT, (c) 2026
// Shikhar Tiwari): https://github.com/S-T-0-7/NMS-Shipwright - see
// THIRD-PARTY-NOTICES.md. Their notes: "exact reimpl of NMS procedural part
// selection, reversed from NMS.exe (selector + weighted picker, 2026-09 build)".
// Checked against our own in-game test (2026-10-14, DELETEME, 5 Fighters):
// engines, cockpits and the bubble canopy all came out as seen in game.
// The maths must match the game bit for bit - don't "tidy" it.

/// <summary>One group of a part tree: the game picks one of its options.</summary>
public sealed record PartGroup(string Type, IReadOnlyList<PartOption> Options);

/// <summary>A part: its nested groups, and the scenes it references (which have trees of their own).</summary>
public sealed record PartOption(string Id, string Name, IReadOnlyList<string> Scenes, IReadOnlyList<IReadOnlyList<PartGroup>> Children)
{
    /// <summary>Odds: 20 normally, 1 for "xRARE" parts, 0 for "xNEVER".</summary>
    public int Weight => Name.Contains("xRARE", StringComparison.Ordinal) ? 1 : Name.Contains("xNEVER", StringComparison.Ordinal) ? 0 : 20;
}

/// <summary>A part the game picked: its group (e.g. "_WINGS_") and id ("_WINGS_F").</summary>
public sealed record PickedPart(string Group, string Id, int Depth);

/// <summary>
/// Which parts a procedural model (a ship's .SCENE.MBIN) gets for a seed.
/// <c>loadTree</c> returns a descriptor's groups by lower-case path
/// ("models/common/spacecraft/fighters/fighter_proc.descriptor.mbin"), or null.
/// </summary>
public static class ShipParts
{
    private const ulong M32 = 0xFFFFFFFF;

    /// <summary>The descriptor next to a scene: same path, .descriptor.mbin.</summary>
    public static string DescriptorFor(string scenePath) =>
        scenePath.ToLowerInvariant().Replace(".scene.mbin", ".descriptor.mbin", StringComparison.Ordinal);

    /// <param name="storedParts">Parts the save keeps for this model, which win over
    /// the seed: a staff's crafted head, core and pole ("^STAFF_HEAD_1", in the
    /// tool's 7DH.SMP list, 2026-10-16). Matched to options ignoring '^', '_' and case.</param>
    public static List<PickedPart> Generate(string scenePath, ulong seed, Func<string, IReadOnlyList<PartGroup>?> loadTree,
        IEnumerable<string>? storedParts = null)
    {
        var out_ = new List<PickedPart>();
        var preferred = new HashSet<string>((storedParts ?? Enumerable.Empty<string>()).Select(Plain), StringComparer.Ordinal);
        if (loadTree(DescriptorFor(scenePath)) is { } root)
            Select(root, seed, out_, new HashSet<string>(StringComparer.Ordinal), loadTree, 0, preferred);
        return out_;
    }

    private static string Plain(string id) => id.Replace("^", "").Replace("_", "").ToUpperInvariant();

    private static void Select(IReadOnlyList<PartGroup> groups, ulong seed, List<PickedPart> out_, HashSet<string> seen,
        Func<string, IReadOnlyList<PartGroup>?> loadTree, int depth, HashSet<string> preferred)
    {
        var rng = new Mwc(seed);
        foreach (var g in groups)
        {
            int total = g.Options.Sum(o => o.Weight);
            // A stored part is taken without a draw, as the game's selector does
            // with a single candidate (NMS.exe 0x142d6b5a0); several draw among themselves.
            var wanted = preferred.Count == 0 ? new List<PartOption>() : g.Options.Where(o => preferred.Contains(Plain(o.Id))).ToList();
            // The game skips (no RNG step) a group with nothing to pick, or with any
            // option already chosen elsewhere in the model. A stored part counts even
            // at weight 0: builder-only parts are "xNEVER" (never on a found ship) -
            // the Explorer's Skywave/Prismatic/Planar/Vortex wings differ only in an
            // xNEVER sub-wing (_SubWingR_AxNEVER...), so all four looked alike (2026-10-17).
            if ((total == 0 && wanted.Count == 0) || g.Options.Any(o => seen.Contains(o.Id))) continue;
            PartOption pick = g.Options[^1];
            if (wanted.Count == 1) pick = wanted[0];
            else if (wanted.Count > 1) pick = wanted[(int)((rng.Next() * (ulong)wanted.Count) >> 32)];
            else
            {
                ulong r = (rng.Next() * (ulong)total) >> 32;
                foreach (var o in g.Options)
                {
                    if (r < (ulong)o.Weight) { pick = o; break; }
                    r -= (ulong)o.Weight;
                }
            }
            string stored = StoredId(pick.Id);
            if (seen.Add(stored)) out_.Add(new PickedPart(g.Type, stored, depth));
            foreach (var kid in pick.Children)
                Select(kid, rng.ChildSeed(), out_, seen, loadTree, depth + 1, preferred);
            foreach (var scene in pick.Scenes)
            {
                ulong child = rng.ChildSeed();
                if (loadTree(DescriptorFor(scene)) is { } sub)
                    Select(sub, child, out_, seen, loadTree, depth + 1, preferred);
            }
        }
    }

    /// <summary>How the game records a part: a trailing "LOD&lt;digit&gt;" cut, upper-cased.</summary>
    private static string StoredId(string id)
    {
        int i = id.IndexOf("LOD", StringComparison.Ordinal);
        return i >= 0 && id.Length - i == 4 && char.IsDigit(id[i + 3]) ? id[..i].ToUpperInvariant() : id;
    }

    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }

    /// <summary>The game's 32-bit multiply-with-carry, seeded its own way.</summary>
    private sealed class Mwc
    {
        private ulong _s0, _s1;

        public Mwc(ulong seed)
        {
            ulong lo = seed & M32, hi = (seed >> 32) & M32;
            _s0 = lo == 0 ? 1 : lo; // the game does this
            _s1 = (((lo << 16) | (lo >> 16)) & M32) ^ hi ^ lo;
        }

        public ulong Next()
        {
            ulong t = _s0 * 0x5A76F899UL + _s1;
            _s0 = t & M32;
            _s1 = t >> 32;
            return _s0;
        }

        public ulong ChildSeed()
        {
            ulong a = Next();
            return Mix64((Next() << 32) | a);
        }
    }

    /// <summary>Reads a tree as stored in the catalog (CatalogSteps.AddShipDescriptors).</summary>
    public static IReadOnlyList<PartGroup> ParseTree(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Groups(doc.RootElement);

        static List<PartGroup> Groups(JsonElement array) => array.EnumerateArray().Select(g => new PartGroup(
            g.GetProperty("t").GetString() ?? "",
            g.GetProperty("o").EnumerateArray().Select(o => new PartOption(
                o.GetProperty("id").GetString() ?? "",
                o.GetProperty("n").GetString() ?? "",
                o.GetProperty("s").EnumerateArray().Select(s => s.GetString() ?? "").ToList(),
                o.GetProperty("c").EnumerateArray().Select(c => (IReadOnlyList<PartGroup>)Groups(c)).ToList())).ToList())).ToList();
    }
}
