using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>Ship types seen in this system (at its station or flying
    /// by), from <see cref="SystemNotesService.ShipTypes"/>.</summary>
    public List<string> Ships { get; set; } = new();

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
        && SystemName == "" && StationName == "" && PlanetNames.Count == 0 && Ships.Count == 0;

    /// <summary>Whether a body is a moon. The save doesn't mark moons, but
    /// checked against 4 real systems (2026-10-11) moons are always the bodies
    /// right after planet 1: one moon = planet 2, two moons = planets 2 and 3.
    /// So with the player's recorded moon count, planets 2..(1 + Moons) are moons.</summary>
    /// <remarks>Superseded by <see cref="BodyKindsService.IsMoon"/> (generator + what the
    /// game showed); kept as its last fallback.</remarks>
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

    /// <summary>The game's ship classes (GcSpaceshipClasses: Fighter, Dropship,
    /// Scientific, Shuttle, Royal, Sail, Alien, Robot, Corvette) by the names
    /// players use for them.</summary>
    public static readonly string[] ShipTypes = { "Fighter", "Hauler", "Explorer", "Shuttle", "Exotic", "Solar", "Living", "Sentinel", "Corvette" };

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

    // Storage: user.db (UserDatabase) - tables systems, planet_names and
    // system_notes. Everything is also held in memory, since the finder looks
    // notes up for thousands of planets per refresh; writes go through to the DB.
    private static readonly string LegacyJsonPath = Path.Combine(LocalAppDataPaths.RootFolder, "system-notes.json");
    private static readonly object Lock = new();
    private static Dictionary<string, SystemNote>? _notes;

    private static Dictionary<string, SystemNote> Notes
    {
        get
        {
            lock (Lock)
            {
                if (_notes is not null) return _notes;
                ImportLegacyJsonOnce();
                _notes = LoadAll();
                return _notes;
            }
        }
    }

    private static string Key(ulong systemAddress) => systemAddress.ToString("X", CultureInfo.InvariantCulture);

    public static SystemNote? Get(ulong systemAddress) =>
        Notes.TryGetValue(Key(systemAddress), out var note) ? note : null;

    public static int Count => Notes.Count;

    /// <summary>Raised (on any thread) when notes, names, or flags for a system are added, modified, or reloaded.</summary>
    public static event Action<ulong>? Changed;

    /// <summary>Drops the in-memory copy so the next read comes from user.db (after an import).</summary>
    public static void Reload()
    {
        lock (Lock) _notes = null;
        Changed?.Invoke(0);
    }

    /// <summary>Every recorded system's notes (for building search filters).</summary>
    public static IReadOnlyList<SystemNote> All
    {
        get { lock (Lock) return Notes.Values.ToList(); }
    }

    /// <param name="source">"user" when typed on a page, "save" when learned from a save.</param>
    public static void Save(ulong systemAddress, SystemNote note, string source = "user")
    {
        string key = Key(systemAddress);
        lock (Lock)
        {
            if (note.IsEmpty) Notes.Remove(key);
            else Notes[key] = note;
        }

        try
        {
            using var connection = UserDatabase.Open();
            using var tx = connection.BeginTransaction();
            Write(connection, tx, key, NmsGalaxy(systemAddress), note, source);
            tx.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemNotesService] Couldn't save notes: {ex.Message}");
        }
        Changed?.Invoke(systemAddress);
    }

    private static int NmsGalaxy(ulong address) => (int)((address >> 32) & 0xFF) + 1;

    // ---------------------------------------------------------------- database

    private static Dictionary<string, SystemNote> LoadAll()
    {
        var result = new Dictionary<string, SystemNote>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var connection = UserDatabase.Open();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT address, name, station, star_class, star_colour, economy, wealth, conflict,
                                           race, moons, water, dissonant, ships
                                    FROM systems WHERE deleted = 0";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    result[r.GetString(0)] = new SystemNote
                    {
                        SystemName = r.GetString(1), StationName = r.GetString(2), StarClass = r.GetString(3),
                        StarColour = r.GetString(4), Economy = r.GetString(5), Wealth = r.GetString(6),
                        Conflict = r.GetString(7), Race = r.GetString(8),
                        Moons = r.IsDBNull(9) ? null : r.GetInt32(9),
                        Water = r.GetInt64(10) != 0, Dissonant = r.GetInt64(11) != 0,
                        Ships = r.GetString(12).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    };
                }
            }

            SystemNote For(string address) => result.TryGetValue(address, out var n) ? n : result[address] = new SystemNote();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT system_address, planet_index, name FROM planet_names WHERE deleted = 0";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    For(r.GetString(0)).PlanetNames[r.GetInt32(1).ToString(CultureInfo.InvariantCulture)] = r.GetString(2);
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT system_address, text FROM system_notes WHERE deleted = 0 ORDER BY created_at, rowid";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    For(r.GetString(0)).Notes.Add(r.GetString(1));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemNotesService] Couldn't read user.db: {ex.Message}");
        }
        return result;
    }

    private static readonly string[] SystemFields =
        { "name", "station", "star_class", "star_colour", "economy", "wealth", "conflict", "race", "moons", "water", "dissonant", "ships" };

    /// <summary>Upserts one system's row, planet names and notes list. Rows are
    /// never deleted: removed planet names / notes and emptied systems are
    /// marked deleted = 1 (see UserDatabase's sync columns).</summary>
    private static void Write(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction tx,
        string key, int galaxy, SystemNote note, string source)
    {
        string now = UserDatabase.Now();
        var rowArgs = new Dictionary<string, object> { ["$a"] = key };
        var before = FieldSources.ReadRow(c, tx, "systems", "address = $a", rowArgs, SystemFields);

        using (var cmd = c.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO systems (address, galaxy, name, station, star_class, star_colour, economy, wealth, conflict,
                                     race, moons, water, dissonant, ships, updated_at, source, synced_at, deleted, added_by)
                VALUES ($a, $g, $name, $station, $class, $colour, $economy, $wealth, $conflict,
                        $race, $moons, $water, $dissonant, $ships, $now, $source, NULL, $deleted, $added_by)
                ON CONFLICT(address) DO UPDATE SET
                    galaxy = $g, name = $name, station = $station, star_class = $class, star_colour = $colour,
                    economy = $economy, wealth = $wealth, conflict = $conflict, race = $race, moons = $moons,
                    water = $water, dissonant = $dissonant, ships = $ships, updated_at = $now, source = $source,
                    synced_at = NULL, deleted = $deleted";
            cmd.Parameters.AddWithValue("$a", key);
            cmd.Parameters.AddWithValue("$g", galaxy);
            cmd.Parameters.AddWithValue("$name", note.SystemName);
            cmd.Parameters.AddWithValue("$station", note.StationName);
            cmd.Parameters.AddWithValue("$class", note.StarClass);
            cmd.Parameters.AddWithValue("$colour", note.StarColour);
            cmd.Parameters.AddWithValue("$economy", note.Economy);
            cmd.Parameters.AddWithValue("$wealth", note.Wealth);
            cmd.Parameters.AddWithValue("$conflict", note.Conflict);
            cmd.Parameters.AddWithValue("$race", note.Race);
            cmd.Parameters.AddWithValue("$moons", note.Moons is int m ? m : DBNull.Value);
            cmd.Parameters.AddWithValue("$water", note.Water ? 1 : 0);
            cmd.Parameters.AddWithValue("$dissonant", note.Dissonant ? 1 : 0);
            cmd.Parameters.AddWithValue("$ships", string.Join(", ", note.Ships));
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
            cmd.Parameters.AddWithValue("$source", source);
            cmd.Parameters.AddWithValue("$deleted", note.IsEmpty ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
        FieldSources.Record(c, tx, "system", key, before, FieldSources.ReadRow(c, tx, "systems", "address = $a", rowArgs, SystemFields), source);

        // Planet names: upsert the current ones, soft-delete any that are gone.
        var existingNames = new Dictionary<int, string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT planet_index, name FROM planet_names WHERE system_address = $a AND deleted = 0";
            cmd.Parameters.AddWithValue("$a", key);
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingNames[r.GetInt32(0)] = r.GetString(1);
        }
        foreach (var (indexText, name) in note.PlanetNames)
        {
            if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)) continue;
            if (existingNames.TryGetValue(index, out var old) && old == name) continue;
            FieldSources.Record(c, tx, "planet", $"{key}|{index}", new Dictionary<string, string> { ["name"] = old ?? "" },
                new Dictionary<string, string> { ["name"] = name }, source);
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO planet_names (system_address, planet_index, name, updated_at, source, synced_at, deleted, added_by)
                VALUES ($a, $i, $name, $now, $source, NULL, 0, $added_by)
                ON CONFLICT(system_address, planet_index) DO UPDATE SET
                    name = $name, updated_at = $now, source = $source, synced_at = NULL, deleted = 0";
            cmd.Parameters.AddWithValue("$a", key);
            cmd.Parameters.AddWithValue("$i", index);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
            cmd.Parameters.AddWithValue("$source", source);
            cmd.ExecuteNonQuery();
        }
        foreach (int gone in existingNames.Keys.Where(i => !note.PlanetNames.ContainsKey(i.ToString(CultureInfo.InvariantCulture))))
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE planet_names SET deleted = 1, updated_at = $now, synced_at = NULL WHERE system_address = $a AND planet_index = $i";
            cmd.Parameters.AddWithValue("$a", key);
            cmd.Parameters.AddWithValue("$i", gone);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }

        // Notes list: keep rows whose text is still present (matched in order,
        // duplicates allowed), soft-delete the rest, insert the new ones.
        var existingNotes = new List<(string Id, string Text)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id, text FROM system_notes WHERE system_address = $a AND deleted = 0 ORDER BY created_at, rowid";
            cmd.Parameters.AddWithValue("$a", key);
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingNotes.Add((r.GetString(0), r.GetString(1)));
        }
        var unmatched = existingNotes.ToList();
        var toInsert = new List<string>();
        foreach (var text in note.Notes)
        {
            int match = unmatched.FindIndex(n => n.Text == text);
            if (match >= 0) unmatched.RemoveAt(match);
            else toInsert.Add(text);
        }
        foreach (var (id, _) in unmatched)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE system_notes SET deleted = 1, updated_at = $now, synced_at = NULL WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }
        foreach (var text in toInsert)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO system_notes (id, system_address, text, created_at, updated_at, source, synced_at, deleted, added_by)
                                VALUES ($id, $a, $text, $now, $now, $source, NULL, 0, $added_by)";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("$a", key);
            cmd.Parameters.AddWithValue("$text", text);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
            cmd.Parameters.AddWithValue("$source", source);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Moves notes from the original system-notes.json (before user.db
    /// existed) into the database, once. The JSON file is then renamed to
    /// system-notes.json.imported - kept, never deleted, in case it's needed.</summary>
    private static void ImportLegacyJsonOnce()
    {
        if (!File.Exists(LegacyJsonPath)) return;
        try
        {
            var legacy = JsonSerializer.Deserialize(File.ReadAllText(LegacyJsonPath), SystemNotesJsonContext.Default.DictionaryStringSystemNote)
                ?? new Dictionary<string, SystemNote>();

            using (var connection = UserDatabase.Open())
            using (var tx = connection.BeginTransaction())
            {
                foreach (var (key, note) in legacy)
                {
                    if (note.Text.Length > 0 && note.Notes.Count == 0) note.Notes.Add(note.Text);
                    note.Text = "";
                    if (note.IsEmpty || !ulong.TryParse(key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong address)) continue;
                    Write(connection, tx, key.ToUpperInvariant(), NmsGalaxy(address), note, "user");
                }
                tx.Commit();
            }

            File.Move(LegacyJsonPath, LegacyJsonPath + ".imported", overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemNotesService] Couldn't import system-notes.json: {ex.Message}");
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

        if (changed) SystemNotesService.Save(address, note, source: "save");
        return changed;
    }

    /// <summary>Names players gave planets and systems, from the save's discovery records
    /// (NmsPlanets.ReadCustomNames) - a renamed planet is then known before it's ever landed
    /// on ("Waterworld", renamed by the player under an earlier name, 2026-10-18). Fills only
    /// names not known yet: a name learned from where the player stands is newer.</summary>
    public static int LearnDiscoveryNames(Newtonsoft.Json.Linq.JArray? discoveries)
    {
        int learned = 0;
        foreach (var group in ArtifactX.Core.NmsModels.NmsPlanets.ReadCustomNames(discoveries).GroupBy(n => n.System))
        {
            var note = SystemNotesService.Get(group.Key) ?? new SystemNote();
            bool changed = false;
            foreach (var (_, planet, name) in group)
            {
                if (planet == 0)
                {
                    if (note.SystemName.Length == 0) { note.SystemName = name; changed = true; }
                    continue;
                }
                string key = planet.ToString(CultureInfo.InvariantCulture);
                if (!note.PlanetNames.ContainsKey(key)) { note.PlanetNames[key] = name; changed = true; }
            }
            if (changed) { SystemNotesService.Save(group.Key, note, source: "save"); learned++; }
        }
        return learned;
    }
}

[JsonSerializable(typeof(Dictionary<string, SystemNote>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SystemNotesJsonContext : JsonSerializerContext { }
