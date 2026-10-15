using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Experiment (branch experiment/ship-parts, 2026-10-14): the parts a seed gives
/// the selected ship (ShipParts - port of NMS Shipwright's selector, confirmed
/// in game), shown as the game's own ship-builder parts - name and icon, e.g.
/// "Omega Fuselage" - and a search for a seed with the parts the player picks.
///
/// Ship-builder parts come from catalog "PartGroups": each part item
/// (FIGHT_WINGEB_FI) turns on a set of descriptor options (_SUBWINGS_B, _WINGS_E,
/// _ACC_A2). A seed "has" a part when it picked all of them; per part set
/// (FIGHT_WING...) the most specific match is shown.
/// </summary>
public sealed partial class ShipsPage
{
    private static Dictionary<string, string>? _treeJson;
    private static List<(string Id, string Set, List<string> Descriptors)>? _partGroups;
    private static DateTime? _dataStamp;
    private static List<ColourPalette>? _palettes;
    private static LookData? _look;
    // The game files' model reader: indexing every PAK takes a moment, so once per session.
    private static Task<ArtifactX.GameData.ShipModels?>? _models;
    private int _previewVersion;
    private static readonly Dictionary<string, IReadOnlyList<PartGroup>> TreeCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _partSearch;
    private readonly Dictionary<string, ComboBox> _wantBoxes = new(StringComparer.Ordinal);

    private static IReadOnlyList<PartGroup>? LoadTree(string path)
    {
        lock (TreeCache)
        {
            if (TreeCache.TryGetValue(path, out var tree)) return tree;
            if (_treeJson is null || !_treeJson.TryGetValue(path, out var json)) return null;
            return TreeCache[path] = ShipParts.ParseTree(json);
        }
    }

    private string? CurrentScene() => _selectedIndex < 0 ? null
        : SaveSessionManager.GetValue("vLc", "6f=", "@Cs", _selectedIndex.ToString(CultureInfo.InvariantCulture), "NTx", "93M")?.ToString();

    private static bool TryParseSeed(string? text, out ulong seed)
    {
        seed = 0;
        text = text?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return text.Length > 0 && ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed);
    }

    /// <summary>Every option id reachable from a tree (which parts this ship type can have at all).</summary>
    private static HashSet<string> AllOptionIds(IReadOnlyList<PartGroup> root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        if (seenTrees.Add(path) && LoadTree(path) is { } sub) Walk(sub);
                    }
                }
        }
        Walk(root);
        return ids;
    }

    /// <summary>The ship-builder parts this ship type can have, grouped by set
    /// (cockpit, wings...). Builder data writes some ids in mixed case
    /// (_WingJointL_A vs the tree's _WINGJOINTL_A), so ids compare ignoring case.
    /// Ship types share some ids (_COCKPIT_A), so only the builder family that
    /// fits this ship best is used - FIGHT_, DROPS_, SCIEN_, SHUTT_, SAIL_ - picked
    /// by how many of its parts this tree can make, not by a hand-made list.</summary>
    private static List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> PartSetsFor(IReadOnlyList<PartGroup> root)
    {
        var possible = AllOptionIds(root);
        var fitting = (_partGroups ?? new()).Where(g => g.Descriptors.All(possible.Contains)).ToList();
        static string Family(string set) => set.Contains('_') ? set[..set.IndexOf('_')] : set;
        string? best = fitting.GroupBy(g => Family(g.Set)).OrderByDescending(f => f.Count()).FirstOrDefault()?.Key;
        // Only parts that are real items (a name and icon): Shuttles can't be built
        // or broken into parts in game, so theirs - like Explorer wings - have none
        // and are left out rather than shown as raw ids (checked in game 2026-10-15).
        return fitting
            .Where(g => Family(g.Set) == best && CatalogService.TryGet(g.Id) is not null)
            .GroupBy(g => g.Set)
            .Where(s => s.Count() > 1)
            .ToList();
    }

    /// <summary>"Omega Fuselage" from the catalog, else the part id.</summary>
    private static string PartName(string id)
    {
        string name = CatalogService.TryGet(id)?.DisplayName ?? id;
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    /// <summary>Rebuilds the parts list for the selected ship's type and the seed in the box.</summary>
    private async void RefreshShipParts()
    {
        ApplyDetailsCollapsed(); // the closed band names the ship shown
        ShipPartsPanel.Children.Clear();
        // Hide the last ship's model (and drop a build still running for it), or
        // a ship that shows no parts kept the previous ship's model (2026-10-15).
        int refresh = ++_previewVersion;
        ShipPreviewGroup.Visibility = Visibility.Collapsed;
        string? scene = CurrentScene();
        if (scene is null) return;
        // Built again since last time (game-data update): forget everything read from the old one.
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (stamp != _dataStamp)
        {
            _dataStamp = stamp;
            _treeJson = null;
            _partGroups = null;
            _palettes = null;
            _look = null;
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
        // Every time: a game-data update empties the name/icon cache, and parts
        // with no name are left out - every Fighter part vanished after one
        // (2026-10-15). Already-cached ids are skipped, so this is cheap.
        await CatalogService.WarmCacheAsync(_partGroups.Select(g => g.Id));
        if (_treeJson.Count == 0 || _partGroups.Count == 0)
        {
            ShipPartsPanel.Children.Add(Dim("Update your game data (Settings › Game data) to see which parts a seed gives."));
            return;
        }
        var root = LoadTree(ShipParts.DescriptorFor(scene));
        bool hasSeed = TryParseSeed(SeedEditBox.Text, out ulong seed);
        // Every ship type has a model, parts or not - except a corvette, whose
        // look is the modules built onto it, stored apart from the ship.
        if (!IsCorvetteSelected() && (root is null || hasSeed))
        {
            await EnsureLookAsync();
            if (refresh == _previewVersion) ShowPreview(scene, hasSeed ? seed : 0, root);
        }
        if (root is null)
        {
            ShipPartsPanel.Children.Add(Dim("This ship type has a fixed look - its seed doesn't change its parts."));
            return;
        }
        var sets = PartSetsFor(root);
        if (sets.Count == 0)
        {
            ShipPartsPanel.Children.Add(Dim("This ship type can't be built from parts or broken into parts in game, so its parts have no names to show."));
            return;
        }

        ShipPartsPanel.Children.Add(new TextBlock { Text = "PARTS THIS SEED GIVES", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        if (hasSeed)
        {
            var picked = ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var row = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            foreach (var set in sets)
                if (Match(set, picked) is string id) row.Children.Add(PartChip(id));
            if (row.Children.Count == 0) ShipPartsPanel.Children.Add(Dim("No ship-builder parts match this seed."));
            else ShipPartsPanel.Children.Add(row);
            // Paint isn't shown: the seed -> colour rule (ShipColours/ShipLook,
            // ported from NMS Shipwright) was disproved in game on 2026-10-15 -
            // four controlled test fighters came out tan/red/blue/blue where it
            // predicted yellow/periwinkle/white/green. The code stays in Core for
            // when the game's real rule is found; until then the preview is
            // neutral and the page claims no colours.
        }
        else
        {
            ShipPartsPanel.Children.Add(Dim("Not a seed - it should look like 0x1CD40D0620985D13."));
        }

        BuildPartSearch(scene, sets);
    }

    /// <summary>Which part of a set a ship has, from the parts its seed picked.
    /// Fighter/Hauler/Solar parts list pieces that must ALL be picked;
    /// Explorer parts often list alternatives (the game's own "Any" salvage
    /// rule, in a table libMBIN can't read since COSMOS). So: a full match
    /// first, else the part sharing the most pieces. The display, the odds and
    /// the search all use this, so they always agree.</summary>
    private static string? Match(IGrouping<string, (string Id, string Set, List<string> Descriptors)> set, HashSet<string> picked)
    {
        string? best = null;
        int bestCount = -1;
        foreach (var g in set)
            if (g.Descriptors.Count > bestCount && g.Descriptors.All(picked.Contains)) { best = g.Id; bestCount = g.Descriptors.Count; }
        if (best is not null) return best;
        int bestHits = 0, bestSize = int.MaxValue;
        foreach (var g in set)
        {
            int hits = g.Descriptors.Count(picked.Contains);
            if (hits > bestHits || (hits == bestHits && hits > 0 && g.Descriptors.Count < bestSize))
            {
                best = g.Id; bestHits = hits; bestSize = g.Descriptors.Count;
            }
        }
        return best;
    }

    /// <summary>Every option id a ship type can have, compared exactly as the
    /// original look code does (case-sensitive) - see ShipLook.Generate.</summary>
    private static HashSet<string> AllOptionIdsExact(IReadOnlyList<PartGroup> root) =>
        new(AllOptionIds(root), StringComparer.Ordinal);

    private bool IsCorvetteSelected() =>
        _ships.FirstOrDefault(s => s.Index == _selectedIndex)?.IsCorvette ?? false;

    /// <summary>Material -> texture list data for the preview's surface shades.</summary>
    private static async Task EnsureLookAsync()
    {
        if (_look is not null) return;
        if (_palettes is null || _palettes.Count == 0) _palettes = await CatalogService.GetPalettesAsync();
        _look ??= new LookData(_palettes,
            await CatalogService.GetJsonCategoryAsync("ShipScenes"),
            await CatalogService.GetJsonCategoryAsync("ShipMaterials"),
            await CatalogService.GetJsonCategoryAsync("TextureLists"));
    }

    /// <summary>The ship look data from the catalog, parsed as ShipLook asks for it.</summary>
    private sealed class LookData(IReadOnlyList<ColourPalette> palettes, Dictionary<string, string> scenes,
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

    /// <summary>The 3D preview: the seed's parts built from the player's own
    /// game files (ShipModels) in neutral shades - each paint surface its own
    /// grey so the parts read apart, glass/lights/dark parts by their material
    /// name. Not the ship's real colours (see RefreshShipParts).</summary>
    private async void ShowPreview(string scene, ulong seed, IReadOnlyList<PartGroup>? root)
    {
        int version = ++_previewVersion;
        if (GameDataService.Game is not { } game) { ShipPreviewGroup.Visibility = Visibility.Collapsed; return; }
        ShipPreviewGroup.Visibility = Visibility.Visible;
        _models ??= Task.Run(() => { try { return new ArtifactX.GameData.ShipModels(game); } catch { return null; } });
        var models = await _models;
        if (models is null || version != _previewVersion) return;
        // A fixed-look type has no part options: every mesh in its scene is kept.
        var options = root is null ? new HashSet<string>(StringComparer.Ordinal) : AllOptionIdsExact(root);
        var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
            : ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var mesh = await Task.Run(() => models.Build(scene, options, chosen));
        if (version != _previewVersion) return;
        var look = _look;
        ShipPreviewView.Show(mesh, material => MaterialColour(material, look));
        ShipPreviewNote.Text = mesh is null
            ? "This ship's model couldn't be read from your game files."
            : "Shape from your game files - drag to turn. Shown in neutral grey: the colours a seed gives can't be predicted yet.";
    }

    private static uint MaterialColour(string material, LookData? look)
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
    private static UIElement PartChip(string id)
    {
        var chip = new StackPanel { Width = 92, Spacing = 2 };
        if (CatalogService.TryGet(id)?.Icon is { } icon) chip.Children.Add(new Image { Source = icon, Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center });
        chip.Children.Add(new TextBlock { Text = PartName(id), FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxLines = 3 });
        ToolTipService.SetToolTip(chip, PartName(id));
        return chip;
    }

    // ------------------------------------------------------------------ odds

    /// <summary>A sample of random ships of one type: the part each has per set
    /// (Rows[ship][set]). Gives every part's odds and the odds of any
    /// combination, so the search can say how long it will take - or that the
    /// parts were never seen together.</summary>
    private sealed record PartSample(string?[][] Rows);

    private const int SampleSize = 100_000;
    private static readonly Dictionary<string, Task<PartSample>> Samples = new(StringComparer.OrdinalIgnoreCase);

    private static Task<PartSample> SampleFor(string scene, List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> sets)
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
                        var picked = ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
    private static int CountWith(PartSample sample, Dictionary<int, string> want)
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

    private static string Odds(int count, int total) => count switch
    {
        0 => "not seen",
        _ when total / (double)count < 1.5 => "most ships",
        _ => $"1 in {Math.Round(total / (double)count):N0}",
    };

    // ------------------------------------------------------------------ search

    /// <summary>"Find a seed": one choice per part set with each part's odds
    /// (given the other picks), the odds of the whole combination, then a
    /// search on all cores.</summary>
    private void BuildPartSearch(string scene, List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> sets)
    {
        var toggle = new HyperlinkButton { Content = "Find a seed with the parts you want", Padding = new Thickness(0) };
        var body = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        toggle.Click += (_, _) => body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        ShipPartsPanel.Children.Add(toggle);

        var choices = new StackPanel { Spacing = 8 };
        _wantBoxes.Clear();
        var oddsTexts = new List<Dictionary<string, TextBlock>>();
        var oddsLine = Dim("Working out the odds...");
        for (int s = 0; s < sets.Count; s++)
        {
            var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            box.Items.Add(new ComboBoxItem { Content = "Any", Tag = null });
            var texts = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in sets[s].OrderBy(g => PartName(g.Id)))
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (CatalogService.TryGet(g.Id)?.Icon is { } icon) item.Children.Add(new Image { Source = icon, Width = 28, Height = 28 });
                item.Children.Add(new TextBlock { Text = PartName(g.Id), VerticalAlignment = VerticalAlignment.Center });
                var odds = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = ThemeResources.Brush("ThemeTextDimBrush") };
                item.Children.Add(odds);
                texts[g.Id] = odds;
                box.Items.Add(new ComboBoxItem { Content = item, Tag = g.Id });
            }
            box.SelectedIndex = 0;
            _wantBoxes[sets[s].Key] = box;
            oddsTexts.Add(texts);
            choices.Children.Add(box);
        }
        body.Children.Add(choices);
        body.Children.Add(oddsLine);

        var results = new StackPanel { Spacing = 4 };
        var status = Dim("");
        var search = new Button { Content = "Search" };
        search.Click += async (_, _) => await SearchSeedsAsync(scene, sets, search, status, results);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { search, status } });
        body.Children.Add(results);
        ShipPartsPanel.Children.Add(body);

        // Odds: each part's chance given the other picks; parts never seen with
        // them are dimmed (still pickable - very rare isn't impossible).
        int version = 0;
        async void UpdateOdds()
        {
            int mine = ++version;
            var sample = await SampleFor(scene, sets);
            var chosen = Chosen(sets);
            var ids = oddsTexts.Select(t => t.Keys.ToList()).ToList();
            // Counting 100,000 ships per part is too slow for the UI thread on
            // ship types with many parts (a Hauler has 122 wings).
            var (perPart, all) = await Task.Run(() =>
            {
                var counts = new List<Dictionary<string, (int N, bool HasOthers)>>();
                for (int s = 0; s < sets.Count; s++)
                {
                    var others = chosen.Where(c => c.Key != s).ToDictionary(c => c.Key, c => c.Value);
                    var d = new Dictionary<string, (int, bool)>(StringComparer.OrdinalIgnoreCase);
                    foreach (var id in ids[s])
                        d[id] = (CountWith(sample, new Dictionary<int, string>(others) { [s] = id }), others.Count > 0);
                    counts.Add(d);
                }
                return (counts, chosen.Count == 0 ? -1 : CountWith(sample, chosen));
            });
            if (mine != version) return; // a newer pick is already being counted
            for (int s = 0; s < sets.Count; s++)
                foreach (var (id, text) in oddsTexts[s])
                {
                    var (n, hasOthers) = perPart[s][id];
                    text.Text = hasOthers && n == 0 ? "not seen with your other picks" : Odds(n, SampleSize);
                    if (text.Parent is FrameworkElement item) item.Opacity = n == 0 ? 0.5 : 1;
                }
            if (all < 0) { oddsLine.Text = "Pick the parts you want, then Search."; return; }
            double perSecond = 60_000.0 * Math.Max(1, Environment.ProcessorCount - 1);
            oddsLine.Text = all == 0
                ? $"None of {SampleSize:N0} sample ships had all of these - they may never appear together. You can still search."
                : $"About {Odds(all, SampleSize)} ships have all of these - a search should take {SearchTime(5 * SampleSize / (double)all / perSecond)}.";
        }
        foreach (var box in _wantBoxes.Values) box.SelectionChanged += (_, _) => UpdateOdds();
        UpdateOdds();
    }

    private static string SearchTime(double seconds) =>
        seconds < 2 ? "a moment" : seconds < 90 ? $"about {Math.Ceiling(seconds)} seconds" : $"about {Math.Ceiling(seconds / 60)} minutes";

    /// <summary>The picked part per set (set index -> part id), leaving out "Any".</summary>
    private Dictionary<int, string> Chosen(List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> sets)
    {
        var chosen = new Dictionary<int, string>();
        for (int s = 0; s < sets.Count; s++)
            if (_wantBoxes.TryGetValue(sets[s].Key, out var box) && (box.SelectedItem as ComboBoxItem)?.Tag is string id)
                chosen[s] = id;
        return chosen;
    }

    private async Task SearchSeedsAsync(string scene, List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> sets,
        Button search, TextBlock status, StackPanel results)
    {
        if (_partSearch is not null) { _partSearch.Cancel(); return; }
        var want = Chosen(sets);
        results.Children.Clear();
        _partSearch = new CancellationTokenSource();
        var token = _partSearch.Token;
        search.Content = "Stop";
        status.Text = "Searching...";
        var found = new List<ulong>();
        long tried = 0;
        const long MaxTries = 100_000_000;
        try
        {
            // Every core but one, so the app stays responsive.
            int workers = Math.Max(1, Environment.ProcessorCount - 1);
            await Task.Run(() => Parallel.For(0, workers, w =>
            {
                var rng = new Random();
                while (!token.IsCancellationRequested && Volatile.Read(ref tried) < MaxTries)
                {
                    lock (found) if (found.Count >= 5) return;
                    ulong seed = (ulong)rng.NextInt64() | ((ulong)rng.Next(2) << 63);
                    Interlocked.Increment(ref tried);
                    var picked = ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (want.All(c => string.Equals(Match(sets[c.Key], picked), c.Value, StringComparison.OrdinalIgnoreCase)))
                        lock (found) if (found.Count < 5) found.Add(seed);
                }
            }), token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _partSearch = null;
            search.Content = "Search";
        }
        status.Text = found.Count == 0
            ? $"No match in {tried:N0} seeds - these parts may never appear together."
            : $"Found {found.Count} in {tried:N0} seeds.";
        foreach (ulong seed in found)
        {
            string text = $"0x{seed:X16}";
            var use = new Button { Content = "Use", Padding = new Thickness(10, 2, 10, 2) };
            use.Click += (_, _) =>
            {
                if (_selectedIndex < 0) return;
                SeedEditBox.Text = text;
                SaveSessionManager.StageEdit(text, NmsInventoryContainer.ShipModelSeedPath(_selectedIndex));
                PageResetBtn.Visibility = Visibility.Visible;
                RefreshShipParts();
            };
            results.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 10,
                Children = { new TextBlock { Text = text, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"), VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true }, use },
            });
        }
    }

    private static TextBlock Dim(string text) => new()
    {
        Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
    };
}
