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
    // Where the last save put the player: the system and planet (0 = in space / the station).
    private static (ulong System, int Planet)? _here;

    /// <summary>The planet the player is on in that system per the last save (0 in space or
    /// at the station), or null when the save puts them elsewhere.</summary>
    // Kinds of fauna the player has discovered per planet address, from the last save.
    private static Dictionary<ulong, int> _faunaFound = new();

    /// <summary>The planet whose fauna list this is, when the screen doesn't name it: the save
    /// counts the animals you've discovered on each planet, and the list says "4/4 Species
    /// Discovered". The planet you're on wins when its count matches; else the only planet of
    /// a recent system with that count. Synthetic planets' notes are code, not "planet X"
    /// (Karat 46/H3, 2026-10-18).</summary>
    internal static (ulong System, int Planet)? PlanetWithFauna(int discovered)
    {
        if (discovered <= 0) return null;
        Dictionary<ulong, int> found;
        (ulong System, int Planet)? here;
        lock (Lock) { found = _faunaFound; here = _here; }
        var recent = Recent();
        var matches = found.Where(kv => kv.Value == discovered)
            .Select(kv => (System: NmsSpaceStationPaths.ToSystemAddress(kv.Key), Planet: (int)((kv.Key >> 52) & 0xF)))
            .Where(m => recent.Contains(m.System)).Distinct().ToList();
        if (here is { } h && matches.Contains(h)) return h;
        return matches.Count == 1 ? matches[0] : null;
    }

    internal static int? CurrentPlanet(ulong system)
    {
        lock (Lock) return _here is { } h && h.System == system ? h.Planet : null;
    }
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
                _here = (system, (int?)snapshot.Universe?["oZw"]?["jsv"] ?? 0);
                _faunaFound = ArtifactX.Core.NmsModels.NmsPlanets.CountDiscoveries(snapshot.Discoveries)
                    .Where(kv => kv.Value.Fauna > 0).ToDictionary(kv => kv.Key, kv => kv.Value.Fauna);
                if (RecentSystems.Count > 3) RecentSystems.RemoveAt(3);
            }
        };
    }

    /// <summary>Called with each recorded screen's text lines. Returns a short note of
    /// what happened (for the capture log), or null when it isn't a planet page.</summary>
    /// <summary>The card the game shows as you fly towards a planet (2026-10-17): name, "Arrive in
    /// 00:00:28", type ("Metallurgic Planet"), resources and sentinel level ("Aggressive Sentinels").
    /// Fills resources and sentinels only where the planet has none yet - the planet page's exact
    /// word wins. A name ArtifactX doesn't know is held for the player to place.</summary>
    public static async Task<string?> TryReadFlightCardAsync(IReadOnlyList<string> lines)
    {
        int arrive = lines.ToList().FindIndex(l => Regex.IsMatch(l, @"Arrive\s+in\b", RegexOptions.IgnoreCase));
        if (arrive < 0) return null;
        // A station / ship marker says "ARRIVE IN: 0:33" - a planet card has no colon ("SPACE STATION"
        // was held as a planet name, 2026-10-18).
        if (Regex.IsMatch(lines[arrive], @"Arrive\s+in\s*:", RegexOptions.IgnoreCase)
            || lines.Any(l => Regex.IsMatch(l.Trim(), @"^(SPACE|ATLAS)\s+STATION$", RegexOptions.IgnoreCase))) return null;
        int type = -1;
        for (int i = arrive + 1; i < lines.Count && i <= arrive + 4; i++)
            // "Metallurgic Planet", "Poisonous Moon" - or an exotic's own words ("Harsh Blue Globe", 2026-10-17).
            if (Regex.IsMatch(lines[i].Trim(), @"^[\p{L}\[\]'-]+(\s[\p{L}\[\]'-]+){1,3}$") && !lines[i].Contains("Arrive", StringComparison.OrdinalIgnoreCase)) { type = i; break; }
        if (type < 0) return null;
        await EnsureWordsAsync();

        // The name: above the distance line, past "You discovered this" / "Discovered by ...".
        string? name = null;
        for (int i = arrive - 1; i >= 0 && i >= arrive - 4; i--)
        {
            string line = Clean(lines[i]);
            // The distance can come out garbled ("261,933u" read as "26T,i33u", 2026-10-17): test the raw line, numbers and all.
            if (Regex.IsMatch(line, @"discovered|^\d|ks$|\bu$|^(Moon|Planet)\s+of\b", RegexOptions.IgnoreCase) || line.Count(char.IsLetter) < 3
                || LooksLikeDistance(lines[i])) continue;
            name = line;
            break;
        }
        if (name is null) return "flight card: no planet name";
        string flightDescriptor = lines[type].Trim();
        bool moon = flightDescriptor.EndsWith("Moon", StringComparison.OrdinalIgnoreCase);

        var resources = new List<string>();
        string sentinels = "";
        for (int i = type + 1; i < lines.Count && i <= type + 8; i++)
        {
            string line = Clean(lines[i]);
            if (SentinelLine(line) is { } read)
            {
                sentinels = read;
                break;
            }
            string res = Snap(line, _resources!);
            if (_resources!.Contains(res, StringComparer.OrdinalIgnoreCase) && !resources.Contains(res, StringComparer.OrdinalIgnoreCase)) resources.Add(res);
            else if (resources.Count > 0) break;
        }
        if (resources.Count == 0 && sentinels.Length == 0) return "flight card: nothing readable";
        // "Dissonance detected" on the card marks the system dissonant (the card shows it last, 2026-10-17).
        bool dissonant = resources.Remove("Dissonance detected") | lines.Skip(type + 1).Take(10)
            .Any(l => l.Contains("Dissonance detected", StringComparison.OrdinalIgnoreCase));
        if (dissonant && (MatchBody(name)?.Item1 ?? Recent().FirstOrDefault()) is ulong here && here != 0)
            SystemPageReader.MarkDissonant(here);

        if (MatchBody(name) is not (ulong system, int body, string matched))
        {
            UnmatchedBodiesService.Hold(name, new PlanetDetails { Sentinels = sentinels, Resources = resources, Descriptor = flightDescriptor }, moon);
            return $"flight card \"{name}\": unknown name - held for you to place on Current System";
        }
        BodyKindsService.Store(system, body, moon);
        var existing = PlanetDetailsService.Get(system, body);
        var details = existing?.Copy() ?? new PlanetDetails();
        bool changed = false;
        if (details.Descriptor.Length == 0 && flightDescriptor.Length > 0) { details.Descriptor = flightDescriptor; changed = true; }
        if (details.Sentinels.Length == 0 && sentinels.Length > 0) { details.Sentinels = sentinels; changed = true; }
        if (details.Resources.Count == 0 && resources.Count > 0) { details.Resources = resources; changed = true; }
        if (!changed) return $"flight card {matched}: already known";
        PlanetDetailsService.Save(system, body, details, source: "captured");
        Filled++;
        return $"flight card {matched}: saved ({flightDescriptor} / {sentinels} / {string.Join(", ", resources)})";
    }

    /// <summary>A distance ("261,933u", even garbled as "26T,i33u"): a digit beside a comma, or
    /// digits ending in "u". Names can hold digits ("Mino 68/P1"), never a thousands comma.</summary>
    public static bool LooksLikeDistance(string raw) =>
        Regex.IsMatch(raw.Trim(), @"\d\s*,|,\s*\S{0,2}\d|\d\w{0,2}u$", RegexOptions.IgnoreCase);

    /// <summary>On a planet: the box shown after landing and the analysis visor list Weather /
    /// Sentinels / Flora / Fauna with each value beside its label (2026-10-17). Values are paired
    /// by height on screen - reading order mixes them up, and flora and fauna use the same words.
    /// The planet is the one the last save put the player on. Only empty fields are filled; the
    /// visor's "Available Resources" fill resources when none are recorded.</summary>
    public static async Task<string?> TryReadOnPlanetAsync(IReadOnlyList<(string Text, double Y)> placed)
    {
        var lines = placed.Select(p => p.Text).ToList();
        if (lines.Any(l => l.Trim().Equals("Planet Data", StringComparison.OrdinalIgnoreCase))) return null; // the Discoveries page
        // The teleporter lists the same four for the DESTINATION's planet, not this one (2026-10-18).
        if (lines.Any(l => Regex.IsMatch(l, @"TELEPORTATION\s+TERMINAL|^\s*Warp\s+to\b|Last\s+edited\s+by", RegexOptions.IgnoreCase))) return null;
        var labels = new[] { "Weather", "Sentinels", "Flora", "Fauna" };
        var found = labels.Select(lb => placed.FirstOrDefault(p => p.Text.Trim().Equals(lb, StringComparison.OrdinalIgnoreCase))).ToList();
        if (found.Count(f => f.Text is not null) < 3) return null;
        ulong system = Recent().FirstOrDefault();
        if (system == 0 || CurrentPlanet(system) is not int body || body <= 0) return "planet box: not on a planet per the save";
        await EnsureWordsAsync();

        string Value(int k, string group)
        {
            if (found[k].Text is null) return "";
            var words = _words!.Where(w => w.Group == group).Select(w => w.Word).ToList();
            foreach (var p in placed.Where(p => Math.Abs(p.Y - found[k].Y) < 12 && !labels.Contains(p.Text.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                string word = Snap(Clean(p.Text), words);
                if (words.Contains(word, StringComparer.OrdinalIgnoreCase)) return word;
            }
            return "";
        }
        var read = new PlanetDetails
        {
            Weather = Value(0, "Weather"), Sentinels = Value(1, "Sentinels"), Flora = Value(2, "Life"), Fauna = Value(3, "Life"),
        };
        int avail = lines.FindIndex(l => l.Contains("Available Resources", StringComparison.OrdinalIgnoreCase));
        if (avail >= 0)
            for (int i = avail + 1; i < lines.Count && i <= avail + 8; i++)
            {
                string r = Snap(Clean(Regex.Replace(lines[i], @"[^\p{L}\s-]+$", "")), _resources!);
                if (_resources!.Contains(r, StringComparer.OrdinalIgnoreCase)) { if (!read.Resources.Contains(r, StringComparer.OrdinalIgnoreCase)) read.Resources.Add(r); }
                else if (read.Resources.Count > 0) break;
            }

        var existing = PlanetDetailsService.Get(system, body);
        var details = existing?.Copy() ?? new PlanetDetails();
        bool changed = false;
        void Fill(string value, Func<string> get, Action<string> set) { if (value.Length > 0 && get().Length == 0) { set(value); changed = true; } }
        Fill(read.Weather, () => details.Weather, v => details.Weather = v);
        Fill(read.Sentinels, () => details.Sentinels, v => details.Sentinels = v);
        Fill(read.Flora, () => details.Flora, v => details.Flora = v);
        Fill(read.Fauna, () => details.Fauna, v => details.Fauna = v);
        string? onPlanetDescriptor = lines.Select(Clean).FirstOrDefault(l =>
            IsBodyType(l)
            && !labels.Contains(l, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(onPlanetDescriptor))
            Fill(onPlanetDescriptor, () => details.Descriptor, v => details.Descriptor = v);
        if (details.Resources.Count == 0 && read.Resources.Count > 0) { details.Resources = read.Resources; changed = true; }
        string what = $"{read.Weather} / {read.Sentinels} / {read.Flora} / {read.Fauna}{(read.Resources.Count > 0 ? " / " + string.Join(", ", read.Resources) : "")}";
        if (!changed) return $"planet box {UniverseInfo.PlanetName(system, body)}: nothing new ({what})";
        PlanetDetailsService.Save(system, body, details, source: "captured");
        Filled++;
        return $"planet box {UniverseInfo.PlanetName(system, body)}: saved ({what})";
    }

    /// <param name="titleReads">Reads the title area again on its own: the big planet name
    /// is often missed in the whole-screen read ("Guhul VI" read as nothing, 2026-10-17),
    /// while a tight, shrunk crop of it reads well.</param>
    /// <param name="dataReads">Reads the Planet Data column again on its own, for a field the
    /// whole-screen read left empty (Lefredid XVI's sentinels, "Frenzied", 2026-10-20).</param>
    public static async Task<string?> TryReadAsync(IReadOnlyList<string> lines, Func<Task<IReadOnlyList<string>>>? titleReads = null,
        Func<Task<IReadOnlyList<string>>>? dataReads = null)
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
            Descriptor = Descriptor(lines) ?? "",
            Resources = Resources(lines, res),
            FaunaTotal = FaunaTotal(lines),
        };
        if (dataReads is not null && (details.Weather == "" || details.Sentinels == "" || details.Flora == "" || details.Fauna == ""))
        {
            var column = await dataReads();
            if (details.Weather == "") details.Weather = Field(column, -1, "Weather", "Weather");
            if (details.Sentinels == "") details.Sentinels = Field(column, -1, "Sentinels", "Sentinels");
            if (details.Flora == "") details.Flora = Field(column, -1, "Flora", "Life");
            if (details.Fauna == "") details.Fauna = Field(column, -1, "Fauna", "Life");
        }
        if (details.Weather == "" && details.Sentinels == "" && details.Resources.Count == 0) return "planet page: nothing readable";

        var titles = new List<string>();
        if (Title(lines) is { } fromScreen) titles.Add(fromScreen);
        string? cropTitle = null;
        if (titleReads is not null)
            foreach (var read in await titleReads())
                if (Clean(read) is { Length: >= 3 } t && !IsHud(t)) { titles.Add(t); cropTitle ??= t; }
        if (titles.Count == 0) return "planet page: no planet name";
        var match = Match(titles);
        if (match is not (ulong system, int planet, string name))
        {
            // A name ArtifactX doesn't know (another player renamed it): held for the player to place.
            UnmatchedBodiesService.Hold(cropTitle ?? titles[0], details, null);
            return $"planet page \"{string.Join("\" / \"", titles.Distinct())}\": unknown name - held for you to place on Current System";
        }

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
            if (details.Descriptor == "") details.Descriptor = existing.Descriptor;
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
        && a.Descriptor == b.Descriptor
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
            .Concat(new[] { "Dissonance detected", "Vile Brood Detected", "Swarm Debris" })
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
    /// Flora and minerals show only what you found, so only fauna has a total. OCR often reads
    /// the count and "Discovered" as two lines ("0/11" then "Discovered", Lefredid XVI,
    /// 2026-10-20 - in crops too), so a count line followed by one is taken as well.</summary>
    private static int? FaunaTotal(IReadOnlyList<string> lines)
    {
        if (lines.Any(l => l.Contains("No Planetary Fauna", StringComparison.OrdinalIgnoreCase))) return 0;
        static string Digits(string s) => s.Replace('O', '0').Replace('o', '0');
        for (int i = 0; i < lines.Count; i++)
        {
            var m = Regex.Match(Digits(lines[i]), @"(\d+)\s*/\s*(\d+)\s*Disc", RegexOptions.IgnoreCase);
            if (!m.Success && i + 1 < lines.Count && lines[i + 1].TrimStart().StartsWith("Disc", StringComparison.OrdinalIgnoreCase))
                m = Regex.Match(Digits(lines[i]), @"^\s*(\d+)\s*/\s*(\d+)\s*$");
            if (m.Success && int.TryParse(m.Groups[2].Value, out int total) && total is > 0 and < 100)
                return total;
        }
        return null;
    }

    /// <summary>Specific descriptor ("Crimson Planet", "Paradise Planet", "Contaminated Moon") on the discovery page.</summary>
    private static string? Descriptor(IReadOnlyList<string> lines)
    {
        int data = lines.ToList().FindIndex(l => l.Trim().Equals("Planet Data", StringComparison.OrdinalIgnoreCase));
        int by = lines.ToList().FindIndex(l => Regex.IsMatch(l, @"\bby\b", RegexOptions.IgnoreCase));
        if (by >= 0)
        {
            int max = data > by ? data : Math.Min(lines.Count, by + 4);
            for (int i = by + 1; i < max; i++)
            {
                string line = Clean(lines[i]);
                if (IsBodyType(line))
                    return line;
            }
        }
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

    /// <summary>A body's type as the game words it: "Verdant Planet", "Contaminated Moon",
    /// "[REDACTED] Planet" - or "Planetary Anomaly" for an anomaly world, which was
    /// skipped until 2026-10-21 (Aukel). Not "Space Anomaly", the station.</summary>
    internal static bool IsBodyType(string line) =>
        Regex.IsMatch(line, @"^[\p{L} \[\]'-]{3,30}\s(Moon|Planet|(?<!Space\s)Anomaly)$", RegexOptions.IgnoreCase);

    /// <summary>True for an anomaly's type, which doesn't say whether it's a planet or a moon.</summary>
    internal static bool IsAnomalyType(string type) => type.EndsWith("Anomaly", StringComparison.OrdinalIgnoreCase);

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
    public static List<ulong> Recent()
    {
        var systems = new HashSet<ulong>();
        lock (Lock)
        {
            foreach (var s in RecentSystems) systems.Add(s);
        }
        if (NmsSpaceStationPaths.ReadSystemAddress(LiveSaveWatcher.FromSession().Universe) is ulong current && current != 0)
            systems.Add(current);

        try
        {
            var snap = LiveSaveWatcher.FromSession();
            if (snap.Discoveries is not null)
            {
                // Every system the save knows - one with no planet record yet counts too.
                foreach (var s in NmsPlanets.ReadSystems(snap.Discoveries))
                    systems.Add(s.SystemAddress);
            }
        }
        catch { }

        return systems.ToList();
    }

    /// <summary>Resources from a Discoveries list pop-up: the lines after its "Verdant Planet"
    /// type line, up to its buttons. Stored only when it read at least one real item; the
    /// planet's other details stay. Returns a note for the log ("" when nothing).</summary>
    internal static string ReadPopupResources(IReadOnlyList<string> lines, int typeLine, ulong system, int body)
    {
        var found = PopupResources(lines, typeLine);
        if (found.Count == 0) return "";
        var existing = PlanetDetailsService.Get(system, body);
        if (existing is not null && existing.Resources.Count >= found.Count
            && found.All(r => existing.Resources.Contains(r, StringComparer.OrdinalIgnoreCase))) return "";
        var details = existing?.Copy() ?? new PlanetDetails();
        details.Resources = found;
        PlanetDetailsService.Save(system, body, details, source: "captured");
        return $"; resources {string.Join(", ", found)}";
    }

    /// <summary>The real items a Discoveries list pop-up lists after its type line, up to its buttons.</summary>
    internal static List<string> PopupResources(IReadOnlyList<string> lines, int typeLine)
    {
        var found = new List<string>();
        if (_resources is not { Count: > 0 }) return found;
        for (int i = typeLine + 1; i < lines.Count; i++)
        {
            string line = Clean(lines[i]);
            if (Regex.IsMatch(line, @"RENAME|VIEW|ADD TO WONDERS|MARK (PLANET|MOON)", RegexOptions.IgnoreCase)) break;
            if (line.Length < 3) continue;
            string word = Snap(line, _resources);
            if (_resources.Contains(word, StringComparer.OrdinalIgnoreCase) && !found.Contains(word, StringComparer.OrdinalIgnoreCase)) found.Add(word);
        }
        return found;
    }

    /// <summary>A sentinel line on a flight card or Discoveries pop-up ("Aggressive Sentinels",
    /// "Low Sentinel Activity") as the game's word for it - "" when it's a sentinel line
    /// without one of the game's words ("High Sentinel Activity": the planet's page then
    /// gives the word), null when it isn't a sentinel line. The pop-up shows the level; the
    /// planet's page shows the word for it (Lefredid XVI: "Aggressive Sentinels" on hover,
    /// "Frenzied" on its page - a word of the same level), so the page's word wins.</summary>
    internal static string? SentinelLine(string line)
    {
        if (Regex.Match(Clean(line), @"^(.+?)\s+Sentinel(s|\s+Activity)$", RegexOptions.IgnoreCase) is not { Success: true } m) return null;
        var words = _words?.Where(w => w.Group == "Sentinels").Select(w => w.Word).ToList() ?? new();
        string word = Snap(m.Groups[1].Value, words);
        return words.Contains(word, StringComparer.OrdinalIgnoreCase) ? word : "";
    }

    /// <summary>The sentinel word a Discoveries list pop-up shows after its type line, "" if none.</summary>
    internal static string PopupSentinels(IReadOnlyList<string> lines, int typeLine)
    {
        for (int i = typeLine + 1; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], @"RENAME|VIEW|ADD TO WONDERS|MARK (PLANET|MOON)", RegexOptions.IgnoreCase)) break;
            if (SentinelLine(lines[i]) is { } word) return word;
        }
        return "";
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
    public static string Norm(string s)
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
