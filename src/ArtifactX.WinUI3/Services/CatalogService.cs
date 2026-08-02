using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Media.Imaging;
using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    /// back to a raw-id label with no icon before this stripped it too.</summary>
    private static string NormalizeId(string gameId)
    {
        string trimmed = gameId.TrimStart('^');
        int hashIndex = trimmed.IndexOf('#');
        return hashIndex >= 0 ? trimmed[..hashIndex] : trimmed;
    }

    public static async Task WarmCacheAsync(IEnumerable<string?> gameIds)
    {
        var toFetch = gameIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => NormalizeId(id!))
            .Distinct()
            .Where(id => !Cache.ContainsKey(id))
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
    /// (PIRATEFREIGHTER) - cross-checked against NomNom.</summary>
    public static async Task<List<(string DisplayName, string ScenePath)>> GetFreighterTypesAsync() =>
        await GetScenePathOptionsAsync("FreighterTypes");

    /// <summary>Freighter crew captain "Race" options (Gek/Korvax/Vy'keen/Robot),
    /// sourced from the FreighterCrewRaces category - the captain is a separate
    /// model reference (Sjw.93M) from the freighter hull itself (bIR.93M).</summary>
    public static async Task<List<(string DisplayName, string ScenePath)>> GetFreighterCrewRacesAsync() =>
        await GetScenePathOptionsAsync("FreighterCrewRaces");

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
            results[reader.GetString(0)] = (
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3));
        }

        return results;
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

    /// <summary>Every catalog item that can plausibly appear in the account-wide
    /// "unlocked items" list (technology/product/substance rows only - the
    /// scene-path option tables like MultiToolTypes/ShipCapacity are model/slot
    /// metadata, not player-facing unlockables, so they're excluded here).
    /// Used by AccountDataPage to build its full browsable list up front; unlike
    /// SearchAsync this isn't restricted by name/count, so it returns the whole
    /// ~4,700-row set in one call for the page to filter locally.</summary>
    public static async Task<List<CatalogUnlockableItem>> GetAllUnlockableItemsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryAllUnlockableItems(dbPath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetAllUnlockableItemsAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<CatalogUnlockableItem> QueryAllUnlockableItems(string dbPath)
    {
        var results = new List<CatalogUnlockableItem>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.GameId, COALESCE(i.NameEnglish, i.NameLowerEnglish) AS DisplayName, c.TemplateType
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType IN ('GcProductTable','GcSubstanceTable','GcTechnologyTable','GcProceduralTechnologyTable')
              AND (i.NameEnglish IS NOT NULL OR i.NameLowerEnglish IS NOT NULL)
            ORDER BY DisplayName";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CatalogUnlockableItem
            {
                GameId = reader.GetString(0),
                DisplayName = reader.GetString(1),
                CategoryLabel = ElvLabelFor(reader.GetString(2))
            });
        }
        return results;
    }

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