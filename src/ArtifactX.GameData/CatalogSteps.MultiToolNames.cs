using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// The words multi-tool names are made of (2026-10-16): the game names a
    /// tool the player never renamed from its seed (ArtifactX.Core
    /// MultiToolNames), picking a pattern ("%NOUN% of %PROCNAME%-%NUMERAL%")
    /// and words from these lists - NAMEGEN_WEAP_FORMAT/NOUN/PROPERTY/ADJ/
    /// COMP1/COMP2 and the staff's NAMEGEN_STAFF_FORMAT/ADJ. Starships too
    /// (ArtifactX.Core ShipNames, format 12): NAMEGEN_SHIP_FORMAT/NOUN/VERB/
    /// POSESSION (the game's spelling)/PROPERTY/ADJ. One row per list,
    /// its words in the game's order (key _1 first), read from the player's
    /// own language files.
    /// </summary>
    public static void AddMultiToolNameWords(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}");
            return;
        }

        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            try { index.Add(pak.FullPath, reader.ReadHeader(pak.FullPath), reader.Read(pak.FullPath)); }
            catch { /* unreadable PAK - skip */ }
        }
        var extraction = new ExtractionService();

        // The lists are spread over several language files (update files included).
        var key = new Regex(@"^NAMEGEN_(WEAP|STAFF|SHIP)_([A-Z0-9]+)_(\d+)$", RegexOptions.IgnoreCase);
        var lists = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in index.Paths.Where(p => p.StartsWith("LANGUAGE/", StringComparison.OrdinalIgnoreCase)
                     && p.EndsWith("_ENGLISH.MBIN", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (!index.TryFind(path, out var pakPath, out var entry, out var header)) continue;
            try
            {
                if (extraction.ExtractEntryBytes(pakPath, entry, header) is not { } bytes) continue;
                var mbin = new MBINFile(new MemoryStream(bytes));
                mbin.Load();
                foreach (var (id, text) in LocalisationService.BuildEnglishLookup(mbin.GetData()))
                {
                    var m = key.Match(id);
                    if (!m.Success) continue;
                    string list = $"{m.Groups[1].Value}_{m.Groups[2].Value}".ToUpperInvariant();
                    if (!lists.TryGetValue(list, out var words)) lists[list] = words = new SortedDictionary<int, string>();
                    words.TryAdd(int.Parse(m.Groups[3].Value), text);
                }
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read {path}: {ex.Message}"); }
        }

        var rows = new List<CatalogItem>();
        foreach (var (list, words) in lists)
        {
            // The game counts from 1 with no gaps; a gap means a list this code doesn't understand.
            if (words.Keys.First() != 1 || words.Keys.Last() != words.Count)
            {
                ConsoleStyle.Warning($"NAMEGEN_{list}: numbering has gaps - skipped.");
                continue;
            }
            rows.Add(new CatalogItem
            {
                GameId = "NAMEGEN:" + list,
                NameEnglish = list,
                DescriptionEnglish = new JsonArray(words.Values.Select(w => (JsonNode)w).ToArray()).ToJsonString(),
            });
        }
        ConsoleStyle.Header($"Found {rows.Count} multi-tool and starship name word lists.");

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "MultiToolNameWords", "LocalisedText", "language/*_english.mbin", rows);
    }
}
