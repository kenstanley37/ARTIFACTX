using ArtifactX.Core.NmsModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The wording Current System, the Planet Finder and a planet's page share: what you
/// found and how long you were there, one-line summaries of what you recorded, and
/// where a planet is from you.
/// </summary>
public static class PlanetText
{
    /// <summary>"2 / 6 fauna · 0 flora · 0 minerals": fauna out of how many kinds the
    /// planet has, when its Discoveries page said (screen recording, or typed in).</summary>
    public static string Discovered(ulong planetAddress, PlanetDiscoveryCounts? tally)
    {
        var t = tally ?? new PlanetDiscoveryCounts(0, 0, 0);
        int? total = PlanetDetailsService.Get(S.ToSystemAddress(planetAddress), (int)((planetAddress >> 52) & 0xF))?.FaunaTotal;
        string fauna = total switch { 0 => "no fauna", int n => $"{t.Fauna} / {n} fauna", _ => $"{t.Fauna} fauna" };
        return $"{fauna} · {t.Flora} flora · {t.Minerals} minerals";
    }

    /// <summary>"12m 4s · 85 m walked" from a planet's stats (NmsPlanets.PlanetStats), or "".</summary>
    public static string Visit(IReadOnlyDictionary<string, double> stats)
    {
        var visit = new List<string>();
        if (stats.TryGetValue("^TIME", out double seconds) && seconds >= 1) visit.Add(Duration(seconds));
        if (stats.TryGetValue("^DIST_WALKED", out double walked) && walked >= 1) visit.Add($"{walked:N0} m walked");
        return string.Join(" · ", visit);
    }

    public static string Duration(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
             : span.TotalMinutes >= 1 ? $"{span.Minutes}m {span.Seconds}s"
             : $"{span.Seconds}s";
    }

    /// <summary>"Crimson Planet · Weather: ... · Sentinels: Infrequent · Paraffinium, Silver · Has water · notes", or "".</summary>
    public static string DetailsSummary(PlanetDetails? d, bool? water)
    {
        var parts = new List<string>();
        if (d is not null)
        {
            if (d.Descriptor != "") parts.Add(d.Descriptor);
            if (d.Weather != "") parts.Add($"Weather: {d.Weather}");
            if (d.Sentinels != "") parts.Add($"Sentinels: {d.Sentinels}");
            if (d.Flora != "") parts.Add($"Flora: {d.Flora}");
            if (d.Fauna != "") parts.Add($"Fauna: {d.Fauna}");
            if (d.Resources.Count > 0) parts.Add(string.Join(", ", d.Resources));
        }
        if (water == true) parts.Add("Has water");
        else if (water == false) parts.Add("No water");
        if (d is not null) parts.AddRange(d.Notes);
        return string.Join(" · ", parts);
    }

    /// <summary>A planet card's one line: "Infrequent sentinels · Painfully Hot Rain · Copper, Salt · 2 notes", or "".</summary>
    public static string CardSummary(PlanetDetails? d)
    {
        if (d is null) return "";
        var parts = new List<string>();
        if (d.Sentinels != "") parts.Add($"{d.Sentinels} sentinels");
        if (d.Weather != "") parts.Add(d.Weather);
        if (d.Resources.Count > 0) parts.Add(string.Join(", ", d.Resources));
        if (d.Notes.Count > 0) parts.Add(d.Notes.Count == 1 ? "1 note" : $"{d.Notes.Count} notes");
        return string.Join(" · ", parts);
    }

    /// <summary>"Yellow star (G7) · Mathematical (Scientific) · Opulent (High ★★★ wealth) · ...", or "".</summary>
    public static string NoteSummary(SystemNote? note)
    {
        if (note is null) return "";
        var parts = new List<string>();
        if (note.StarColour != "") parts.Add(note.StarClass != "" ? $"{note.StarColour} star ({note.StarClass})" : $"{note.StarColour} star");
        else if (note.StarClass != "") parts.Add($"Class {note.StarClass}");
        if (note.Economy != "") parts.Add(Described("Economy", note.Economy, ""));
        if (note.Wealth != "") parts.Add(Described("Wealth", note.Wealth, "wealth"));
        if (note.Conflict != "") parts.Add(Described("Conflict", note.Conflict, "conflict"));
        if (note.Race != "") parts.Add(note.Race);
        if (note.Water) parts.Add("Water");
        if (note.Dissonant) parts.Add("Dissonant");
        if (note.Moons is int moons) parts.Add(moons == 1 ? "1 moon" : $"{moons} moons");
        if (note.Ships.Count > 0) parts.Add($"Ships: {string.Join(", ", note.Ships)}");
        parts.AddRange(note.Notes);
        return string.Join(" · ", parts);
    }

    /// <summary>"Opulent (High ★★★ wealth)" - the game's word plus its category.</summary>
    private static string Described(string group, string word, string suffix)
    {
        string category = SystemNotesService.CategoryOf(group, word);
        string label = suffix.Length > 0 ? $"{category} {suffix}" : category;
        return category == word ? word : $"{word} ({label})";
    }

    /// <summary>Levels in the order the editor lists them.</summary>
    public static readonly string[] LevelOrder = { "None", "Low", "Medium", "High", "Corrupted", "Infested", "Synthetic", "Weird" };

    /// <summary>The game's tier names (from the loc ids, e.g. SENTINEL_RARE3, RARITY_HIGH6) as
    /// levels: sentinels RARE and LOW are both low, DEFAULT is the usual medium, HIGH and
    /// AGGRESSIVE share their words (both high).</summary>
    public static string LevelName(string tier) => tier.ToUpperInvariant() switch
    {
        "NONE" => "None",
        "LOW" or "RARE" => "Low",
        "MID" or "DEFAULT" => "Medium",
        "HIGH" or "AGGRESSIVE" => "High",
        "CORRUPT" => "Corrupted",
        "INFESTED" => "Infested",
        "ROBOT" => "Synthetic",
        "WEIRD" => "Weird",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier.ToLowerInvariant()),
    };

    /// <summary>The level a recorded word belongs to ("Frenzied" -> "High"; sentinels' top
    /// level is "Aggressive", as the game says it), or the word itself.</summary>
    public static string LevelOf(IReadOnlyList<(string Word, string Group, string Tier)> words, string group, string word)
    {
        if (word.Length == 0) return "";
        var match = words.FirstOrDefault(w => w.Group == group && string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase));
        if (match.Word is null) return word;
        string level = LevelName(match.Tier);
        return group == "Sentinels" && level == "High" ? "Aggressive" : level;
    }

    public static string GalaxyLabel(int galaxy) =>
        NmsGalaxyNames.ForNumber(galaxy) is string name ? $"{name} ({galaxy})" : $"Galaxy {galaxy}";

    public static string Date(long unixSeconds) =>
        unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("d", CultureInfo.CurrentCulture) : "unknown date";

    /// <summary>"planet 2 of Irskya" / "moon of Irskya".</summary>
    public static string Body(KnownPlanet planet) =>
        $"{(BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex) ? "moon" : $"planet {planet.PlanetIndex}")} of {UniverseInfo.SystemName(planet.SystemAddress)}";

    /// <summary>A system's star colour and race: the player's note when they took one, else the generator.</summary>
    public static string StarOf(ulong system) =>
        SystemNotesService.Get(system)?.StarColour is { Length: > 0 } s ? s : UniverseInfo.For(system).StarLabel;

    public static string RaceOf(ulong system) =>
        SystemNotesService.Get(system)?.Race is { Length: > 0 } r ? r : UniverseInfo.For(system).RaceLabel;

    /// <summary>"About 2,300 ly from you (3 jumps) · 410,000 ly from the galaxy core".</summary>
    public static string Distance(KnownPlanet planet, ulong? here, double? lightYears)
    {
        string core = $"{NmsPlanets.DistanceToCoreLightYears(planet.Address):N0} ly from the galaxy core";
        if (here is ulong h && planet.SystemAddress == h) return $"In the system you're in · {core}";
        string away = lightYears switch
        {
            null => "In a different galaxy from you",
            < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
            double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
        };
        return $"{away} · {core}";
    }

    /// <summary>Everything the finder's search box looks through for a planet: names, type,
    /// what's recorded about it and its system, and its creatures ("Underwater", "Nocturnal").</summary>
    public static string SearchText(KnownPlanet p) => string.Join(" ",
        UniverseInfo.PlanetName(p.SystemAddress, p.PlanetIndex), UniverseInfo.SystemName(p.SystemAddress), UniverseInfo.Region(p.SystemAddress), p.TypeName, p.BiomeGroup,
        DetailsSummary(PlanetDetailsService.Get(p.SystemAddress, p.PlanetIndex), PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex)),
        NoteSummary(SystemNotesService.Get(p.SystemAddress)),
        string.Join(" ", CreatureListService.On(p.SystemAddress, p.PlanetIndex).Select(c => $"{c.Name} {c.Rarity} {c.Habitat} {c.Activity}")));

    /// <summary>"Space station" / "Trading post · Irskya XIX" for a market place key.</summary>
    public static string PlaceName(ulong system, string place) => place == "station" ? "Space station"
        : int.TryParse(place.Replace("planet:", ""), out int p) ? $"Trading post · {UniverseInfo.PlanetName(system, p)}" : place;

    /// <summary>"just now" / "12 min ago" / "3 h ago" / a date.</summary>
    public static string SeenAgo(string iso)
    {
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)) return "";
        var ago = DateTime.UtcNow - at.ToUniversalTime();
        return ago.TotalMinutes < 1 ? "just now" : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes} min ago"
            : ago.TotalDays < 1 ? $"{(int)ago.TotalHours} h ago" : at.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
    }

    /// <summary>A fish's name in normal case ("Glass Angel", not "GLASS ANGEL").</summary>
    public static string FishName(string id, IReadOnlyDictionary<string, string> itemNames) => ItemName(id, itemNames);

    /// <summary>An item's name in normal case ("Magnetized Ferrite", not "MAGNETIZED FERRITE").</summary>
    public static string ItemName(string id, IReadOnlyDictionary<string, string> itemNames)
    {
        string name = itemNames.TryGetValue(id, out var n) ? n
            : CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } shown ? shown : id;
        return name == name.ToUpperInvariant() ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()) : name;
    }

    // ---------------------------------------------------------------- Generated profiles

    /// <summary>Weather types in GcWeatherOptions order, worded plainly (the game picks one
    /// of several flowery words per type - "Frost", "Icy Blasts" - in its text step).</summary>
    private static readonly string[] WeatherTypes =
    {
        "Clear", "Dusty", "Humid", "Snowy", "Toxic", "Scorching", "Radioactive", "Exotic", "Exotic", "Exotic",
        "Swampy", "Volcanic", "Bubbling", "Anomalous", "Burning", "Cold", "Gas giant",
    };

    /// <summary>"Extreme toxic · frequent storms" for a generated profile.</summary>
    public static string Weather(Core.Universe.PlanetProfile profile)
    {
        string type = profile.WeatherType >= 0 && profile.WeatherType < WeatherTypes.Length ? WeatherTypes[profile.WeatherType] : "Unknown";
        string weather = profile.Extreme ? $"Extreme {type.ToLowerInvariant()}" : type;
        return profile.Storms switch
        {
            1 => $"{weather} · occasional storms",
            2 => $"{weather} · frequent storms",
            _ => weather,
        };
    }

    /// <summary>The sentinel level at normal difficulty, in the levels the editor uses
    /// (sentinels' top level is "Aggressive"; level 3 is the dissonant, corrupted kind).</summary>
    public static string Sentinels(Core.Universe.PlanetProfile profile) =>
        (profile.Sentinels.Length > NormalDifficulty ? profile.Sentinels[NormalDifficulty] : 1) switch
        {
            0 => "Low",
            2 => "Aggressive",
            3 => "Corrupted",
            _ => "Medium",
        };

    /// <summary>GcCombatTimerDifficultyOption Normal - the slot the game uses unless the save's
    /// sentinel difficulty is changed.</summary>
    private const int NormalDifficulty = 2;

    /// <summary>The three mined resources, most common first ("Copper, Ammonia, Cobalt").</summary>
    public static IReadOnlyList<string> Resources(Core.Universe.PlanetProfile profile, IReadOnlyDictionary<string, string> itemNames) =>
        new[] { profile.Common, profile.Uncommon, profile.Rare }.Where(id => id.Length > 0)
            .Select(id => ItemName(id, itemNames)).Distinct().ToList();
}

/// <summary>"Coblat isn't one of the game's resources - did you mean Cobalt?": the closest of
/// the game's words to one typed, so typos (kept as typed) don't miss every search.</summary>
public static class WordSuggestion
{
    /// <summary>The closest word, or null when it matches one or nothing is close
    /// (up to 1 edit per 4 letters - 2 for "Coblat" - so real one-offs aren't flagged).</summary>
    public static string? For(string typed, IReadOnlyList<string> words)
    {
        typed = typed.Trim();
        if (typed.Length < 3 || words.Count == 0) return null;
        if (words.Any(w => string.Equals(w, typed, StringComparison.OrdinalIgnoreCase))) return null;
        string a = typed.ToLowerInvariant();
        var best = words.Select(w => (Word: w, Distance: Distance(a, w.ToLowerInvariant())))
            .OrderBy(x => x.Distance).ThenBy(x => Math.Abs(x.Word.Length - typed.Length)).First();
        return best.Distance <= Math.Max(1, typed.Length / 4) ? best.Word : null;
    }

    /// <summary>Edit distance where swapping two neighbouring letters counts as one.</summary>
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}
