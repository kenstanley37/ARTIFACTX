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

    [JsonIgnore]
    public bool IsEmpty => StarColour == "" && Economy == "" && Wealth == "" && Conflict == "" && Race == "" && Text == "";
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
    public static readonly string[] Races = { "Gek", "Korvax", "Vy'keen", "Autophage", "None (uncharted)" };

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
            return _notes ??= new Dictionary<string, SystemNote>();
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

[JsonSerializable(typeof(Dictionary<string, SystemNote>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SystemNotesJsonContext : JsonSerializerContext { }
