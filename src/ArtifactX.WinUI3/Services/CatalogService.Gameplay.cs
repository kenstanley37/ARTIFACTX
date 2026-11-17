using ArtifactX.Core.Companions;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

public static partial class CatalogService
{
    private static List<LanguageWord>? _languageWordsCache;
    private static List<CatalogUnlockableItem>? _unlockableItemsCache;
    private static List<GuideTopicCatalogRow>? _guideTopicsCache;

    /// <summary>Per-species pet/creature data (Rarity, whether the species can be
    /// used in Holo-Arena Pet Battles, its native MoveArea, and its
    /// PetBattlerForcedAffinity), sourced from the CreatureSpecies category.</summary>
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

    /// <summary>The Egg Sequencer's rules (catalog row EGGSEQUENCER, format 66),
    /// or null for an older catalog.</summary>
    public static Task<EggSequencerRules?> GetEggSequencerRulesAsync() => Task.Run(() =>
    {
        if (ResolveDbPath() is not string dbPath) return null;
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId = 'EGGSEQUENCER'";
            return command.ExecuteScalar() is string json
                && System.Text.Json.JsonSerializer.Deserialize<EggSequencerRules>(json) is { Ingredients: not null, Texts: not null, Species: not null } rules
                ? rules : null;
        }
        catch (Exception ex) when (ex is SqliteException or System.Text.Json.JsonException)
        {
            Debug.WriteLine($"[CatalogService] Egg Sequencer rules unavailable: {ex.Message}");
            return null;
        }
    });

    /// <summary>Where and when each fish can be caught (catalog category "Fish").</summary>
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

    /// <summary>Items for the Food Storage page (2026-10-15).</summary>
    public static async Task<List<CatalogPart>> GetFoodStorageItemsAsync(bool coldStorage)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        var fish = coldStorage
            ? (await GetFishAsync()).Where(f => IsFishSpecies(f.Id))
                .GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, FishInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return await Task.Run(() =>
            {
                var parts = new List<CatalogPart>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = coldStorage
                    ? @"SELECT i.GameId, i.NameEnglish, i.CatalogGroup, i.MaxStackSize
                        FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                        WHERE c.TemplateType IN ('GcProductTable', 'GcSubstanceTable') AND i.NameEnglish IS NOT NULL
                          AND (i.GameId LIKE 'F!_%' ESCAPE '!' OR i.GameId LIKE 'FISHBAIT!_%' ESCAPE '!')"
                    : @"SELECT i.GameId, i.NameEnglish, i.CatalogGroup, i.MaxStackSize
                        FROM Items ck
                        JOIN Categories cc ON cc.Id = ck.CategoryId
                        JOIN Items i ON i.GameId = substr(ck.GameId, 6)
                        JOIN Categories c ON c.Id = i.CategoryId
                        WHERE cc.TemplateType = 'CookingIngredients' AND ck.UsageCategory = 'Ingredient'
                          AND c.TemplateType IN ('GcProductTable', 'GcSubstanceTable') AND i.NameEnglish IS NOT NULL
                          AND i.GameId NOT LIKE 'ANY!_%' ESCAPE '!'
                        GROUP BY i.GameId";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    bool bait = id.StartsWith("FISHBAIT_", StringComparison.OrdinalIgnoreCase);
                    if (coldStorage && !bait && !fish.ContainsKey(id)) continue;
                    string group = coldStorage ? (bait ? "Bait" : "Fish")
                        : fish.ContainsKey(id) || IsFishSpecies(id) ? "Fish"
                        : bait || id.StartsWith("BAIT_", StringComparison.OrdinalIgnoreCase) ? "Bait"
                        : reader.IsDBNull(2) ? "Other" : reader.GetString(2);
                    fish.TryGetValue(id, out var fishInfo);
                    int rarityOrder = fishInfo is not null && Enum.TryParse<libMBIN.NMS.GameComponents.GcItemQuality.ItemQualityEnum>(fishInfo.Quality, out var quality)
                        ? (int)quality : int.MaxValue;
                    parts.Add(new CatalogPart(id, reader.GetString(1), group, reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        Tag: fishInfo?.RarityName(), TagOrder: rarityOrder));
                }
                return parts.OrderBy(p => p.Group).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetFoodStorageItemsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>What every craftable product and buildable technology is made
    /// from (catalog category "Crafting", DataCataloger add-crafting), by id.</summary>
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

    /// <summary>Full body-part "Descriptors" option tree for one creature rig.</summary>
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

    /// <summary>Finds which creature rig an option ID belongs to in CreatureDescriptorOptions.</summary>
    public static async Task<string?> GetRigForDescriptorOptionAsync(string optionId)
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return null;
        try
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT RigId FROM CreatureDescriptorOptions WHERE OptionId = @opt LIMIT 1";
                command.Parameters.AddWithValue("@opt", optionId.TrimStart('^'));
                return command.ExecuteScalar() as string;
            });
        }
        catch { return null; }
    }

    /// <summary>Every learnable alien word group (~3,840 across the 4 races and Atlas).</summary>
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

    /// <summary>Every catalog item that can plausibly appear in the account-wide
    /// "unlocked items" list (technology/product/substance rows only).</summary>
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
                TemplateType = reader.GetString(2),
                UsageCategory = reader.IsDBNull(3) ? null : reader.GetString(3),
                CatalogGroup = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsBrokenSlotTech = reader.GetBoolean(5)
            });
        }
        return results;
    }

    /// <summary>Every real in-game Guide topic, grouped by its real section name.</summary>
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

    private static readonly Regex ExpeditionBannerPattern = new(@"^EXPD_BANNER(\d{2})$", RegexOptions.Compiled);
    private static readonly Regex ExpeditionItemPattern = new(@"^EXPD_(?:BANNER|DECAL|TITLE)(\d{2})[A-Z]?$", RegexOptions.Compiled);

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
    private static readonly Regex FlotsamPattern = new(@"^F_(TRASH_\d+|BOTTLE)$", RegexOptions.Compiled);

    public static List<CatalogUnlockableItem> FilterFishSpecies(IEnumerable<CatalogUnlockableItem> catalogItems) =>
        catalogItems.Where(c => FishSpeciesPattern.IsMatch(c.GameId)).OrderBy(c => c.DisplayName).ToList();

    public static List<CatalogUnlockableItem> FilterFlotsam(IEnumerable<CatalogUnlockableItem> catalogItems) =>
        catalogItems.Where(c => FlotsamPattern.IsMatch(c.GameId)).OrderBy(c => c.GameId, StringComparer.Ordinal).ToList();

    public static bool IsFishSpecies(string gameId) => FishSpeciesPattern.IsMatch(gameId);
}

public sealed record CraftInfo(string Id, string Name, string Kind, List<(string Id, int Amount)> Requirements);

public sealed record RecipeInfo(string Id, string Name, string Kind, (string Id, int Amount) Result, List<(string Id, int Amount)> Ingredients);

public sealed record FishInfo(string Id, string Quality, string Size, string Time, bool NeedsStorm, bool NeedsMission, int[] Biomes)
{
    public const int AnyPlanet = 16;
    public bool LivesOn(int biome) => Biomes.Contains(AnyPlanet) || Biomes.Contains(biome);

    public static string RarityName(string quality) => quality switch
    {
        "Rare" => "Uncommon",
        "Epic" => "Rare",
        _ => quality,
    };

    public string RarityName() => RarityName(Quality);
}
