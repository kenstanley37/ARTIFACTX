using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Media.Imaging;
using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace ArtifactX.WinUI3.Services;

public static class CatalogService
{
    private static readonly Dictionary<string, CatalogEntry?> Cache = new();
    private static string? _dbPath;
    private static bool _pathChecked;

    public static bool IsAvailable => ResolveDbPath() is not null;

    public static CatalogEntry? TryGet(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return null;
        string key = NormalizeId(gameId);
        return Cache.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>Strips the leading "^" every save-file item id carries, plus a
    /// trailing "#NNNNN" - a per-instance stat-roll seed that upgrade modules
    /// (Scanner/Mining/Hazard upgrades etc.) get at the moment they're rolled
    /// in-game, e.g. "^UP_SCAN0#68250". That number is never part of the
    /// game's static data, so the catalog only ever has a row for the base
    /// type ("UP_SCAN0") - looking up the full suffixed id can never match,
    /// which is why every procedurally-rolled upgrade was silently falling
    /// back to a raw-id label with no icon before this stripped it too.
    /// Public because AccountDataPage needs the exact same normalization for
    /// accountdata.hg's B89/B1h ids, which carry the same "^" prefix despite
    /// this project's own earlier notes claiming otherwise - confirmed by
    /// direct inspection (2026-08-02) after that wrong assumption caused
    /// every account item to read as locked.</summary>
    public static string NormalizeId(string gameId)
    {
        string trimmed = gameId.TrimStart('^');
        int hashIndex = trimmed.IndexOf('#');
        return hashIndex >= 0 ? trimmed[..hashIndex] : trimmed;
    }

    public static async Task WarmCacheAsync(IEnumerable<string?> gameIds)
    {
        // A cached entry with a real DisplayName but a null Icon means a PREVIOUS
        // call found the catalog row but its BitmapImage decode failed - almost
        // certainly a transient issue (confirmed 2026-08-06: manually re-decoded
        // several such icons' raw PNG bytes outside the app and they were 100%
        // valid, non-corrupt images), not a real "this item has no icon" case.
        // Without this, that transient failure was PERMANENT for the rest of the
        // app session - any page that warmed the same id later (even correctly,
        // on the UI thread) would see it as "already cached" and skip retrying,
        // which is exactly how a handful of Exocraft tech icons (VEHICLE_SCAN,
        // EXO_RECHARGE, etc.) ended up permanently blank: CataloguePage warms
        // EVERY Technology-category item on first visit (not filtered by
        // UsageCategory), so a decode hiccup there poisoned the shared cache
        // before ExocraftPage ever got a chance to load them itself. Genuinely
        // missing catalog rows (Cache[id] itself null, e.g. UP_EXENG4) are still
        // left alone - re-querying SQL for those every time would be pure waste.
        var toFetch = gameIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => NormalizeId(id!))
            .Distinct()
            .Where(id => !Cache.TryGetValue(id, out var existing) || existing is { Icon: null })
            .ToList();

        Debug.WriteLine($"[CatalogService] WarmCacheAsync: {toFetch.Count} new ids to look up (of the ids passed in).");

        if (toFetch.Count == 0) return;

        string? dbPath = ResolveDbPath();
        if (dbPath is null)
        {
            Debug.WriteLine("[CatalogService] No catalog file found - all ids will fall back to raw display.");
            foreach (var id in toFetch) Cache[id] = null;
            return;
        }

        Dictionary<string, (string DisplayName, byte[]? IconPng)> rows;
        try
        {
            rows = await Task.Run(() => QueryRows(dbPath, toFetch));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] Query failed against {dbPath}: {ex.Message}");
            foreach (var id in toFetch) Cache[id] = null;
            return;
        }

        Debug.WriteLine($"[CatalogService] Query returned {rows.Count} matches out of {toFetch.Count} requested ids.");

        // Packaged technology (e.g. ^UP_SHIP_TELE, ^UP_VEHICLE_BOOS, ^UP_SUIT_REFINER,
        // seen in a 2026-10-11 expedition save's cargo): the id is "UP_" + the
        // technology's own id, cut to the game's 15-character id limit. No table or
        // loc file contains these ids (grep/locidall over every PAK: 0 matches), so
        // show the technology itself - SHIP_TELEPORT, VEHICLE_BOOST, SUIT_REFINER.
        foreach (var id in toFetch.Where(i => !rows.ContainsKey(i) && i.StartsWith("UP_", StringComparison.Ordinal)))
        {
            try
            {
                string? techId = await Task.Run(() => FindPackagedTechId(dbPath, id[3..]));
                if (techId is not null && (await Task.Run(() => QueryRows(dbPath, new List<string> { techId }))).TryGetValue(techId, out var techRow))
                    rows[id] = techRow;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CatalogService] Packaged-tech lookup failed for '{id}': {ex.Message}");
            }
        }

        foreach (var id in toFetch)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                Cache[id] = null;
                continue;
            }

            BitmapImage? icon = null;
            if (row.IconPng is { Length: > 0 })
            {
                try
                {
                    icon = new BitmapImage();
                    using var stream = new InMemoryRandomAccessStream();
                    await stream.WriteAsync(row.IconPng.AsBuffer());
                    stream.Seek(0);
                    await icon.SetSourceAsync(stream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CatalogService] Icon decode failed for '{id}': {ex.Message}");
                    icon = null;
                }
            }

            Cache[id] = new CatalogEntry { DisplayName = row.DisplayName, Icon = icon };
        }
    }

    /// <summary>The technology id a packaged-tech "UP_" id was cut from: an exact
    /// match, else the shortest technology id starting with it.</summary>
    private static string? FindPackagedTechId(string dbPath, string truncated)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId FROM Items i JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'GcTechnologyTable' AND (i.GameId = $id OR i.GameId LIKE $prefix ESCAPE '\')
            ORDER BY length(i.GameId) LIMIT 1";
        command.Parameters.AddWithValue("$id", truncated);
        command.Parameters.AddWithValue("$prefix", truncated.Replace("\\", "\\\\").Replace("_", "\\_").Replace("%", "\\%") + "%");
        return command.ExecuteScalar() as string;
    }

    /// <summary>Searches the catalog for items whose display name contains the
    /// query, restricted to the given MBIN table names (e.g. "GcTechnologyTable"
    /// for the Exosuit tech grid, "GcProductTable"/"GcSubstanceTable" for cargo).
    /// allowedUsageCategories further restricts to specific equipment slots (e.g.
    /// "Suit"/"All" for the Exosuit tech grid) - pass null to skip this filter
    /// entirely, which is required for Product/Substance searches since those rows
    /// have no UsageCategory at all. Returns bare GameId + elv-style CategoryLabel
    /// only - callers should follow up with WarmCacheAsync(results...GameId) and
    /// TryGet() for display name/icon, reusing the existing cache rather than
    /// duplicating that lookup here.</summary>
    public static async Task<List<CatalogSearchResult>> SearchAsync(string query, string[] allowedTemplateTypes, string[]? allowedUsageCategories = null, int maxResults = 30)
    {
        if (string.IsNullOrWhiteSpace(query) || allowedTemplateTypes.Length == 0)
            return new();

        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QuerySearch(dbPath, query.Trim(), allowedTemplateTypes, allowedUsageCategories, maxResults));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] SearchAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<CatalogSearchResult> QuerySearch(string dbPath, string query, string[] allowedTemplateTypes, string[]? allowedUsageCategories, int maxResults)
    {
        var results = new List<CatalogSearchResult>();
        var seen = new HashSet<string>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        string typePlaceholders = string.Join(",", allowedTemplateTypes.Select((_, i) => $"@t{i}"));

        // Only join on UsageCategory when a filter was actually requested - Product/
        // Substance rows have no UsageCategory at all, so forcing this clause for
        // every search would silently exclude every cargo item.
        string usageClause = "";
        if (allowedUsageCategories is { Length: > 0 })
        {
            string usagePlaceholders = string.Join(",", allowedUsageCategories.Select((_, i) => $"@u{i}"));
            usageClause = $"AND i.UsageCategory IN ({usagePlaceholders})";
        }

        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT i.GameId, c.TemplateType, i.MaxStackSize
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE i.NameEnglish IS NOT NULL
              AND c.TemplateType IN ({typePlaceholders})
              AND i.NameEnglish LIKE @q
              {usageClause}
            LIMIT @max";

        for (int i = 0; i < allowedTemplateTypes.Length; i++)
            command.Parameters.AddWithValue($"@t{i}", allowedTemplateTypes[i]);

        if (allowedUsageCategories is { Length: > 0 })
        {
            for (int i = 0; i < allowedUsageCategories.Length; i++)
                command.Parameters.AddWithValue($"@u{i}", allowedUsageCategories[i]);
        }

        command.Parameters.AddWithValue("@q", $"%{query}%");
        command.Parameters.AddWithValue("@max", maxResults);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string gameId = reader.GetString(0);
            if (!seen.Add(gameId)) continue;

            results.Add(new CatalogSearchResult
            {
                GameId = gameId,
                CategoryLabel = ElvLabelFor(reader.GetString(1)),
                MaxStackSize = reader.IsDBNull(2) ? null : reader.GetInt32(2)
            });
        }
        return results;
    }

    // Maps the MBIN source table to the "elv" string the save file itself uses
    // per-slot (Vn8.elv: "Technology"/"Product"/"Substance") - add new tables here
    // as they're classified, rather than guessing a default for unknown ones.
    private static string ElvLabelFor(string templateType) => templateType switch
    {
        "GcTechnologyTable" => "Technology",
        "GcSubstanceTable" => "Substance",
        _ => "Product"
    };

    private static Dictionary<string, (string DisplayName, byte[]? IconPng)> QueryRows(string dbPath, List<string> gameIds)
    {
        var result = new Dictionary<string, (string, byte[]?)>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        // COALESCE to NameLowerEnglish for rows where NameEnglish never resolves -
        // true for every GcProceduralTechnologyTable row (Scanner/Mining/Hazard
        // upgrade modules etc.): their own Name field is just a short id fragment
        // ("UP_SCAN"), never a real "_NAME"-suffixed loc key, but NameLower
        // correctly resolves to the base technology's name ("Analysis Visor" for
        // scanner upgrades). GameId isn't unique across the whole catalog (see
        // docs/DATACATALOGER.md), so the WHERE clause still requires at least one
        // of the two to be non-null to pick the "real" row among duplicates.
        string placeholders = string.Join(",", gameIds.Select((_, i) => $"@p{i}"));
        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT i.GameId, COALESCE(i.NameEnglish, i.NameLowerEnglish) AS DisplayName, b.PngData
            FROM Items i
            LEFT JOIN Icons ic ON ic.ItemId = i.Id
            LEFT JOIN IconBlobs b ON b.Id = ic.IconBlobId
            WHERE (i.NameEnglish IS NOT NULL OR i.NameLowerEnglish IS NOT NULL) AND i.GameId IN ({placeholders})";

        for (int i = 0; i < gameIds.Count; i++)
            command.Parameters.AddWithValue($"@p{i}", gameIds[i]);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string gameId = reader.GetString(0);
            if (result.ContainsKey(gameId)) continue;

            string name = reader.GetString(1);
            byte[]? png = reader.IsDBNull(2) ? null : (byte[])reader["PngData"];
            result[gameId] = (name, png);
        }
        return result;
    }

    /// <summary>Multi-tool "Type" (base model) options, sourced from the
    /// MultiToolTypes category CatalogBuildService discovers by filename rule
    /// (see that service for how) rather than a curated list anywhere in the
    /// app. GameId is the model scene path itself; NameEnglish is the derived
    /// display name. Rebuilding the catalog after a game update refreshes this
    /// automatically - nothing in the WinUI3 project needs to change.</summary>
    public static async Task<List<(string DisplayName, string ScenePath)>> GetMultiToolTypesAsync() =>
        await GetScenePathOptionsAsync("MultiToolTypes");

    /// <summary>Freighter "Type" (hull model) options, sourced from the
    /// FreighterTypes category - same filename-rule discovery as Multi-Tool
    /// Types, see CatalogBuildService for how. Confirmed real names for the
    /// three model paths actually seen in player saves: "Normal"
    /// (FREIGHTER_PROC), "Capital" (CAPITALFREIGHTER_PROC), "Dreadnought"
    /// (PIRATEFREIGHTER) - cross-checked against a reference tool.</summary>
    public static async Task<List<(string DisplayName, string ScenePath)>> GetFreighterTypesAsync() =>
        await GetScenePathOptionsAsync("FreighterTypes");

    /// <summary>Freighter crew captain "Race" options (Gek/Korvax/Vy'keen/Robot),
    /// sourced from the FreighterCrewRaces category - the captain is a separate
    /// model reference (Sjw.93M) from the freighter hull itself (bIR.93M).</summary>
    public static async Task<List<(string DisplayName, string ScenePath)>> GetFreighterCrewRacesAsync() =>
        await GetScenePathOptionsAsync("FreighterCrewRaces");

    /// <summary>The parts a corvette is built from - the game's own
    /// BuildableInShipStructural flag (DataCataloger add-corvette-parts) -
    /// joined to the product row for the name and stack size, A-Z. These are
    /// what the Corvette Cache holds: 139 of the 141 different items found in
    /// real caches across every save (2026-10-12; the other two were base
    /// reactors). Decorative-only parts (decals, posters, furniture) are placed
    /// inside a corvette, not kept in the cache, so they're left out, as are
    /// parts with no inventory item of their own. And only the ones in the
    /// game's build menu (ShowInBuildMenu): most structural ids are rotations
    /// and mirrors of one part (B_STR_K_NE, _NE1-3, _Y_NE...) - 433 of 575 -
    /// and real caches held 131 of the 142 menu parts; the few non-menu ids
    /// seen there were right-hand wing pieces the game hands back.</summary>
    public static async Task<List<CatalogPart>> GetCorvettePartsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var parts = new List<CatalogPart>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT p.GameId, p.NameEnglish, cp.UsageCategory, p.MaxStackSize
                    FROM Items cp
                    JOIN Categories cc ON cc.Id = cp.CategoryId
                    JOIN Items p ON p.GameId = substr(cp.GameId, 10)
                    JOIN Categories pc ON pc.Id = p.CategoryId
                    WHERE cc.TemplateType = 'CorvetteParts'
                      AND cp.UsageCategory IN ('Structural', 'Both')
                      AND cp.DescriptionEnglish LIKE '%""menu"":true%'
                      AND pc.TemplateType = 'GcProductTable'
                      AND p.NameEnglish IS NOT NULL
                    GROUP BY p.GameId
                    ORDER BY p.NameEnglish";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    parts.Add(new CatalogPart(reader.GetString(0), reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt32(3)));
                return parts;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetCorvettePartsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Technology that fits a device, by the catalog's usage categories
    /// (a ship kind's own set - see ShipsPage.TechRuleFor). Skips the game's
    /// broken-slot placeholders, the generic "Upgrade Module" template rows and
    /// procedural upgrades (those carry a seed and come from shops, not a list).
    /// Group holds the usage category. Checked 2026-10-13 against every ship in
    /// the real saves: every installed non-procedural tech is in its kind's list.</summary>
    public static async Task<List<CatalogPart>> GetTechForAsync(IReadOnlyCollection<string> usageCategories)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null || usageCategories.Count == 0) return new();
        try
        {
            return await Task.Run(() =>
            {
                var tech = new List<CatalogPart>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                var names = usageCategories.Select((u, i) => (Name: $"@u{i}", Value: u)).ToList();
                command.CommandText = $@"
                    SELECT i.GameId, i.NameEnglish, i.UsageCategory, i.MaxStackSize, s.NameEnglish, s.UsageCategory, s.DescriptionEnglish
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    LEFT JOIN Items s ON s.GameId = 'TECHSTAT:' || i.GameId
                    WHERE c.TemplateType = 'GcTechnologyTable'
                      AND i.NameEnglish IS NOT NULL
                      AND i.UsageCategory IN ({string.Join(", ", names.Select(n => n.Name))})
                      AND COALESCE(i.IsBrokenSlotTech, 0) = 0
                      AND NOT (i.NameEnglish = 'UPGRADE MODULE' AND i.DescriptionEnglish IS NULL)
                      AND i.GameId NOT LIKE '%#%'
                    GROUP BY i.GameId
                    ORDER BY i.NameEnglish";
                foreach (var n in names) command.Parameters.AddWithValue(n.Name, n.Value);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    tech.Add(new CatalogPart(reader.GetString(0), reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        Stat: reader.IsDBNull(4) ? null : reader.GetString(4),
                        Primary: !reader.IsDBNull(5) && reader.GetString(5) == "Primary",
                        Colour: reader.IsDBNull(6) ? null : reader.GetString(6)));
                return tech;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetTechForAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>S-class upgrade modules (the ones bought at space stations) for
    /// the given module families, e.g. "UP_PULSE": per family the "supremely
    /// powerful" one (class 4 for ships, but class 3 for the Exosuit's hazard
    /// modules - UP_HOT3 etc. have no 4), else its highest class. The game allows 3 per
    /// technology, any class - every ship in the real saves tops out at exactly
    /// 3 per family (2026-10-13). Named after the tech they upgrade, read from
    /// the module's own description ("upgrade for the Pulsing Heart").</summary>
    public static async Task<List<CatalogPart>> GetUpgradeModulesAsync(IReadOnlyCollection<string> families)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null || families.Count == 0) return new();
        try
        {
            return await Task.Run(() =>
            {
                var modules = new List<CatalogPart>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                var names = families.Select((f, i) => (Name: $"@f{i}", Value: f)).ToList();
                command.CommandText = $@"
                    SELECT i.GameId, i.NameLowerEnglish, i.DescriptionEnglish, s.NameEnglish, s.DescriptionEnglish
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    LEFT JOIN Items s ON s.GameId = 'TECHSTAT:' || i.GameId
                    WHERE c.TemplateType = 'GcProceduralTechnologyTable'
                      AND substr(i.GameId, 1, length(i.GameId) - 1) IN ({string.Join(", ", names.Select(n => n.Name))})
                      AND substr(i.GameId, -1) IN ('1', '2', '3', '4')";
                foreach (var n in names) command.Parameters.AddWithValue(n.Name, n.Value);
                using var reader = command.ExecuteReader();
                var rows = new List<(string Id, string? Plain, string? Desc, string? Stat, string? Colour)>();
                while (reader.Read())
                    rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
                foreach (var best in rows.GroupBy(r => r.Id[..^1])
                             .Select(g => g.OrderByDescending(r => r.Desc?.Contains("supremely powerful") == true)
                                 .ThenByDescending(r => r.Id[^1]).First()))
                {
                    string id = best.Id;
                    string? plain = best.Plain;
                    string? desc = best.Desc;
                    var target = desc is null ? null : Regex.Match(desc, @"upgrade for the (?:vessel's )?<TECHNOLOGY>(.*?)<>");
                    string tech = target is { Success: true }
                        ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(target.Groups[1].Value)
                        : plain ?? id;
                    // "Exosuit Thermal Protection System" -> "Thermal Protection System".
                    foreach (string prefix in new[] { "Exosuit ", "Starship ", "Multi-Tool " })
                        if (tech.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) tech = tech[prefix.Length..];
                    modules.Add(new CatalogPart(id, $"{tech} upgrade (S)", "Upgrade modules", null,
                        CountKey: id[..^1], Limit: 3, Seeded: true, Stat: best.Stat, Colour: best.Colour));
                }
                return modules.OrderBy(m => m.Name).ToList();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetUpgradeModulesAsync failed: {ex.Message}");
            return new();
        }
    }

    private static async Task<List<(string DisplayName, string ScenePath)>> GetScenePathOptionsAsync(string templateType)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryScenePathOptions(dbPath, templateType));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetScenePathOptionsAsync({templateType}) failed: {ex.Message}");
            return new();
        }
    }

    private static List<(string DisplayName, string ScenePath)> QueryScenePathOptions(string dbPath, string templateType)
    {
        var results = new List<(string, string)>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.NameEnglish, i.GameId
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = @templateType
            ORDER BY i.NameEnglish";
        command.Parameters.AddWithValue("@templateType", templateType);

        using var reader = command.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetString(1)));

        return results;
    }

    /// <summary>Per-species pet/creature data (Rarity, whether the species can be
    /// used in Holo-Arena Pet Battles, its native MoveArea, and its
    /// PetBattlerForcedAffinity), sourced from the CreatureSpecies category -
    /// CatalogBuildService extracts this from the game's own
    /// metadata/simulation/ecosystem/creaturedatatable.mbin (GcCreatureDataTable),
    /// found while investigating the Companions page's "Battle Abilities" grades (they
    /// turned out NOT to live here, but this data is real and otherwise
    /// undiscoverable, so it's kept). Keyed by the same uppercase archetype code
    /// the save's own pet XID field uses (e.g. "RODENT") - confirmed via real
    /// save data, see ArtifactX.Core.NmsModels.NmsCompanionPaths. Rarity is
    /// species-wide (every Rodent shares the same value), NOT a per-pet-instance
    /// stat the way ujr/Trust/etc. are.</summary>
    public static async Task<Dictionary<string, (string DisplayName, string Rarity, string Description)>> GetCreatureSpeciesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryCreatureSpecies(dbPath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetCreatureSpeciesAsync failed: {ex.Message}");
            return new();
        }
    }

    private static Dictionary<string, (string DisplayName, string Rarity, string Description)> QueryCreatureSpecies(string dbPath)
    {
        var results = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, i.NameEnglish, i.UsageCategory, i.DescriptionEnglish
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'CreatureSpecies'";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[reader.GetString(0)] = (
                reader.GetString(1),
                reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3));
        }

        return results;
    }

    /// <summary>Settlement Perks ("buffs" shown in-game under "Settlement
    /// Features"), sourced from the SettlementPerks category -
    /// CatalogBuildService's Phase 1.8 extracts this from the game's own
    /// metadata/reality/tables/settlementperkstable.mbin
    /// (GcSettlementPerksTable), resolving Name/Description against the
    /// merged loc lookup directly since the generic classifier doesn't
    /// recognize that row shape's fields as loc-key candidates. Keyed by
    /// the raw perk id (e.g. "PROC_FUN", matching a settlement's own osl-
    /// style Perks array once the "^" prefix and any "#NNNNN" per-instance
    /// roll suffix are stripped - see NmsSettlementPaths). Category holds
    /// one of Blessing/Job/Starter/Proc/Negative/Positive; Description
    /// includes a qualitative (not exact-numeric) summary of which Stat(s)
    /// the perk affects and how strongly, e.g. "Improves citizen happiness
    /// (Happiness +Medium)".</summary>
    public static async Task<Dictionary<string, (string DisplayName, string Description, string Category)>> GetSettlementPerksAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QuerySettlementPerks(dbPath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetSettlementPerksAsync failed: {ex.Message}");
            return new();
        }
    }

    private static Dictionary<string, (string DisplayName, string Description, string Category)> QuerySettlementPerks(string dbPath)
    {
        var results = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, i.NameEnglish, i.DescriptionEnglish, i.UsageCategory
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'SettlementPerks'";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(1);
            string description = reader.IsDBNull(2) ? "" : reader.GetString(2);
            results[reader.GetString(0)] = (
                ReadablePerkName(name, description),
                StripPerkPlaceholders(description),
                reader.IsDBNull(3) ? "" : reader.GetString(3));
        }

        return results;
    }

    private static readonly Regex PerkPlaceholderRegex = new(@"%([A-Z_]+)%", RegexOptions.Compiled);

    // Placeholder words too clipped to read on their own. Presentation only -
    // the placeholder tokens themselves still come from the catalog.
    private static readonly Dictionary<string, string> PerkTypeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SENT"] = "Sentinel",
        ["POP"] = "Population",
        ["PROD"] = "Production",
        ["JOBLEVEL"] = "Job",
        ["GREEK"] = "Building",
    };

    /// <summary>"Proc" perks (17 of 90) have names the game assembles per
    /// settlement from word lists ("%BAR_ADJ% %BAR%", "Blessed by the
    /// %BLESSING%"), picked by the "#NNNNN" roll suffix stored with each perk -
    /// not reproducible here, so the real in-game name can't be shown. Stand-in
    /// (user's choice 2026-10-10): keep any literal words with "…" for the
    /// placeholder; a name that's ALL placeholder becomes "Type: effect", the
    /// type word taken from the last placeholder so the four "Increases
    /// productivity" perks (Shop/Factory/Facility/Production) stay distinct.</summary>
    private static string ReadablePerkName(string name, string description)
    {
        if (!name.Contains('%')) return name;

        string literal = PerkPlaceholderRegex.Replace(name, "").Trim();
        if (literal.Length > 0)
            return $"{PerkPlaceholderRegex.Replace(name, "…")} (random name)";

        string lastToken = PerkPlaceholderRegex.Matches(name)[^1].Groups[1].Value;
        string typeWord = PerkTypeWords.TryGetValue(lastToken, out var mapped)
            ? mapped
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lastToken.Replace('_', ' ').ToLowerInvariant());

        string effect = StripPerkPlaceholders(description);
        int paren = effect.IndexOf(" (", StringComparison.Ordinal);
        if (paren > 0) effect = effect[..paren];

        return effect.Length > 0 ? $"{typeWord}: {effect} (random name)" : $"{typeWord} (random name)";
    }

    /// <summary>Drops placeholder tokens from a perk description - e.g.
    /// PROC_JOB's "%JOB_STAT% (Production +Wide)" becomes "Production +Wide".</summary>
    private static string StripPerkPlaceholders(string description)
    {
        if (!description.Contains('%')) return description;
        string stripped = PerkPlaceholderRegex.Replace(description, "").Trim();
        if (stripped.StartsWith('(') && stripped.EndsWith(')'))
            stripped = stripped[1..^1];
        return stripped;
    }

    /// <summary>Readable item names (the catalog's proper-case NameLowerEnglish,
    /// e.g. "Magnetized Ferrite", falling back to the upper-case name) for
    /// products and substances, keyed by GameId.</summary>
    public static async Task<Dictionary<string, string>> GetItemNamesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.GameId, COALESCE(i.NameLowerEnglish, i.NameEnglish)
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType IN ('GcProductTable', 'GcSubstanceTable')
                      AND COALESCE(i.NameLowerEnglish, i.NameEnglish) IS NOT NULL";
                using var reader = command.ExecuteReader();
                while (reader.Read()) names.TryAdd(reader.GetString(0), reader.GetString(1));
                return names;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetItemNamesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Base Units value and trade category of every product and
    /// substance (catalog category "ItemValues", DataCataloger add-item-values).</summary>
    public static async Task<List<ItemValueInfo>> GetItemValuesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new List<ItemValueInfo>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.NameEnglish, i.UsageCategory, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'ItemValues'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(2)) continue;
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
                    var root = json.RootElement;
                    results.Add(new ItemValueInfo(
                        root.GetProperty("id").GetString() ?? "",
                        reader.GetString(0),
                        reader.IsDBNull(1) ? "None" : reader.GetString(1),
                        root.GetProperty("value").GetInt64(),
                        root.GetProperty("legality").GetString() ?? "",
                        root.GetProperty("kind").GetString() ?? ""));
                }
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetItemValuesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Where and when each fish can be caught (catalog category
    /// "Fish", DataCataloger add-fish, from the game's GcFishTable). Biomes are
    /// GcBiomeType indexes - the same numbering as a planet's type in the
    /// save's discovery records; 16 means every planet.</summary>
    public static async Task<List<FishInfo>> GetFishAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new List<FishInfo>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'Fish'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0)) continue;
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(0));
                    var root = json.RootElement;
                    results.Add(new FishInfo(
                        root.GetProperty("id").GetString() ?? "",
                        root.GetProperty("quality").GetString() ?? "",
                        root.GetProperty("size").GetString() ?? "",
                        root.GetProperty("time").GetString() ?? "",
                        root.GetProperty("storm").GetBoolean(),
                        root.GetProperty("mission").GetBoolean(),
                        root.GetProperty("biomes").EnumerateArray().Select(b => b.GetInt32()).ToArray()));
                }
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetFishAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>A plain-English item type for each product/substance, for the
    /// Recipes page's type filter: from the catalog's CatalogGroup (the game's
    /// own "Cooking Products"/"Crafted Products"/"Raw Materials" grouping) and,
    /// for raw substances, their material category (UsageCategory: Fuel, Metal,
    /// Catalyst, Stellar, Earth, Exotic, Flora, Special).</summary>
    public static async Task<Dictionary<string, string>> GetItemTypesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.GameId, c.TemplateType, i.CatalogGroup, i.UsageCategory
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType IN ('GcProductTable', 'GcSubstanceTable')";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string group = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    string usage = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    string type = reader.GetString(1) == "GcSubstanceTable"
                        ? usage switch
                        {
                            "Fuel" => "Fuels (Carbon, Oxygen)",
                            "Metal" => "Ferrite and silicates",
                            "Catalyst" => "Catalysts (Sodium)",
                            "Stellar" => "Stellar metals (Copper, Indium...)",
                            "Earth" => "Earth elements (Cobalt, Salt, Pyrite...)",
                            "Exotic" => "Precious metals (Gold, Silver, Platinum)",
                            "Flora" => "Plants (Solanium, Star Bulb...)",
                            _ => "Special (Chromatic Metal, gases...)",
                        }
                        : group == "Cooking Products" ? "Cooked food" : "Crafted products";
                    types.TryAdd(reader.GetString(0), type);
                }
                return types;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetItemTypesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Every refiner and cooking recipe (catalog category "Recipes",
    /// DataCataloger add-recipes). Empty if the catalog predates that command.</summary>
    /// <summary>What every craftable product and buildable technology is made
    /// from (catalog category "Crafting", DataCataloger add-crafting), by id.
    /// Ingredient ids with no entry here are raw materials.</summary>
    public static async Task<Dictionary<string, CraftInfo>> GetCraftingAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new Dictionary<string, CraftInfo>(StringComparer.OrdinalIgnoreCase);
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.NameEnglish, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'Crafting'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(1)) continue;
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(1));
                    var root = json.RootElement;
                    string id = root.GetProperty("id").GetString() ?? "";
                    var req = root.GetProperty("req").EnumerateArray()
                        .Select(r => (Id: r[0].GetString() ?? "", Amount: r[1].GetInt32()))
                        .Where(r => r.Id.Length > 0)
                        .ToList();
                    if (id.Length > 0) results[id] = new CraftInfo(id, reader.GetString(0), root.GetProperty("kind").GetString() ?? "", req);
                }
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetCraftingAsync failed: {ex.Message}");
            return new();
        }
    }

    public static async Task<List<RecipeInfo>> GetRecipesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() =>
            {
                var results = new List<RecipeInfo>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.GameId, i.NameEnglish, i.UsageCategory, i.DescriptionEnglish
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'Recipes'
                    ORDER BY i.Id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(3)) continue;
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(3));
                    var root = json.RootElement;
                    static (string Id, int Amount) Pair(System.Text.Json.JsonElement e) => (e[0].GetString() ?? "", e[1].GetInt32());
                    results.Add(new RecipeInfo(
                        reader.GetString(0),
                        reader.IsDBNull(1) ? "" : reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Pair(root.GetProperty("result")),
                        root.GetProperty("ingredients").EnumerateArray().Select(Pair).ToList()));
                }
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetRecipesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>The words the game uses for a system's economy, wealth and
    /// conflict (catalog category "SystemDescriptors", DataCataloger
    /// add-system-descriptors): (Word, Group = Economy/Wealth/Conflict, Tier
    /// e.g. SCIENCE/HIGH). Empty if the catalog predates that command.</summary>
    public static async Task<List<(string Word, string Group, string Tier)>> GetSystemDescriptorsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() =>
            {
                var results = new List<(string, string, string)>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.NameEnglish, i.UsageCategory, i.DescriptionEnglish
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'SystemDescriptors'
                    ORDER BY i.Id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    results.Add((reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetSystemDescriptorsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Freighter Recolouring blueprints (catalog category
    /// "FreighterPaints", DataCataloger add-freighter-paints): blueprint id,
    /// in-game name, and the paint colour as "#RRGGBB". Empty if the catalog
    /// predates that command.</summary>
    public static async Task<List<(string GameId, string Name, string Hex)>> GetFreighterPaintsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() =>
            {
                var results = new List<(string, string, string)>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.GameId, i.NameEnglish, i.DescriptionEnglish
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'FreighterPaints'
                    ORDER BY i.Id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    results.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetFreighterPaintsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Frigate trait id -> (DisplayName, formatted description with the
    /// exact numeric stat delta e.g. "Deep Scout Prototype (+15 Combat)", sign
    /// Category) - see CatalogBuildService's Phase 1.9 for how the description
    /// is computed from GcFleetGlobals.FrigateTraitStrengths.</summary>
    public static async Task<Dictionary<string, (string DisplayName, string Description, string Category)>> GetFrigateTraitsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryFrigateTraits(dbPath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetFrigateTraitsAsync failed: {ex.Message}");
            return new();
        }
    }

    private static Dictionary<string, (string DisplayName, string Description, string Category)> QueryFrigateTraits(string dbPath)
    {
        var results = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, i.NameEnglish, i.DescriptionEnglish, i.UsageCategory
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'FrigateTraits'";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[reader.GetString(0)] = (
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3));
        }

        return results;
    }

    /// <summary>Ship Technology/Cargo max-slot counts per (ship type, class letter),
    /// sourced from the ShipCapacity category CatalogBuildService extracts from the
    /// game's own GcInventoryTable.ShipInventoryMaxUpgradeSize (see that service for
    /// how). Returns raw GameId -> CapacityValue pairs (e.g. "DROPSHIP_C_CARGO" ->
    /// 60) rather than anything ship-domain-specific - ArtifactX.Core's
    /// StarshipCapacity owns the key format and the friendly-type-to-raw-name
    /// mapping, this just hands back whatever the catalog has. Rebuilding the
    /// catalog after a game update refreshes these numbers automatically -
    /// nothing in the WinUI3 project needs to change.</summary>
    public static async Task<Dictionary<string, int>> GetShipCapacityAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryCapacityByTemplateType(dbPath, "ShipCapacity"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetShipCapacityAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Multi-Tool Technology max-slot counts per class letter, sourced
    /// from the MultiToolCapacity category CatalogBuildService extracts from the
    /// game's own GcInventoryTable.WeaponInventoryMaxUpgradeSize (see that
    /// service for how). Same GameId -> CapacityValue shape as
    /// GetShipCapacityAsync; ArtifactX.Core's MultiToolCapacity owns the key
    /// format. Unlike ships, multi-tool capacity doesn't vary by Type/shape -
    /// only by class - so there's no per-type dimension to look up here.</summary>
    public static async Task<Dictionary<string, int>> GetMultiToolCapacityAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryCapacityByTemplateType(dbPath, "MultiToolCapacity"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetMultiToolCapacityAsync failed: {ex.Message}");
            return new();
        }
    }

    private static Dictionary<string, int> QueryCapacityByTemplateType(string dbPath, string templateType)
    {
        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, i.CapacityValue
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = @templateType AND i.CapacityValue IS NOT NULL";
        command.Parameters.AddWithValue("@templateType", templateType);

        using var reader = command.ExecuteReader();
        while (reader.Read())
            results[reader.GetString(0)] = reader.GetInt32(1);

        return results;
    }

    /// <summary>Full body-part "Descriptors" option tree for one creature
    /// rig (e.g. "trex"), sourced from CreatureDescriptorOptions - see
    /// CatalogBuildService's Phase 1.7 for how this is extracted and
    /// ArtifactX.Core.NmsModels.NmsCompanionPaths' class doc comment (the "osl"
    /// bullet) for how a pet's own osl save field references these nodes by
    /// OptionId. rigId should be the pet's XID, lowercased - most species
    /// match their rig filename exactly, but a real minority don't (see
    /// Phase 1.7's comment for the confirmed exceptions); this returns an
    /// empty list for those rather than guessing, so callers should treat
    /// an empty result as "no tree data available for this pet's species",
    /// not an error.</summary>
    public static async Task<List<CreatureDescriptorNode>> GetCreatureDescriptorTreeAsync(string rigId)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryCreatureDescriptorTree(dbPath, rigId));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetCreatureDescriptorTreeAsync({rigId}) failed: {ex.Message}");
            return new();
        }
    }

    private static List<CreatureDescriptorNode> QueryCreatureDescriptorTree(string dbPath, string rigId)
    {
        var results = new List<CreatureDescriptorNode>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT c.Category, c.OptionId, c.Name, p.OptionId AS ParentOptionId, c.Id AS SortOrder
            FROM CreatureDescriptorOptions c
            LEFT JOIN CreatureDescriptorOptions p ON p.Id = c.ParentOptionId
            WHERE c.RigId = @rigId";
        command.Parameters.AddWithValue("@rigId", rigId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CreatureDescriptorNode
            {
                Category = reader.GetString(0),
                OptionId = reader.GetString(1),
                Name = reader.GetString(2),
                ParentOptionId = reader.IsDBNull(3) ? null : reader.GetString(3),
                SortOrder = reader.GetInt32(4)
            });
        }
        return results;
    }

    private static List<LanguageWord>? _languageWordsCache;

    /// <summary>Every alien-language vocabulary word across all 4 races (~4,570
    /// total), from the GcAlienLanguageWords category DataCataloger's
    /// `add-language-words` command extracts from language/nms_loc1_english.mbin -
    /// see NmsLanguagePaths' doc comment for how this was confirmed real.
    /// UsageCategory holds the race name ("Gek"/"Vy'keen"/"Korvax"/"Autophage")
    /// directly, so no id-prefix parsing is needed by callers.</summary>
    public static async Task<List<LanguageWord>> GetAllLanguageWordsAsync()
    {
        if (_languageWordsCache is not null) return _languageWordsCache;

        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            _languageWordsCache = await Task.Run(() => QueryLanguageWords(dbPath));
            return _languageWordsCache;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetAllLanguageWordsAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<LanguageWord> QueryLanguageWords(string dbPath)
    {
        var results = new List<LanguageWord>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, i.NameEnglish, i.UsageCategory
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'GcAlienLanguageWords'";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new LanguageWord
            {
                GameId = reader.GetString(0),
                DisplayName = reader.GetString(1),
                Race = reader.GetString(2)
            });
        }
        return results;
    }

    // Cached process-wide (not per-page) - the underlying rows never change
    // within an app session, and both AccountDataPage and CataloguePage need
    // the same full ~4,700-row set, so caching it once here avoids each page
    // keeping its own separate copy of the same query result.
    private static List<CatalogUnlockableItem>? _unlockableItemsCache;

    /// <summary>Every catalog item that can plausibly appear in the account-wide
    /// "unlocked items" list (technology/product/substance rows only - the
    /// scene-path option tables like MultiToolTypes/ShipCapacity are model/slot
    /// metadata, not player-facing unlockables, so they're excluded here).
    /// Used by AccountDataPage/CataloguePage to build their full browsable lists
    /// up front; unlike SearchAsync this isn't restricted by name/count, so it
    /// returns the whole set in one call for each page to filter locally.</summary>
    public static async Task<List<CatalogUnlockableItem>> GetAllUnlockableItemsAsync()
    {
        if (_unlockableItemsCache is not null) return _unlockableItemsCache;

        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            _unlockableItemsCache = await Task.Run(() => QueryAllUnlockableItems(dbPath));
            return _unlockableItemsCache;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetAllUnlockableItemsAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<GuideTopicCatalogRow>? _guideTopicsCache;

    /// <summary>Every real in-game Guide topic, grouped by its real section
    /// name - see GuideTopicCatalogRow's own doc comment. Cached process-wide
    /// like GetAllUnlockableItemsAsync, same reasoning (GuidePage is the only
    /// consumer today, but the underlying rows never change within a session
    /// regardless).</summary>
    public static async Task<List<GuideTopicCatalogRow>> GetGuideTopicsAsync()
    {
        if (_guideTopicsCache is not null) return _guideTopicsCache;

        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            _guideTopicsCache = await Task.Run(() => QueryGuideTopics(dbPath));
            return _guideTopicsCache;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetGuideTopicsAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<GuideTopicCatalogRow> QueryGuideTopics(string dbPath)
    {
        var results = new List<GuideTopicCatalogRow>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, i.NameEnglish, i.UsageCategory
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'GcWiki'
            ORDER BY i.UsageCategory, i.NameEnglish";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new GuideTopicCatalogRow
            {
                GameId = reader.GetString(0),
                DisplayName = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1),
                Category = reader.IsDBNull(2) ? "Uncategorized" : reader.GetString(2)
            });
        }
        return results;
    }

    private static List<CatalogUnlockableItem> QueryAllUnlockableItems(string dbPath)
    {
        var results = new List<CatalogUnlockableItem>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, COALESCE(i.NameEnglish, i.NameLowerEnglish) AS DisplayName, c.TemplateType, i.UsageCategory, i.CatalogGroup, i.IsBrokenSlotTech
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType IN ('GcProductTable','GcSubstanceTable','GcTechnologyTable','GcProceduralTechnologyTable')
              AND (i.NameEnglish IS NOT NULL OR i.NameLowerEnglish IS NOT NULL)
              AND NOT (c.TemplateType = 'GcTechnologyTable' AND i.NameEnglish = 'UPGRADE MODULE' AND i.DescriptionEnglish IS NULL)
            ORDER BY DisplayName";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CatalogUnlockableItem
            {
                GameId = reader.GetString(0),
                DisplayName = reader.GetString(1),
                CategoryLabel = ElvLabelFor(reader.GetString(2)),
                UsageCategory = reader.IsDBNull(3) ? null : reader.GetString(3),
                CatalogGroup = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsBrokenSlotTech = reader.GetBoolean(5)
            });
        }
        return results;
    }

    private static readonly Regex ExpeditionBannerPattern = new(@"^EXPD_BANNER(\d{2})$", RegexOptions.Compiled);
    private static readonly Regex ExpeditionItemPattern = new(@"^EXPD_(?:BANNER|DECAL|TITLE)(\d{2})[A-Z]?$", RegexOptions.Compiled);

    /// <summary>Derives expedition number -> theme name (1 -> "PIONEERS", 22 -> "SWARM",
    /// etc.) from the EXPD_BANNER## item family's own display names, which spell the
    /// expedition out directly ("PIONEERS EXPEDITION BANNER"/"SWARM BANNER" - two
    /// naming conventions used across the game's history, both handled). Confirmed
    /// 2026-08-04 as a reliable, self-describing source, unlike most other EXPD_ items
    /// (backpacks/capes/ships/etc.), which have no expedition-number signal at all -
    /// deliberately not attempted for those rather than guessing wrong. No external
    /// API/hardcoded list involved: this is a live scan of whatever's in the catalog
    /// DB, so a future expedition's EXPD_BANNER23 gets picked up automatically on the
    /// next DataCataloger rebuild, no code change needed.</summary>
    public static Dictionary<int, string> BuildExpeditionNameLookup(IEnumerable<CatalogUnlockableItem> catalogItems)
    {
        var result = new Dictionary<int, string>();
        foreach (var item in catalogItems)
        {
            var match = ExpeditionBannerPattern.Match(item.GameId);
            if (!match.Success) continue;

            int number = int.Parse(match.Groups[1].Value);
            string name = Regex.Replace(item.DisplayName, @"\s+BANNER$", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"\s+EXPEDITION$", "", RegexOptions.IgnoreCase).Trim();
            result[number] = name;
        }
        return result;
    }

    /// <summary>If gameId is a Banner/Decal/Title item from a numbered expedition (see
    /// BuildExpeditionNameLookup), returns its expedition number/name as data -
    /// otherwise (null, null). Every other EXPD_ item (the large majority) has no
    /// reliable expedition-number signal, so is deliberately left unmatched rather
    /// than guessing from a numeric id suffix that may not actually correspond to
    /// expedition order for that item family. Feeds AccountItemRowViewModel's
    /// ExpeditionNumber/ExpeditionName fields directly - display formatting (column
    /// text, filter labels) is the caller's job, not this method's.</summary>
    public static (int? Number, string? Name) GetExpeditionInfo(string gameId, Dictionary<int, string> expeditionNames)
    {
        var match = ExpeditionItemPattern.Match(gameId);
        if (!match.Success) return (null, null);

        int number = int.Parse(match.Groups[1].Value);
        return expeditionNames.TryGetValue(number, out var name)
            ? (number, name)
            : (null, null);
    }

    private static readonly Regex FishSpeciesPattern = new(@"^F_[A-Z]+_(COM|RARE|EPIC|LEG)_[A-Z0-9]+$", RegexOptions.Compiled);

    /// <summary>Every real fish species catchable via GcFishTable, identified by
    /// Hello Games' own id convention (F_&lt;biome&gt;_&lt;quality&gt;_&lt;size&gt;,
    /// e.g. F_ALL_COM_S2 = "Mud Crab") - confirmed 2026-08-05 by cross-referencing
    /// against a real ~100-hour save's actual fishing-record array
    /// (NmsFishingPaths.ProductListPath): 210 of that save's 220 real entries
    /// matched this pattern exactly, and the other 10 (F_BOTTLE, F_JELLYCHILD,
    /// F_TRASH_1..8) are confirmed non-fish "junk catch" items, deliberately
    /// excluded here since they're not real fishing records. No hardcoded id
    /// list or DataCataloger changes needed - this is a live pattern match
    /// against whatever's already in the catalog DB (GcProductTable), so a
    /// future game update's new fish is picked up automatically on the next
    /// DataCataloger rebuild.</summary>
    public static List<CatalogUnlockableItem> FilterFishSpecies(IEnumerable<CatalogUnlockableItem> catalogItems) =>
        catalogItems.Where(c => FishSpeciesPattern.IsMatch(c.GameId)).OrderBy(c => c.DisplayName).ToList();

    /// <summary>Same rule as FilterFishSpecies, for a single id.</summary>
    public static bool IsFishSpecies(string gameId) => FishSpeciesPattern.IsMatch(gameId);

    private static string? ResolveDbPath()
    {
        if (_pathChecked) return _dbPath;
        _pathChecked = true;

        string candidate = Path.Combine(AppContext.BaseDirectory, "Data", "nms_catalog.sqlite");
        _dbPath = File.Exists(candidate) ? candidate : null;

        Debug.WriteLine(_dbPath is not null
            ? $"[CatalogService] Catalog found at: {_dbPath}"
            : $"[CatalogService] Catalog NOT found. Checked: {candidate}");

        return _dbPath;
    }
}

/// <summary>One refiner/cooking recipe: Kind is "Refiner" or "Cooking"; ids are item GameIds.</summary>
/// <summary>A craftable product or buildable technology: Kind is "Product" or
/// "Technology"; Requirements are ingredient ids and amounts for one.</summary>
public sealed record CraftInfo(string Id, string Name, string Kind, List<(string Id, int Amount)> Requirements);

public sealed record RecipeInfo(string Id, string Name, string Kind, (string Id, int Amount) Result, List<(string Id, int Amount)> Ingredients);

/// <summary>An item's base Units value. TradeCategory is the game's own
/// (Mineral, Tech, Commodity, Component, Alloy, Exotic, Energy, None, SpecialShop);
/// Kind is "Product" or "Substance".</summary>
public sealed record FishInfo(string Id, string Quality, string Size, string Time, bool NeedsStorm, bool NeedsMission, int[] Biomes)
{
    public const int AnyPlanet = 16;
    public bool LivesOn(int biome) => Biomes.Contains(AnyPlanet) || Biomes.Contains(biome);
}

public sealed record ItemValueInfo(string Id, string Name, string TradeCategory, long Value, string Legality, string Kind);
