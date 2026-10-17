using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Fills in a system's notes (the Planets page's "Your notes on this system") from two
/// game screens, as game screen recording reads them (GitHub #18, 2026-10-17):
/// - the Discoveries system pop-up: "Celestial Bodies: 3 Planets // 3 Moons",
///   "Dominant Lifeform: Korvax", "Economy: High Tech // Prosperous", "Conflict level: Low";
/// - the Galaxy Map card for the system you're in ("Amogat-Aizu XIV (Current Location)"):
///   "G5pf // Water", Race, Economy, "... // Prosperous", Conflict.
/// The system is found by name among the systems the save recently put the player in -
/// a card for another system can't be tied to an address, so it's skipped. Values must
/// be the game's own words (catalog SystemDescriptors); only fields read are changed,
/// and the player's notes, ship marks and names are kept.
/// </summary>
public static class SystemPageReader
{
    private static List<(string Word, string Group, string Tier)>? _words;
    private static readonly string[] Races = { "Gek", "Korvax", "Vy'keen", "Autophage" };

    public static int Filled { get; private set; }

    /// <summary>Raised (any thread) when a system's notes were filled in from the screen.</summary>
    public static event Action<ulong>? Captured;

    public static async Task<string?> TryReadAsync(IReadOnlyList<string> lines)
    {
        // Labels can carry a stray icon read in front ("C) Celestial Bodies: ...").
        bool popup = lines.Any(l => l.Contains("Celestial Bodies", StringComparison.OrdinalIgnoreCase))
            && lines.Any(l => l.Contains("Conflict level", StringComparison.OrdinalIgnoreCase));
        // The Galaxy Map card: "REGION : ..." under the system's name, then Race / Economy / Conflict.
        int region = lines.ToList().FindIndex(l => Regex.IsMatch(l, @"\bR\S{1,4}ON\S*\s*:", RegexOptions.IgnoreCase));
        bool card = region > 0 && lines.Skip(region).Take(12).Any(l => Is(PlanetPageReader.Clean(l), "Race"));
        if (!popup && !card) return null;

        _words ??= await CatalogService.GetSystemDescriptorsAsync();
        var read = new SystemNote();
        string? name;
        if (popup)
        {
            name = PopupName(lines);
            if (After(lines, "Celestial Bodies") is { } bodies && Regex.Match(bodies, @"(\d+)\s*Moon", RegexOptions.IgnoreCase) is { Success: true } m)
                read.Moons = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            else if (After(lines, "Celestial Bodies") is { } b2 && !b2.Contains("Moon", StringComparison.OrdinalIgnoreCase) && b2.Contains("Planet", StringComparison.OrdinalIgnoreCase))
                read.Moons = 0;
            read.Race = RaceWord(After(lines, "Dominant Lifeform"));
            if (After(lines, "Economy") is { } economy)
            {
                var parts = economy.Split("//", StringSplitOptions.TrimEntries);
                read.Economy = Word("Economy", parts[0]);
                if (parts.Length > 1) read.Wealth = Word("Wealth", parts[^1]);
            }
            read.Conflict = Word("Conflict", After(lines, "Conflict level") ?? "");
        }
        else
        {
            // Usually the line above REGION, but a nearby star's label can land in between
            // ("Ots" from Totsbursle, 2026-10-17): take the first of the 3 lines above that
            // names a system you were recently in.
            name = PlanetPageReader.Clean(Regex.Replace(lines[region - 1], @"\(.*$", ""));
            for (int k = region - 1; k >= 0 && k >= region - 3; k--)
            {
                string candidate = PlanetPageReader.Clean(Regex.Replace(lines[k], @"\(.*$", ""));
                if (candidate.Length >= 3 && Match(candidate) is not null) { name = candidate; break; }
            }
            // The busy star field behind the card scatters other systems' labels through the
            // text, so each field is taken from the line right after its label.
            for (int i = region + 1; i < lines.Count && i < region + 16; i++)
            {
                string line = PlanetPageReader.Clean(lines[i]);
                if (read.StarClass == "" && i <= region + 3
                    && Regex.Match(lines[i], @"(?<![A-Za-z])([OBAFGKMEXY]\d{1,2}[a-z]{0,3})(?![A-Za-z])") is { Success: true } cls)
                {
                    read.StarClass = cls.Groups[1].Value;
                    read.Water = Regex.IsMatch(lines[i], @"W\w{0,2}ter", RegexOptions.IgnoreCase);
                }
                else if (Is(line, "Race") && i + 1 < lines.Count) read.Race = RaceWord(lines[i + 1]);
                else if (Is(line, "Economy") && i + 1 < lines.Count) read.Economy = Word("Economy", lines[i + 1]);
                else if (Is(line, "Conflict") && i + 1 < lines.Count) read.Conflict = Word("Conflict", lines[i + 1]);
                if (lines[i].Contains('/') && Word("Wealth", lines[i].Split('/')[^1]) is { Length: > 0 } wealth)
                    read.Wealth = wealth;
            }
        }
        if (name is not { Length: >= 3 }) return "system page: no system name";
        if (Match(name) is not ulong system) return $"system page \"{name}\": not a system you were recently in";

        var note = SystemNotesService.Get(system) ?? new SystemNote();
        bool changed = false;
        void Set(string value, Func<string> get, Action<string> set)
        {
            if (value.Length > 0 && get() != value) { set(value); changed = true; }
        }
        Set(read.Race, () => note.Race, v => note.Race = v);
        Set(read.Economy, () => note.Economy, v => note.Economy = v);
        Set(read.Wealth, () => note.Wealth, v => note.Wealth = v);
        Set(read.Conflict, () => note.Conflict, v => note.Conflict = v);
        // A class read with its end cut off ("G5p" for G5pf) never replaces the fuller one.
        if (!note.StarClass.StartsWith(read.StarClass, StringComparison.OrdinalIgnoreCase) || note.StarClass.Length == 0)
            Set(read.StarClass, () => note.StarClass, v => note.StarClass = v);
        if (read.StarClass.Length > 0 && SystemNotesService.ColourForClass(read.StarClass) is { } colour)
            Set(colour, () => note.StarColour, v => note.StarColour = v);
        if (read.StarClass.Length > 0 && read.Water && !note.Water) { note.Water = true; changed = true; }
        if (read.Moons is int moons && note.Moons != moons) { note.Moons = moons; changed = true; }
        string what = string.Join(" / ", new[] { read.StarClass, read.Water ? "Water" : "", read.Race, read.Economy, read.Wealth, read.Conflict,
            read.Moons is int n ? $"{n} moons" : "" }.Where(s => s.Length > 0));
        if (!changed) return $"system page {name}: already up to date ({what})";
        SystemNotesService.Save(system, note, source: "captured");
        Filled++;
        Captured?.Invoke(system);
        return $"system page {name}: saved ({what})";
    }

    /// <summary>The system shown when the screen is a system's own Discoveries view with no
    /// pop-up over it ("3 PLANETS / 3 MOONS" under its name, its planets drawn on the right) -
    /// the view the Planets page borrows a picture of. Null for anything else.</summary>
    public static ulong? SystemView(IReadOnlyList<string> lines)
    {
        int header = lines.ToList().FindIndex(l => Regex.IsMatch(l, @"\d+\s*PLANETS?\s*/", RegexOptions.IgnoreCase));
        if (header < 0) return null;
        // A pop-up or a planet page covers the planets.
        if (lines.Any(l => l.Contains("Galaxy:", StringComparison.OrdinalIgnoreCase) || l.Contains("RENAME AND UPLOAD", StringComparison.OrdinalIgnoreCase)
            || l.Trim().Equals("Planet Data", StringComparison.OrdinalIgnoreCase)))
            return null;
        for (int i = header - 1; i >= 0 && i >= header - 4; i--)
        {
            string line = PlanetPageReader.Clean(lines[i]);
            if (line.Count(char.IsLetter) < 3 || Regex.IsMatch(line, @"\bby\b|Discovered", RegexOptions.IgnoreCase)) continue;
            return Match(line);
        }
        return null;
    }

    /// <summary>A planet page listed "Dissonance detected": the system is dissonant.</summary>
    public static void MarkDissonant(ulong system)
    {
        var note = SystemNotesService.Get(system) ?? new SystemNote();
        if (note.Dissonant) return;
        note.Dissonant = true;
        SystemNotesService.Save(system, note, source: "captured");
        Captured?.Invoke(system);
    }

    private static bool Is(string line, string label) => line.Equals(label, StringComparison.OrdinalIgnoreCase)
        || PlanetPageReader.Distance(PlanetPageReader.Norm(line), PlanetPageReader.Norm(label)) <= 1;

    /// <summary>The text after "Label:" on its line.</summary>
    private static string? After(IReadOnlyList<string> lines, string label)
    {
        foreach (var raw in lines)
        {
            int at = raw.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;
            string line = raw[at..];
            if (line.IndexOf(':') is int colon and > 0)
            {
                string value = line[(colon + 1)..].Trim();
                return value.Contains("Data Unavailable", StringComparison.OrdinalIgnoreCase) ? "" : value;
            }
        }
        return null;
    }

    /// <summary>The pop-up's own title: above "Galaxy:", past the discoverer line and
    /// stray icon reads ("9").</summary>
    private static string? PopupName(IReadOnlyList<string> lines)
    {
        int galaxy = lines.ToList().FindIndex(l => l.Contains("Galaxy:", StringComparison.OrdinalIgnoreCase));
        for (int i = galaxy - 1; i >= 0 && i >= galaxy - 4; i--)
        {
            string line = PlanetPageReader.Clean(lines[i]);
            if (line.Count(char.IsLetter) < 3 || line.Contains("Discovered", StringComparison.OrdinalIgnoreCase)) continue;
            return line;
        }
        return null;
    }

    private static string Word(string group, string text)
    {
        text = PlanetPageReader.Clean(text);
        if (text.Length == 0) return "";
        var words = _words!.Where(w => w.Group == group).Select(w => w.Word).ToList();
        string word = PlanetPageReader.Snap(text, words);
        return words.Contains(word, StringComparer.OrdinalIgnoreCase) ? word : "";
    }

    private static string RaceWord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        if (text.Contains("Uncharted", StringComparison.OrdinalIgnoreCase)) return "None (uncharted)";
        string word = PlanetPageReader.Snap(PlanetPageReader.Clean(text), Races);
        return Races.Contains(word) ? word : "";
    }

    internal static ulong? MatchSystem(string name) => Match(name);

    /// <summary>"High Tech // Prosperous" read elsewhere (the trade terminal banner): fills the
    /// system's economy and wealth like the pop-up does. Returns a note, or null if unchanged.</summary>
    internal static async Task<string?> TryReadEconomyAsync(ulong system, string text)
    {
        _words ??= await CatalogService.GetSystemDescriptorsAsync();
        var parts = text.Split("//", StringSplitOptions.TrimEntries);
        string economy = Word("Economy", parts[0]), wealth = parts.Length > 1 ? Word("Wealth", parts[^1]) : "";
        var note = SystemNotesService.Get(system) ?? new SystemNote();
        bool changed = false;
        if (economy.Length > 0 && note.Economy != economy) { note.Economy = economy; changed = true; }
        if (wealth.Length > 0 && note.Wealth != wealth) { note.Wealth = wealth; changed = true; }
        if (!changed) return null;
        SystemNotesService.Save(system, note, source: "captured");
        Captured?.Invoke(system);
        return $"economy {economy} / {wealth}";
    }

    private static ulong? Match(string name)
    {
        string t = PlanetPageReader.Norm(name);
        var scored = PlanetPageReader.Recent()
            .Select(s => (System: s, Score: new[] { UniverseInfo.SystemName(s), UniverseInfo.For(s).GeneratedName }
                .Distinct().Min(n => PlanetPageReader.Distance(t, PlanetPageReader.Norm(n)))))
            .OrderBy(x => x.Score).ToList();
        if (scored.Count == 0 || scored[0].Score > Math.Max(1, t.Length / 4)) return null;
        if (scored.Count > 1 && scored[1].Score == scored[0].Score) return null;
        return scored[0].System;
    }
}
