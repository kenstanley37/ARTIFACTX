using ArtifactX.Core.Procedural;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A ship-builder part set (cockpit, wings...): its part items, each with the
/// descriptor options it turns on.</summary>
public sealed class PartSet
{
    public PartSet(string key, List<(string Id, List<string> Descriptors)> parts)
    {
        Key = key;
        Parts = parts;
    }

    public string Key { get; }
    public List<(string Id, List<string> Descriptors)> Parts { get; }
}

/// <summary>The look a ship built at a station keeps in the save (in-game build diff
/// 2026-10-16): its customisation entry (l:j[3 + slot], slots 6-11 at 17+) holds the
/// builder parts by item id (SMP: "^FIGHT_COCKAA"), every palette's colours (Aak, 66 x 5)
/// and the paint style as a texture option (T>1: "^STEALTH"). Null for found and bought
/// ships, whose entry is empty.</summary>
public sealed record BuiltLook(List<string> Items, Dictionary<(string, string), float[]> Colours, HashSet<string> Styles);

/// <summary>
/// The parts a seed gives a starship (ShipParts - port of NMS Shipwright's selector,
/// confirmed in game), as the game's own ship-builder parts - name and icon, e.g. "Omega
/// Fuselage" - plus how common each is and a search for a seed with the parts wanted.
///
/// Ship-builder parts come from catalog "PartGroups": each part item (FIGHT_WINGEB_FI)
/// turns on a set of descriptor options (_SUBWINGS_B, _WINGS_E, _ACC_A2). A seed "has" a
/// part when it picked all of them; per part set (FIGHT_WING...) the most specific match
/// is shown. Everything read from the game data is forgotten when it's rebuilt.
/// </summary>
public static class ShipPartsService
{
    private static Dictionary<string, string>? _treeJson;
    private static List<(string Id, string Set, List<string> Descriptors)>? _partGroups;
    // Part group -> the builder item that adds it (catalog "ShipBuilder"). A built ship
    // stores groups (SCIEN_L_WINGE), and the item (SCIEN_WINGE) has the name and icon.
    private static Dictionary<string, string>? _builderItemOf;
    private static DateTime? _dataStamp;
    private static List<ColourPalette>? _palettes;
    private static readonly Dictionary<string, IReadOnlyList<PartGroup>> TreeCache = new(StringComparer.OrdinalIgnoreCase);

    public const int SampleSize = 100_000;
    private static readonly Dictionary<string, Task<PartSample>> Samples = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The ship look data for the 3D preview (EnsureLookAsync).</summary>
    public static ShipLookData? Look { get; private set; }

    /// <summary>Loads (or reloads, after a game-data rebuild) the trees and part
    /// lists. False when the game data has none - it needs updating.</summary>
    public static async Task<bool> EnsureLoadedAsync()
    {
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (stamp != _dataStamp)
        {
            _dataStamp = stamp;
            _treeJson = null;
            _partGroups = null;
            _builderItemOf = null;
            _palettes = null;
            Look = null;
            lock (TreeCache) TreeCache.Clear();
            lock (Samples) Samples.Clear();
        }
        // Re-read while empty: the game data may have been updated since.
        if (_treeJson is null || _treeJson.Count == 0)
        {
            _treeJson = await CatalogService.GetShipPartTreesAsync();
            lock (TreeCache) TreeCache.Clear();
        }
        if (_partGroups is null || _partGroups.Count == 0)
            _partGroups = await CatalogService.GetPartGroupsAsync();
        if (_builderItemOf is null || _builderItemOf.Count == 0)
            _builderItemOf = BuilderItems(await CatalogService.GetJsonCategoryAsync("ShipBuilder"));
        // Every time: a game-data update empties the name/icon cache, and parts with no
        // name are left out - every Fighter part vanished after one (2026-10-15).
        // Already-cached ids are skipped, so this is cheap.
        await CatalogService.WarmCacheAsync(_partGroups.Select(g => g.Id).Concat(_builderItemOf.Values));
        return _treeJson.Count > 0 && _partGroups.Count > 0;
    }

    /// <summary>Material -> texture list data for the preview's surface shades.</summary>
    public static async Task EnsureLookAsync()
    {
        if (Look is not null) return;
        if (_palettes is null || _palettes.Count == 0) _palettes = await CatalogService.GetPalettesAsync();
        Look ??= new ShipLookData(_palettes,
            await CatalogService.GetJsonCategoryAsync("ShipScenes"),
            await CatalogService.GetJsonCategoryAsync("ShipMaterials"),
            await CatalogService.GetJsonCategoryAsync("TextureLists"));
    }

    public static IReadOnlyList<PartGroup>? LoadTree(string path)
    {
        lock (TreeCache)
        {
            if (TreeCache.TryGetValue(path, out var tree)) return tree;
            if (_treeJson is null || !_treeJson.TryGetValue(path, out var json)) return null;
            return TreeCache[path] = ShipParts.ParseTree(json);
        }
    }

    public static bool TryParseSeed(string? text, out ulong seed)
    {
        seed = 0;
        text = text?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return text.Length > 0 && ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed);
    }

    /// <summary>The ship-builder parts this ship type can have, grouped by set (cockpit,
    /// wings...). Builder data writes some ids in mixed case (_WingJointL_A vs the tree's
    /// _WINGJOINTL_A), so ids compare ignoring case. Ship types share some ids
    /// (_COCKPIT_A), so only the builder family that fits this ship best is used -
    /// FIGHT_, DROPS_, SCIEN_, SHUTT_, SAIL_ - picked by how many of its parts this tree
    /// can make, not by a hand-made list.</summary>
    public static List<PartSet> PartSetsFor(IReadOnlyList<PartGroup> root)
    {
        var possible = ModelPreviewService.AllOptionIds(root, LoadTree, StringComparer.OrdinalIgnoreCase);
        var fitting = (_partGroups ?? new()).Where(g => g.Descriptors.All(possible.Contains)).ToList();
        static string Family(string set) => set.Contains('_') ? set[..set.IndexOf('_')] : set;
        string? best = fitting.GroupBy(g => Family(g.Set)).OrderByDescending(f => f.Count()).FirstOrDefault()?.Key;
        // Only parts that are real items (a name and icon): Shuttles can't be built or
        // broken into parts in game, so theirs - like Explorer wings - have none and are
        // left out rather than shown as raw ids (checked in game 2026-10-15).
        return fitting
            .Where(g => Family(g.Set) == best && CatalogService.TryGet(g.Id) is not null)
            .GroupBy(g => g.Set)
            .Where(s => s.Count() > 1)
            .Select(s => new PartSet(s.Key, s.Select(g => (g.Id, g.Descriptors)).ToList()))
            .ToList();
    }

    /// <summary>"Omega Fuselage" from the catalog, else the part id.</summary>
    public static string PartName(string id)
    {
        string name = CatalogService.TryGet(id)?.DisplayName ?? id;
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    /// <summary>The builder item a stored part group belongs to (a Hauler wing stores two
    /// groups, DROPS_WINGDA and DROPS_WINGDA_, for one item).</summary>
    public static string BuilderItem(string group) => _builderItemOf?.GetValueOrDefault(group) ?? group;

    /// <summary>The descriptor options a seed picks for a ship model.</summary>
    public static HashSet<string> Picked(string scene, ulong seed) =>
        ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which part of a set a ship has, from the parts its seed picked.
    /// Fighter/Hauler/Solar parts list pieces that must ALL be picked; Explorer parts
    /// often list alternatives (the game's own "Any" salvage rule, in a table libMBIN
    /// can't read since COSMOS). So: a full match first, else the part sharing the most
    /// pieces. The display, the odds and the search all use this, so they always agree.</summary>
    public static string? Match(PartSet set, HashSet<string> picked)
    {
        string? best = null;
        int bestCount = -1;
        foreach (var (id, descriptors) in set.Parts)
            if (descriptors.Count > bestCount && descriptors.All(picked.Contains)) { best = id; bestCount = descriptors.Count; }
        if (best is not null) return best;
        int bestHits = 0, bestSize = int.MaxValue;
        foreach (var (id, descriptors) in set.Parts)
        {
            int hits = descriptors.Count(picked.Contains);
            if (hits > bestHits || (hits == bestHits && hits > 0 && descriptors.Count < bestSize))
            {
                best = id; bestHits = hits; bestSize = descriptors.Count;
            }
        }
        return best;
    }

    public static BuiltLook? BuiltLookOf(int slot)
    {
        if (slot is < 0 or > 11) return null;
        int entry = slot < 6 ? 3 + slot : 17 + slot - 6;
        if (SaveSessionManager.GetValue("vLc", "6f=", "l:j", entry.ToString(CultureInfo.InvariantCulture), "wnR") is not JObject data) return null;
        var items = (data["SMP"] as JArray)?.Select(t => t.ToString().TrimStart('^')).Where(t => t.Length > 0).ToList() ?? new();
        var colours = ModelPreviewService.StoredColours(data["Aak"]);
        var styles = (data["T>1"] as JArray)?.Select(t => t["=Cv"]?.ToString().TrimStart('^') ?? "").Where(t => t.Length > 0)
            .ToHashSet(StringComparer.Ordinal) ?? new();
        return items.Count == 0 && colours.Count == 0 ? null : new BuiltLook(items, colours, styles);
    }

    /// <summary>A built ship's parts as model options: each builder item turns on its
    /// descriptor options (PartGroups - FIGHT_COCKAA -> _COCKPIT_A...).</summary>
    public static List<string>? StoredOptions(BuiltLook? built) =>
        built is not { Items.Count: > 0 } || _partGroups is null ? null
            : built.Items.SelectMany(id => _partGroups.Where(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase))
                .SelectMany(g => g.Descriptors)).ToList();

    /// <summary>The ship types the station builder makes, and so can be painted (an
    /// in-game test, 2026-10-17: a found Hauler took colours and Titan Stripes, and kept them).</summary>
    public static bool IsPaintable(string scene)
    {
        string file = System.IO.Path.GetFileName(scene.Replace('\\', '/')).ToUpperInvariant();
        return file is "FIGHTER_PROC.SCENE.MBIN" or "DROPSHIP_PROC.SCENE.MBIN" or "SCIENTIFIC_PROC.SCENE.MBIN" or "SAILSHIP_PROC.SCENE.MBIN";
    }

    /// <summary>A neutral shade per material for the preview: each paint surface its own
    /// grey so the parts read apart, glass/lights/dark parts by their material name.</summary>
    public static uint MaterialColour(string material, ShipLookData? look)
    {
        string name = System.IO.Path.GetFileName(material).ToUpperInvariant();
        static uint Rgb(int r, int g, int b) => (uint)(r << 16 | g << 8 | b);
        if (System.Text.RegularExpressions.Regex.IsMatch(name, "GLASS|CANOPY|WINDOW|SCREEN|COCKPITTRANS|TRANS_")) return Rgb(32, 62, 82);
        if (System.Text.RegularExpressions.Regex.IsMatch(name, "LIGHT|GLOW|EMIS|THRUST|FLAME|LASER")) return Rgb(255, 214, 140);
        if (look is not null && look.MaterialLists(material) is { Count: > 0 } lists)
        {
            string key = System.IO.Path.GetFileName(lists[0]).Split('.')[0];
            if (key.EndsWith("PRIMARY", StringComparison.Ordinal)) return Rgb(196, 199, 205);
            if (key.EndsWith("SECONDARY", StringComparison.Ordinal)) return Rgb(158, 162, 170);
            if (key == "TERTIARY") return Rgb(122, 127, 136);
            if (key == "TRIMS") return Rgb(98, 102, 110);
        }
        if (System.Text.RegularExpressions.Regex.IsMatch(name, "RUBBER|DARK|BLACK|GRILL|VENT|UNDERCOAT")) return Rgb(46, 48, 53);
        return Rgb(140, 142, 148);
    }

    /// <summary>A part as the game's ship builder shows it: icon and name.</summary>
    private static Dictionary<string, string> BuilderItems(Dictionary<string, string> rows)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string json in rows.Values)
        {
            if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) continue;
            try
            {
                foreach (var item in JObject.Parse(json)["slots"]?.SelectMany(slot => slot["items"] ?? new JArray()) ?? Enumerable.Empty<JToken>())
                    foreach (var group in item["groups"] ?? new JArray())
                        result.TryAdd(group.ToString(), item["item"]?.ToString() ?? "");
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // A row this version can't read: its parts show by group id.
            }
        }
        return result;
    }

    // ------------------------------------------------------------------ odds

    /// <summary>A sample of random ships of one type: the part each has per set
    /// (Rows[ship][set]). Gives every part's odds and the odds of any combination, so
    /// the search can say how long it will take - or that the parts were never seen together.</summary>
    public sealed record PartSample(string?[][] Rows);

    public static Task<PartSample> SampleFor(string scene, List<PartSet> sets)
    {
        lock (Samples)
        {
            if (Samples.TryGetValue(scene, out var running)) return running;
            return Samples[scene] = Task.Run(() =>
            {
                var rows = new string?[SampleSize][];
                // Fixed seeds per chunk: the same odds every time the page opens.
                Parallel.For(0, 16, chunk =>
                {
                    var rng = new Random(1000 + chunk);
                    for (int i = chunk; i < SampleSize; i += 16)
                    {
                        ulong seed = (ulong)rng.NextInt64() | ((ulong)rng.Next(2) << 63);
                        var picked = Picked(scene, seed);
                        var row = new string?[sets.Count];
                        for (int s = 0; s < sets.Count; s++) row[s] = Match(sets[s], picked);
                        rows[i] = row;
                    }
                });
                return new PartSample(rows);
            });
        }
    }

    /// <summary>How many sample ships have all of these (set index -> part id).</summary>
    public static int CountWith(PartSample sample, Dictionary<int, string> want)
    {
        int n = 0;
        foreach (var row in sample.Rows)
        {
            bool all = true;
            foreach (var (s, id) in want)
                if (!string.Equals(row[s], id, StringComparison.OrdinalIgnoreCase)) { all = false; break; }
            if (all) n++;
        }
        return n;
    }

    public static string Odds(int count, int total) => count switch
    {
        0 => "not seen",
        _ when total / (double)count < 1.5 => "most ships",
        _ => $"1 in {Math.Round(total / (double)count):N0}",
    };

    public static string SearchTime(double seconds) =>
        seconds < 2 ? "a moment" : seconds < 90 ? $"about {Math.Ceiling(seconds)} seconds" : $"about {Math.Ceiling(seconds / 60)} minutes";

    // ------------------------------------------------------------------ search

    /// <summary>Random seeds whose parts are the wanted ones (set index -> part id): up to
    /// 5, from at most 100 million tried, on every core but one so the app stays
    /// responsive. Returns what was found and how many were tried.</summary>
    public static async Task<(List<ulong> Found, long Tried)> SearchAsync(string scene, List<PartSet> sets, Dictionary<int, string> want, CancellationToken token)
    {
        var found = new List<ulong>();
        long tried = 0;
        const long MaxTries = 100_000_000;
        try
        {
            int workers = Math.Max(1, Environment.ProcessorCount - 1);
            await Task.Run(() => Parallel.For(0, workers, w =>
            {
                var rng = new Random();
                while (!token.IsCancellationRequested && Volatile.Read(ref tried) < MaxTries)
                {
                    lock (found) if (found.Count >= 5) return;
                    ulong seed = (ulong)rng.NextInt64() | ((ulong)rng.Next(2) << 63);
                    Interlocked.Increment(ref tried);
                    var picked = Picked(scene, seed);
                    if (want.All(c => string.Equals(Match(sets[c.Key], picked), c.Value, StringComparison.OrdinalIgnoreCase)))
                        lock (found) if (found.Count < 5) found.Add(seed);
                }
            }), token);
        }
        catch (OperationCanceledException) { }
        return (found, tried);
    }
}

/// <summary>The ship look data from the catalog, parsed as ShipLook asks for it.</summary>
public sealed class ShipLookData(IReadOnlyList<ColourPalette> palettes, Dictionary<string, string> scenes,
    Dictionary<string, string> materials, Dictionary<string, string> lists) : IShipLookData
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SceneNode?> _scenes = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyList<string>> _materials = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyList<TextureLayer>?> _lists = new(StringComparer.OrdinalIgnoreCase);
    public bool HasScenes => scenes.Count > 0;
    public IReadOnlyList<ColourPalette> Palettes => palettes;
    public SceneNode? Scene(string path) => _scenes.GetOrAdd(path, p => scenes.TryGetValue(p, out var j) ? ShipLook.ParseScene(j) : null);
    public IReadOnlyList<string> MaterialLists(string material) => _materials.GetOrAdd(material, m =>
        materials.TryGetValue(m, out var j) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(j) ?? new List<string>() : new List<string>());
    public IReadOnlyList<TextureLayer>? TextureList(string path) => _lists.GetOrAdd(path, p => lists.TryGetValue(p, out var j) ? ShipLook.ParseTextureList(j) : null);
}
