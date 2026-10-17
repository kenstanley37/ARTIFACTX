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
    public static async Task<string?> TryReadAsync(IReadOnlyList<string> lines)
    {
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
        };
        if (details.Weather == "" && details.Sentinels == "" && details.Resources.Count == 0) return "planet page: nothing readable";

        string? title = Title(lines);
        if (title is null) return "planet page: no planet name";
        var match = Match(title);
        if (match is not (ulong system, int planet, string name)) return $"planet page \"{title}\": no single matching planet in recent systems";

        // The player's own notes stay; the fields are what the game showed.
        var existing = PlanetDetailsService.Get(system, planet);
        details.Notes = existing?.Notes.ToList() ?? new();
        // A partly covered page never shrinks a fuller list already recorded.
        if (existing is { Resources.Count: > 0 } && details.Resources.Count < existing.Resources.Count
            && details.Resources.All(r => existing.Resources.Contains(r, StringComparer.OrdinalIgnoreCase)))
            details.Resources = existing.Resources.ToList();
        if (existing is not null && Same(existing, details)) return $"planet page {name}: already up to date";
        PlanetDetailsService.Save(system, planet, details, source: "captured");
        Filled++;
        return $"planet page {name}: saved ({details.Weather} / {details.Sentinels} / {details.Flora} / {details.Fauna} / {string.Join(", ", details.Resources)})";
    }

    private static bool Same(PlanetDetails a, PlanetDetails b) =>
        a.Weather == b.Weather && a.Sentinels == b.Sentinels && a.Flora == b.Flora && a.Fauna == b.Fauna
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

    /// <summary>The line after the label (first one after "Planet Data"), snapped to the game's words.</summary>
    private static string Field(IReadOnlyList<string> lines, int from, string label, string group)
    {
        for (int i = from + 1; i < lines.Count - 1; i++)
            if (lines[i].Trim().Equals(label, StringComparison.OrdinalIgnoreCase))
                return Snap(Clean(lines[i + 1]), _words!.Where(w => w.Group == group).Select(w => w.Word));
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

    /// <summary>The planet's name: the line before "today by ..." / "Discovered by ...".</summary>
    private static string? Title(IReadOnlyList<string> lines)
    {
        for (int i = 1; i < lines.Count && i < 12; i++)
            if (Regex.IsMatch(lines[i], @"\bby\b", RegexOptions.IgnoreCase) && !lines[i - 1].Contains("ARTIFACT", StringComparison.OrdinalIgnoreCase))
            {
                string t = Clean(lines[i - 1]);
                if (t.Length >= 3 && !t.Equals("Discovered", StringComparison.OrdinalIgnoreCase)) return t;
            }
        return null;
    }

    /// <summary>Drops the icon glyphs OCR reads as stray symbols at the start ("Ø Absent", "$ Cadmium", ", Izent").</summary>
    private static string Clean(string s)
    {
        s = s.Trim();
        s = Regex.Replace(s, @"^[^\p{L}\[]+", "");                 // leading symbols
        s = Regex.Replace(s, @"^\p{L}\s+(?=[\p{L}\[]{3,})", "");          // a lone letter read from an icon ("O ", "d ")
        return s.Trim();
    }

    /// <summary>The game word closest to what OCR read, or the text as read when none is close.</summary>
    private static string Snap(string text, IEnumerable<string> words)
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

    private static (ulong System, int Planet, string Name)? Match(string title)
    {
        List<ulong> systems;
        lock (Lock) systems = RecentSystems.ToList();
        if (systems.Count == 0 && NmsSpaceStationPaths.ReadSystemAddress(LiveSaveWatcher.FromSession().Universe) is ulong current) systems.Add(current);
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
        return (top.System, top.Planet, top.Name);
    }

    /// <summary>Lower-case letters and digits, with the usual OCR swaps folded (8/B, 0/O, 1/l/I, 5/S).</summary>
    private static string Norm(string s)
    {
        var chars = s.ToUpperInvariant().Where(char.IsLetterOrDigit).Select(c => c switch
        {
            '8' => 'B', '0' => 'O', '1' => 'I', 'L' => 'I', '5' => 'S', _ => c,
        });
        return new string(chars.ToArray());
    }

    private static int Distance(string a, string b)
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
