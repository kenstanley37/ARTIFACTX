using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace ArtifactX.WinUI3.Services;

public static partial class CatalogService
{
    // Read from background work too (e.g. the Bases list is built in Task.Run).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CatalogEntry?> Cache = new();
    private static string? _dbPath;
    private static bool _pathChecked;

    public static bool IsAvailable => ResolveDbPath() is not null;
    public static long Revision { get; private set; }

    /// <summary>Forgets everything read so far - after the game data was
    /// rebuilt (GameDataService). Call on the UI thread; pages pick the new
    /// catalog up as they load.</summary>
    private static Dictionary<string, IReadOnlyList<string>>? _namingWords;
    private static volatile IReadOnlyDictionary<string, string>? _moduleClasses;
    private static volatile IReadOnlyDictionary<string, ProceduralProductNaming>? _productNaming;

    public static void Reload()
    {
        Revision++;
        Cache.Clear();
        _namingWords = null;
        _moduleClasses = null;
        _productNaming = null;
        _languageWordsCache = null;
        _unlockableItemsCache = null;
        _guideTopicsCache = null;
        _pathChecked = false;
        _dbPath = null;
        Reloaded?.Invoke();
    }

    /// <summary>Raised after <see cref="Reload"/> (a game data rebuild), so services that keep results worked
    /// out from catalog tables drop them.</summary>
    public static event Action? Reloaded;

    /// <summary>The cached entry for an id. An id WarmCacheAsync hasn't seen is looked
    /// up in the catalog once (name only, no icon - WarmCacheAsync still fetches that),
    /// and an id the catalog doesn't have is remembered as missing, so it isn't
    /// queried again. Prefer warming the ids first: this lookup is synchronous.</summary>
    public static CatalogEntry? TryGet(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return null;
        string key = NormalizeId(gameId);
        if (Cache.TryGetValue(key, out var entry)) return entry;

        if (ResolveDbPath() is not string dbPath) return null;
        CatalogEntry? fetched = null;
        try
        {
            if (QueryRows(dbPath, new List<string> { key }).TryGetValue(key, out var row))
                fetched = new CatalogEntry
                {
                    DisplayName = row.DisplayName,
                    Icon = null,
                    CatalogGroup = row.CatalogGroup,
                    TemplateType = row.TemplateType,
                };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] TryGet({key}) failed: {ex.Message}");
            return null; // not remembered: a failed read may work next time
        }
        Cache[key] = fetched;
        return fetched;
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

        Dictionary<string, (string DisplayName, byte[]? IconPng, string? CatalogGroup, string? TemplateType)> rows;
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

            Cache[id] = new CatalogEntry
            {
                DisplayName = row.DisplayName,
                Icon = icon,
                IconPng = icon is null ? null : row.IconPng,
                CatalogGroup = row.CatalogGroup,
                TemplateType = row.TemplateType,
            };
        }
    }

    /// <summary>
    /// The class badge the game draws on a procedural upgrade module ("UP_LASERX#92128" -> "X"),
    /// from the catalog's extracted quality: Normal/Rare/Epic/Legendary/Illegal = C/B/A/S/X.
    /// Sentinel, Robot and SeaTrash modules have no letter mapping yet and return null,
    /// as does every non-module item and a catalog built before format 43.
    /// </summary>
    public static string? ModuleClassLetter(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId) || !gameId.Contains('#')) return null;
        var classes = _moduleClasses ??= LoadModuleClasses();
        return classes.GetValueOrDefault(NormalizeId(gameId));
    }

    private static IReadOnlyDictionary<string, string> LoadModuleClasses()
    {
        var classes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ResolveDbPath() is not string dbPath) return classes;
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT GameId, ModuleQuality FROM Items WHERE ModuleQuality IS NOT NULL";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? letter = reader.GetString(1) switch
                {
                    "Normal" => "C", "Rare" => "B", "Epic" => "A", "Legendary" => "S", "Illegal" => "X",
                    "SeaTrash" => "!", "Sentinel" or "Robot" => "?", _ => null,
                };
                if (letter is not null) classes[reader.GetString(0)] = letter;
            }
        }
        catch (SqliteException ex)
        {
            Debug.WriteLine($"[CatalogService] Module classes unavailable (catalog before format 43?): {ex.Message}");
        }
        return classes;
    }

    /// <summary>
    /// The name the game gives a procedural product from its seed ("PROC_LOOT#06686" ->
    /// "Shiny Bones", ArtifactX.Core ProceduralProductNames), or null for any other id,
    /// a product whose naming isn't read yet (freighter logs and tech, DismantleData,
    /// MessageInBottle) and a catalog built before format 65.
    /// </summary>
    public static string? ProceduralProductName(string? itemId)
    {
        string id = (itemId ?? "").TrimStart('^');
        int hash = id.IndexOf('#');
        if (hash <= 0) return null;
        var naming = _productNaming ??= LoadProductNaming();
        return naming.TryGetValue(id[..hash], out var product) ? ProceduralProductNames.Generate(id, product) : null;
    }

    private static IReadOnlyDictionary<string, ProceduralProductNaming> LoadProductNaming()
    {
        var naming = new Dictionary<string, ProceduralProductNaming>(StringComparer.OrdinalIgnoreCase);
        if (ResolveDbPath() is not string dbPath) return naming;
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId = 'PROCEDURALPRODUCTNAMES'";
            if (command.ExecuteScalar() is string json)
                foreach (var product in System.Text.Json.JsonSerializer.Deserialize<List<ProceduralProductNaming>>(json) ?? [])
                    naming[product.ProductId] = product;
        }
        catch (Exception ex) when (ex is SqliteException or System.Text.Json.JsonException)
        {
            Debug.WriteLine($"[CatalogService] Procedural product names unavailable (catalog before format 65?): {ex.Message}");
        }
        return naming;
    }

    /// <summary>A product or substance's save category ("Product"/"Substance") and
    /// catalog stack size, for adding it to an inventory; null when it is neither.</summary>
    public static (string CategoryLabel, int? MaxStackSize)? TryGetStackInfo(string itemId)
    {
        if (ResolveDbPath() is not string dbPath) return null;
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.TemplateType, i.MaxStackSize FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE i.GameId = $id AND c.TemplateType IN ('GcProductTable', 'GcSubstanceTable') LIMIT 1";
            command.Parameters.AddWithValue("$id", NormalizeId(itemId));
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            return (ElvLabelFor(reader.GetString(0)), reader.IsDBNull(1) ? null : reader.GetInt32(1));
        }
        catch (SqliteException ex)
        {
            Debug.WriteLine($"[CatalogService] TryGetStackInfo failed: {ex.Message}");
            return null;
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
            WHERE (i.NameEnglish IS NOT NULL OR i.NameLowerEnglish IS NOT NULL)
              AND c.TemplateType IN ({typePlaceholders})
              AND (i.NameEnglish LIKE @q OR i.NameLowerEnglish LIKE @q OR i.GameId LIKE @q)
              {usageClause}
            ORDER BY
              CASE
                WHEN LOWER(COALESCE(i.NameEnglish, i.NameLowerEnglish)) = @rawQ THEN 1
                WHEN LOWER(COALESCE(i.NameEnglish, i.NameLowerEnglish)) LIKE @prefixQ THEN 2
                WHEN LOWER(COALESCE(i.NameEnglish, i.NameLowerEnglish)) LIKE @wordQ THEN 3
                ELSE 4
              END,
              LENGTH(COALESCE(i.NameEnglish, i.NameLowerEnglish)),
              COALESCE(i.NameEnglish, i.NameLowerEnglish)
            LIMIT @max";

        for (int i = 0; i < allowedTemplateTypes.Length; i++)
            command.Parameters.AddWithValue($"@t{i}", allowedTemplateTypes[i]);

        if (allowedUsageCategories is { Length: > 0 })
        {
            for (int i = 0; i < allowedUsageCategories.Length; i++)
                command.Parameters.AddWithValue($"@u{i}", allowedUsageCategories[i]);
        }

        string qLower = query.ToLowerInvariant();
        command.Parameters.AddWithValue("@rawQ", qLower);
        command.Parameters.AddWithValue("@prefixQ", $"{qLower}%");
        command.Parameters.AddWithValue("@wordQ", $"% {qLower}%");
        command.Parameters.AddWithValue("@q", $"%{query}%");
        command.Parameters.AddWithValue("@max", maxResults);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string gameId = reader.GetString(0);
            if (!seen.Add(gameId)) continue;
            string templateType = reader.GetString(1);

            results.Add(new CatalogSearchResult
            {
                GameId = gameId,
                TemplateType = templateType,
                CategoryLabel = ElvLabelFor(templateType),
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

    private static Dictionary<string, (string DisplayName, byte[]? IconPng, string? CatalogGroup, string? TemplateType)> QueryRows(string dbPath, List<string> gameIds)
    {
        var result = new Dictionary<string, (string, byte[]?, string?, string?)>();

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
            SELECT i.GameId, COALESCE(i.NameEnglish, i.NameLowerEnglish) AS DisplayName, b.PngData, i.CatalogGroup, c.TemplateType
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            LEFT JOIN Icons ic ON ic.ItemId = i.Id
            LEFT JOIN IconBlobs b ON b.Id = ic.IconBlobId
            WHERE (i.NameEnglish IS NOT NULL OR i.NameLowerEnglish IS NOT NULL) AND i.GameId IN ({placeholders})
            ORDER BY
              CASE c.TemplateType
                WHEN 'GcTechnologyTable' THEN 1
                WHEN 'GcProceduralTechnologyTable' THEN 2
                WHEN 'GcProductTable' THEN 3
                WHEN 'GcSubstanceTable' THEN 4
                WHEN 'ProceduralProducts' THEN 5
                ELSE 10
              END,
              CASE WHEN b.PngData IS NOT NULL THEN 1 ELSE 2 END";

        for (int i = 0; i < gameIds.Count; i++)
            command.Parameters.AddWithValue($"@p{i}", gameIds[i]);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string gameId = reader.GetString(0);
            if (result.ContainsKey(gameId)) continue;

            string name = reader.GetString(1);
            byte[]? png = reader.IsDBNull(2) ? null : (byte[])reader["PngData"];
            string? catalogGroup = reader.IsDBNull(3) ? null : reader.GetString(3);
            string? templateType = reader.IsDBNull(4) ? null : reader.GetString(4);
            result[gameId] = (name, png, catalogGroup, templateType);
        }
        return result;
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

    /// <summary>Raw JSON rows of one catalog category, by lower-case path
    /// (NameEnglish) - for the ship look data ("ShipScenes", "ShipMaterials",
    /// "TextureLists"), which ShipLook parses on demand.</summary>
    public static async Task<Dictionary<string, string>> GetJsonCategoryAsync(string templateType)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.NameEnglish, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = $type";
                command.Parameters.AddWithValue("$type", templateType);
                using var reader = command.ExecuteReader();
                while (reader.Read()) results[reader.GetString(0)] = reader.GetString(1);
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetJsonCategoryAsync({templateType}) failed: {ex.Message}");
            return new();
        }
    }

    internal static string? ResolveDbPath()
    {
        if (_pathChecked) return _dbPath;
        _pathChecked = true;

        // Built on this PC from the player's own game files (GameDataService).
        _dbPath = GameDataService.ActiveCatalogPath;

        Debug.WriteLine(_dbPath is not null
            ? $"[CatalogService] Catalog found at: {_dbPath}"
            : $"[CatalogService] No catalog yet (not built). Checked: {GameDataService.CatalogPath}");

        return _dbPath;
    }
}

public sealed record ItemValueInfo(string Id, string Name, string TradeCategory, long Value, string Legality, string Kind);
