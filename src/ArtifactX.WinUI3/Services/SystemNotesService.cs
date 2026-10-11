using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtifactX.WinUI3.Services;

/// <summary>What the player recorded about one system. Empty string = not recorded.</summary>
public sealed class SystemNote
{
    public string StarColour { get; set; } = "";
    public string Economy { get; set; } = "";
    public string Wealth { get; set; } = "";
    public string Conflict { get; set; } = "";
    public string Race { get; set; } = "";
    public string Text { get; set; } = "";
    public string StarClass { get; set; } = "";
    public bool Water { get; set; }
    public bool Dissonant { get; set; }

    /// <summary>Moon count as the game's Discoveries screen shows it ("4 PLANETS / 2 MOONS"); null = not recorded.</summary>
    public int? Moons { get; set; }

    /// <summary>Free-form notes, one per line item. (Older notes kept a single
    /// <see cref="Text"/>; it's moved in here when notes are loaded.)</summary>
    public List<string> Notes { get; set; } = new();

    /// <summary>Names the game showed - learned from the save's "where you
    /// saved" line as you play (PlanetsPage), or typed by the player. Planet
    /// names are keyed by planet number ("1".."6").</summary>
    public string SystemName { get; set; } = "";
    public string StationName { get; set; } = "";
    public Dictionary<string, string> PlanetNames { get; set; } = new();

    [JsonIgnore]
    public bool IsEmpty => StarColour == "" && Economy == "" && Wealth == "" && Conflict == "" && Race == ""
        && Text == "" && StarClass == "" && !Water && !Dissonant && Moons is null && Notes.Count == 0
        && SystemName == "" && StationName == "" && PlanetNames.Count == 0;

    /// <summary>Whether a body is a moon. The save doesn't mark moons, but
    /// checked against 4 real systems (2026-10-11) moons are always the bodies
    /// right after planet 1: one moon = planet 2, two moons = planets 2 and 3.
    /// So with the player's recorded moon count, planets 2..(1 + Moons) are moons.</summary>
    public bool IsMoon(int planetIndex) => Moons is int moons && planetIndex >= 2 && planetIndex <= 1 + moons;
}

/// <summary>
/// Player-entered notes per star system, kept in ArtifactX's own app-data
/// folder (system-notes.json) - NEVER written to a save. They exist because
/// star colour, economy, conflict and race aren't stored in the save at all
/// (the game regenerates them from the system seed each visit; see
/// NmsPlanets), so the only way to search by them is for the player to record
/// what the game shows. Keyed by the system's packed address as hex, which
/// includes the galaxy, so notes work across every save slot.
/// </summary>
public static class SystemNotesService
{
    // Star colour and race are picked from these short lists (what the Galaxy
    // Map and system info show). Economy, wealth and conflict use the game's own
    // words instead (catalog "SystemDescriptors"); a note stores the word the
    // player picked, and CategoryOf maps it back to its category for filtering.
    public static readonly string[] StarColours = { "Yellow", "Red", "Green", "Blue", "Purple" };
    public static readonly string[] Races = { "Gek", "Korvax", "Vy'keen", "Autophage", "Evacuated (no station)", "None (uncharted)" };

    /// <summary>Star colour from a spectral class as the Galaxy Map shows it
    /// (e.g. "G7", "F5p", "K8pf", "E7", "X1"): the first letter decides it.
    /// Confirmed on real systems for G/F (yellow), K (red), E (green) and X
    /// (purple); O/B blue, M red and Y purple follow the same convention.</summary>
    public static string? ColourForClass(string? starClass) =>
        string.IsNullOrWhiteSpace(starClass) ? null : char.ToUpperInvariant(starClass.Trim()[0]) switch
        {
            'O' or 'B' => "Blue",
            'F' or 'G' => "Yellow",
            'K' or 'M' => "Red",
            'E' => "Green",
            'X' or 'Y' => "Purple",
            _ => null,
        };

    /// <summary>Economy values with no in-game word list of their own.</summary>
    public static readonly string[] EconomyExtras = { "Pirate controlled", "Abandoned (no economy)" };

    /// <summary>Category names for the descriptor tiers in the loc ids
    /// (UI_ECON_CLASS_SCIENCE_n -> "Scientific"); display labels only.</summary>
    public static string TierName(string group, string tier) => (group, tier) switch
    {
        ("Economy", "TRADE") => "Trading",
        ("Economy", "MINING") => "Mining",
        ("Economy", "MANUFACT") => "Manufacturing",
        ("Economy", "TECH") => "Technology",
        ("Economy", "SCIENCE") => "Scientific",
        ("Economy", "POWER") => "Power Generation",
        ("Economy", "ALLOY") => "Advanced Materials",
        ("Wealth", "LOW") => "Low ★",
        ("Wealth", "MED") => "Medium ★★",
        ("Wealth", "HIGH") => "High ★★★",
        (_, "LOW") => "Low",
        (_, "MED") => "Medium",
        (_, "HIGH") => "High",
        _ => tier,
    };

    private static Dictionary<(string Group, string Word), string> _categories = new();

    /// <summary>Called once the descriptor words are loaded from the catalog.</summary>
    public static void SetDescriptors(IEnumerable<(string Word, string Group, string Tier)> descriptors)
    {
        _categories = new Dictionary<(string, string), string>();
        foreach (var d in descriptors)
            _categories[(d.Group, d.Word)] = TierName(d.Group, d.Tier);
    }

    /// <summary>Category of a stored word ("Mathematical" -> "Scientific"), or
    /// the value itself when it has none (extras, older notes).</summary>
    public static string CategoryOf(string group, string word) =>
        _categories.TryGetValue((group, word), out var category) ? category : word;

    private static readonly string FilePath = Path.Combine(LocalAppDataPaths.RootFolder, "system-notes.json");
    private static Dictionary<string, SystemNote>? _notes;

    private static Dictionary<string, SystemNote> Notes
    {
        get
        {
            if (_notes is not null) return _notes;
            try
            {
                if (File.Exists(FilePath))
                    _notes = JsonSerializer.Deserialize(File.ReadAllText(FilePath), SystemNotesJsonContext.Default.DictionaryStringSystemNote);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemNotesService] Couldn't read notes: {ex.Message}");
            }
            _notes ??= new Dictionary<string, SystemNote>();
            foreach (var note in _notes.Values)
            {
                if (note.Text.Length > 0 && note.Notes.Count == 0) note.Notes.Add(note.Text);
                note.Text = "";
            }
            return _notes;
        }
    }

    private static string Key(ulong systemAddress) => systemAddress.ToString("X", CultureInfo.InvariantCulture);

    public static SystemNote? Get(ulong systemAddress) =>
        Notes.TryGetValue(Key(systemAddress), out var note) ? note : null;

    public static int Count => Notes.Count;

    public static void Save(ulong systemAddress, SystemNote note)
    {
        if (note.IsEmpty) Notes.Remove(Key(systemAddress));
        else Notes[Key(systemAddress)] = note;

        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Notes, SystemNotesJsonContext.Default.DictionaryStringSystemNote));
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemNotesService] Couldn't save notes: {ex.Message}");
        }
    }
}

/// <summary>
/// Records names from the save's "where you saved" line (n:R) into the system
/// notes: "In the X system" (system name - only written when the game saves
/// while you're in your ship in space), "On Planet (X)" (the planet you're on)
/// or "Aboard X" (the system's space station; "Aboard the Space Anomaly" etc.
/// are skipped). Called by every live-following Tools page, so names are
/// learned whichever of them is open.
/// </summary>
public static class SystemNameLearner
{
    private static readonly System.Text.RegularExpressions.Regex InSystemRegex = new(@"^In the (.+) system$");
    private static readonly System.Text.RegularExpressions.Regex OnPlanetRegex = new(@"^On Planet \((.+)\)$");

    /// <summary>Returns whether anything new was stored.</summary>
    public static bool Learn(ulong? system, int planet, string? summary)
    {
        if (system is not ulong address) return false;
        string line = (summary ?? "").Trim();
        var note = SystemNotesService.Get(address) ?? new SystemNote();
        bool changed = false;

        if (InSystemRegex.Match(line) is { Success: true } inSystem && note.SystemName != inSystem.Groups[1].Value)
        {
            note.SystemName = inSystem.Groups[1].Value;
            changed = true;
        }
        else if (OnPlanetRegex.Match(line) is { Success: true } onPlanet && planet > 0)
        {
            string key = planet.ToString(CultureInfo.InvariantCulture);
            if (!note.PlanetNames.TryGetValue(key, out var known) || known != onPlanet.Groups[1].Value)
            {
                note.PlanetNames[key] = onPlanet.Groups[1].Value;
                changed = true;
            }
        }
        else if (planet == 0 && line.StartsWith("Aboard ", StringComparison.Ordinal))
        {
            string station = line["Aboard ".Length..].Trim();
            if (station.Length > 0 && !station.StartsWith("the ", StringComparison.OrdinalIgnoreCase) && note.StationName != station)
            {
                note.StationName = station;
                changed = true;
            }
        }

        if (changed) SystemNotesService.Save(address, note);
        return changed;
    }
}

[JsonSerializable(typeof(Dictionary<string, SystemNote>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SystemNotesJsonContext : JsonSerializerContext { }
