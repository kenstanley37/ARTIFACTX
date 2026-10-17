using ArtifactX.Core.NmsModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Reads a planet's Discoveries page (weather, sentinels, flora, fauna, resources) from
/// the OCR text of a recorded game screen and stores it on the planet, as the Planets
/// page's details, marked source "captured" (GitHub #18 step 2, 2026-10-17). The screen
/// names the planet but not its system, so the name is matched against the planets of
/// the systems the save recently put the player in - generated names plus names the
/// save recorded. OCR mangles the big title ("Izent 62/84" for "Izent 62/B4", "Euben"
/// for "Euben XIII"), so the match is fuzzy, and nothing is stored unless exactly one
/// planet is a clear match. Each field snaps to the game's own words where it can
/// ("d Thin" -> "Spread Thin"). Notes the player wrote are kept.
/// </summary>
public static class PlanetPageReader
{
    private static readonly object Lock = new();
    // The systems the live save put the player in, newest first (the save lags behind a warp).
    private static readonly List<ulong> RecentSystems = new();
    private static List<(string Word, string Group, string Tier)>? _words;
    private static List<string>? _resources;

    /// <summary>Planets filled in this session (for the Settings card).</summary>
    public static int Filled { get; private set; }

    public static void Start()
    {
        FishCatchRecorder.SnapshotSeen += snapshot =>
        {
            if (NmsSpaceStationPaths.ReadSystemAddress(snapshot.Universe) is not ulong system) return;
            lock (Lock)
            {
                RecentSystems.Remove(system);
                RecentSystems.Insert(0, system);
                if (RecentSystems.Count > 3) RecentSystems.RemoveAt(3);
            }
        };
    }

    /// <summary>Called with each recorded screen's text lines. Returns a short note of
    /// what happened (for the capture log), or null when it isn't a planet page.</summary>
    /// <param name="titleReads">Reads the title area again on its own: the big planet name
    /// is often missed in the whole-screen read ("Guhul VI" read as nothing, 2026-10-17),
    /// while a tight, shrunk crop of it reads well.</param>
    public static async Task<string?> TryReadAsync(IReadOnlyList<string> lines, Func<Task<IReadOnlyList<string>>>? titleReads = null)
    {
        await EnsureWordsAsync(); // cached; also what the Discoveries pop-ups use (ReadPopupResources)
        int data = lines.ToList().FindIndex(l => l.Trim().Equals("Planet Data", StringComparison.OrdinalIgnoreCase));
        int res = lines.ToList().FindIndex(l => l.Trim().Equals("Resources", StringComparison.OrdinalIgnoreCase));
        if (data < 0 || res < 0) return null;

        await EnsureWordsAsync();
        var details = new PlanetDetails
        {
            Weather = Field(lines, data, "Weather", "Weather"),
            Sentinels = Field(lines, data, "Sentinels", "Sentinels"),
            Flora = Field(lines, data, "Flora", "Life"),
            Fauna = Field(lines, data, "Fauna", "Life"),
            Resources = Resources(lines, res),
            FaunaTotal = FaunaTotal(lines),
        };
        if (details.Weather == "" && details.Sentinels == "" && details.Resources.Count == 0) return "planet page: nothing readable";

        var titles = new List<string>();
        if (Title(lines) is { } fromScreen) titles.Add(fromScreen);
        if (titleReads is not null)
            foreach (var read in await titleReads())
                if (Clean(read) is { Length: >= 3 } t && !IsHud(t)) titles.Add(t);
        if (titles.Count == 0) return "planet page: no planet name";
        var match = Match(titles);
        if (match is not (ulong system, int planet, string name))
            return $"planet page \"{string.Join("\" / \"", titles.Distinct())}\": no single matching planet in recent systems";

        // The player's own notes stay; the fields are what the game showed.
        var existing = PlanetDetailsService.Get(system, planet);
        details.Notes = existing?.Notes.ToList() ?? new();
        details.FaunaTotal ??= existing?.FaunaTotal;
        // A field that wasn't read keeps what was recorded.
        if (existing is not null)
        {
            if (details.Weather == "") details.Weather = existing.Weather;
            if (details.Sentinels == "") details.Sentinels = existing.Sentinels;
            if (details.Flora == "") details.Flora = existing.Flora;
            if (details.Fauna == "") details.Fauna = existing.Fauna;
        }
        // A partly covered page never shrinks a fuller list already recorded.
        if (existing is { Resources.Count: > 0 } && details.Resources.Count < existing.Resources.Count
            && details.Resources.All(r => existing.Resources.Contains(r, StringComparer.OrdinalIgnoreCase)))
            details.Resources = existing.Resources.ToList();
        if (existing is not null && Same(existing, details)) return $"planet page {name}: already up to date";
        PlanetDetailsService.Save(system, planet, details, source: "captured");
        Filled++;
        // "Dissonance detected" among a planet's resources marks the whole system dissonant.
        if (details.Resources.Contains("Dissonance detected", StringComparer.OrdinalIgnoreCase))
            SystemPageReader.MarkDissonant(system);
        return $"planet page {name}: saved ({details.Weather} / {details.Sentinels} / {details.Flora} / {details.Fauna} / {string.Join(", ", details.Resources)})";
    }

    private static bool Same(PlanetDetails a, PlanetDetails b) =>
        a.Weather == b.Weather && a.Sentinels == b.Sentinels && a.Flora == b.Flora && a.Fauna == b.Fauna
        && a.FaunaTotal == b.FaunaTotal
        && a.Resources.SequenceEqual(b.Resources, StringComparer.OrdinalIgnoreCase);

    private static async Task EnsureWordsAsync()
    {
        if (_words is { Count: > 0 } && _resources is { Count: > 0 }) return;
        _words = await CatalogService.GetPlanetDescriptorsAsync();
        // Substances first (their own names), then every product name (Star Bulb is a
        // plant product) and the planet-only finds (Ancient Bones, Salvageable Scrap).
        _resources = (await CatalogService.GetSubstanceNamesAsync())
            .Concat(_words.Where(w => w.Group == "Resource").Select(w => w.Word))
            .Concat((await CatalogService.GetItemNamesAsync()).Values.Select(TitleCase))
            .Concat(new[] { "Dissonance detected" })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ------------------------------------------------------------- fields

    /// <summary>The line after the label (first one after "Planet Data"), as one of the game's
    /// own words for it - or "" when it isn't one (a half-drawn page read "Discovered" as
    /// Guhul VI's flora, 2026-10-17); the planet then keeps what it had.</summary>
    private static string Field(IReadOnlyList<string> lines, int from, string label, string group)
    {
        var words = _words!.Where(w => w.Group == group).Select(w => w.Word).ToList();
        for (int i = from + 1; i < lines.Count - 1; i++)
            if (lines[i].Trim().Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                string word = Snap(Clean(lines[i + 1]), words);
                return words.Contains(word, StringComparer.OrdinalIgnoreCase) ? word : "";
            }
        return "";
    }

    /// <summary>Resource lines: after "Resources" until the page's lower labels.</summary>
    private static List<string> Resources(IReadOnlyList<string> lines, int from)
    {
        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Flora", "Fauna", "Faun", "Minerals", "Mineral", "Waypoints", "Planet Data", "RETURN" };
        var result = new List<string>();
        for (int i = from + 1; i < lines.Count; i++)
        {
            string line = Clean(lines[i]);
            if (stop.Contains(line) || line.Contains("Discovered", StringComparison.OrdinalIgnoreCase)) break;
            if (line.Length < 3 || line.Equals("x", StringComparison.OrdinalIgnoreCase)) continue;
            // Only real items: anything else means the list ended or something covers it
            // (a pop-up over the page read "m, Euben XIII" for Cadmium, 2026-10-17).
            string word = Snap(line, _resources!);
            if (!_resources!.Contains(word, StringComparer.OrdinalIgnoreCase)) break;
            if (!result.Contains(word, StringComparer.OrdinalIgnoreCase)) result.Add(word);
        }
        return result;
    }

    /// <summary>"0 / 6 Discovered" under Fauna (OCR reads the 0 as "O") - 6; "No Planetary Fauna" - 0.
    /// Flora and minerals show only what you found, so only fauna has a total.</summary>
    private static int? FaunaTotal(IReadOnlyList<string> lines)
    {
        if (lines.Any(l => l.Contains("No Planetary Fauna", StringComparison.OrdinalIgnoreCase))) return 0;
        foreach (var line in lines)
            if (Regex.Match(line.Replace('O', '0').Replace('o', '0'), @"(\d+)\s*/\s*(\d+)\s*Disc", RegexOptions.IgnoreCase) is { Success: true } m
                && int.TryParse(m.Groups[2].Value, out int total) && total is > 0 and < 100)
                return total;
        return null;
    }

    /// <summary>The planet's name: the line before "today by ..." / "Discovered by ...".</summary>
    private static string? Title(IReadOnlyList<string> lines)
    {
        for (int i = 1; i < lines.Count && i < 12; i++)
            if (Regex.IsMatch(lines[i], @"\bby\b", RegexOptions.IgnoreCase) && !lines[i - 1].Contains("ARTIFACT", StringComparison.OrdinalIgnoreCase))
            {
                string t = Clean(lines[i - 1]);
                if (t.Length >= 3 && !t.Equals("Discovered", StringComparison.OrdinalIgnoreCase) && !IsHud(t)) return t;
            }
        return null;
    }

    /// <summary>Drops the icon glyphs OCR reads as stray symbols at the start ("Ø Absent", "$ Cadmium", ", Izent").</summary>
    /// <summary>The currency bar and header above the page ("Nanites" was taken for a planet name).</summary>
    private static bool IsHud(string s) =>
        s.Contains("ARTIFACT", StringComparison.OrdinalIgnoreCase)
        || new[] { "Units", "Nanites", "Quicksilver", "Discoveries", "Catalog & Guide", "Discovered" }.Any(h => s.Equals(h, StringComparison.OrdinalIgnoreCase))
        || s.Count(char.IsLetter) < 3;

    internal static string Clean(string s)
    {
        s = s.Trim();
        s = Regex.Replace(s, @"^[^\p{L}\[]+", "");                 // leading symbols
        s = Regex.Replace(s, @"^\p{L}\s+(?=[\p{L}\[]{3,})", "");          // a lone letter read from an icon ("O ", "d ")
        return s.Trim();
    }

    /// <summary>The game word closest to what OCR read, or the text as read when none is close.</summary>
    internal static string Snap(string text, IEnumerable<string> words)
    {
        if (text.Length == 0) return "";
        var list = words.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.FirstOrDefault(w => w.Equals(text, StringComparison.OrdinalIgnoreCase)) is { } exact) return exact;
        string t = Norm(text);
        // A word read only in part ("Thin" of "Spread Thin").
        var partial = list.Where(w => t.Length >= 4 && Norm(w).EndsWith(t, StringComparison.Ordinal)).ToList();
        if (partial.Count == 1) return partial[0];
        var best = list.Select(w => (w, d: Distance(t, Norm(w)))).OrderBy(x => x.d).Take(2).ToList();
        if (best.Count > 0 && best[0].d <= Math.Max(1, t.Length / 4) && (best.Count == 1 || best[1].d > best[0].d)) return best[0].w;
        return text;
    }

    private static string TitleCase(string s) =>
        s.Any(char.IsUpper) ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);

    // ------------------------------------------------------------- the planet

    /// <summary>The systems the save recently put the player in, newest first.</summary>
    internal static List<ulong> Recent()
    {
        List<ulong> systems;
        lock (Lock) systems = RecentSystems.ToList();
        if (systems.Count == 0 && NmsSpaceStationPaths.ReadSystemAddress(LiveSaveWatcher.FromSession().Universe) is ulong current) systems.Add(current);
        return systems;
    }

    /// <summary>Resources from a Discoveries list pop-up: the lines after its "Verdant Planet"
    /// type line, up to its buttons. Stored only when it read at least one real item; the
    /// planet's other details stay. Returns a note for the log ("" when nothing).</summary>
    internal static string ReadPopupResources(IReadOnlyList<string> lines, int typeLine, ulong system, int body)
    {
        if (_resources is not { Count: > 0 }) return "";
        var found = new List<string>();
        for (int i = typeLine + 1; i < lines.Count; i++)
        {
            string line = Clean(lines[i]);
            if (Regex.IsMatch(line, @"RENAME|VIEW|ADD TO WONDERS|MARK (PLANET|MOON)", RegexOptions.IgnoreCase)) break;
            if (line.Length < 3) continue;
            string word = Snap(line, _resources);
            if (_resources.Contains(word, StringComparer.OrdinalIgnoreCase) && !found.Contains(word, StringComparer.OrdinalIgnoreCase)) found.Add(word);
        }
        if (found.Count == 0) return "";
        var existing = PlanetDetailsService.Get(system, body);
        if (existing is not null && existing.Resources.Count >= found.Count
            && found.All(r => existing.Resources.Contains(r, StringComparer.OrdinalIgnoreCase))) return "";
        var details = existing?.Copy() ?? new PlanetDetails();
        details.Resources = found;
        PlanetDetailsService.Save(system, body, details, source: "captured");
        return $"; resources {string.Join(", ", found)}";
    }

    /// <summary>The body (planet or moon) a name points to in a recently visited system.</summary>
    internal static (ulong System, int Planet, string Name)? MatchBody(string name) => Match(new[] { name });

    /// <summary>The planet the best of several readings of its name points to.</summary>
    private static (ulong System, int Planet, string Name)? Match(IReadOnlyList<string> titles)
    {
        List<ulong> systems;
        lock (Lock) systems = RecentSystems.ToList();
        if (systems.Count == 0 && NmsSpaceStationPaths.ReadSystemAddress(LiveSaveWatcher.FromSession().Universe) is ulong current) systems.Add(current);
        (ulong, int, string)? best = null;
        int bestScore = int.MaxValue;
        foreach (string title in titles.Distinct())
            if (Match(title, systems) is ((ulong, int, string) m, int score) && score < bestScore) { best = m; bestScore = score; }
        return best;
    }

    private static ((ulong System, int Planet, string Name), int Score)? Match(string title, List<ulong> systems)
    {
        string t = Norm(title);
        var scored = new List<(ulong System, int Planet, string Name, int Score)>();
        foreach (ulong system in systems)
        {
            var facts = UniverseInfo.For(system);
            for (int p = 1; p <= facts.PlanetCount + facts.MoonCount; p++)
                foreach (string name in new[] { UniverseInfo.PlanetName(system, p), UniverseInfo.GeneratedPlanetName(system, p) }.Distinct())
                {
                    string n = Norm(name);
                    // The title can lose its end ("Euben" for "Euben XIII"): a long enough start counts.
                    int score = n == t ? 0 : t.Length >= 4 && n.StartsWith(t, StringComparison.Ordinal) ? 1 : Distance(t, n);
                    scored.Add((system, p, name, score));
                }
        }
        var ordered = scored.OrderBy(s => s.Score).ToList();
        if (ordered.Count == 0) return null;
        var top = ordered[0];
        if (top.Score > Math.Max(2, t.Length / 4)) return null;
        // Unique: no other planet scores as well.
        if (ordered.Skip(1).Any(s => s.Score == top.Score && (s.System != top.System || s.Planet != top.Planet))) return null;
        return ((top.System, top.Planet, top.Name), top.Score);
    }

    /// <summary>Lower-case letters and digits, with the usual OCR swaps folded (8/B, 0/O, 1/l/I, 5/S).</summary>
    internal static string Norm(string s)
    {
        var chars = s.ToUpperInvariant().Where(char.IsLetterOrDigit).Select(c => c switch
        {
            '8' => 'B', '0' => 'O', '1' => 'I', 'L' => 'I', '5' => 'S', _ => c,
        });
        return new string(chars.ToArray());
    }

    internal static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
