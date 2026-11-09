using ArtifactX.Core.Frigates;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

public static partial class CatalogService
{
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

    private static (string? Label, int Order) CorvettePartSource(string? shop, ArtifactX.Core.Unlocks.ExpeditionReward? reward, Dictionary<int, string> expeditionNames) => shop switch
    {
        null => (null, 0),
        "basic" => ("Buy at the workshop", 0),
        "barter" => ("Trade at the workshop", 1),
        _ when shop.StartsWith("reward:", StringComparison.Ordinal) && reward is not null
            => ("Expedition reward: " + CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                expeditionNames.GetValueOrDefault(reward.Expedition, $"Expedition {reward.Expedition}").ToLowerInvariant()), 2),
        _ when shop.StartsWith("reward:", StringComparison.Ordinal) => ("Expedition reward", 2),
        _ => ("Not sold at the workshop", 3),
    };

    /// <summary>The parts a corvette is built from - the game's own
    /// BuildableInShipStructural flag (DataCataloger add-corvette-parts) -
    /// joined to the product row for the name and stack size, A-Z.</summary>
    public static async Task<List<CatalogPart>> GetCorvettePartsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            var rewards = await ExpeditionRewardsService.LoadAsync();
            var expeditionNames = BuildExpeditionNameLookup(await GetAllUnlockableItemsAsync());
            return await Task.Run(() =>
            {
                var parts = new List<CatalogPart>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT p.GameId, p.NameEnglish, cp.DescriptionEnglish, p.MaxStackSize
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
                {
                    string id = reader.GetString(0);
                    string? shop = null;
                    string type = "";
                    if (!reader.IsDBNull(2))
                    {
                        using var facts = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
                        if (facts.RootElement.TryGetProperty("shop", out var s)) shop = s.GetString() ?? "";
                        if (facts.RootElement.TryGetProperty("type", out var t)) type = t.GetString() ?? "";
                    }
                    var (source, order) = CorvettePartSource(shop, rewards.GetValueOrDefault(id), expeditionNames);
                    parts.Add(new CatalogPart(id, reader.GetString(1), type, reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        Tag: source, TagOrder: order));
                }
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
    /// (a ship kind's own set - see ShipsPage.TechRuleFor).</summary>
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
                      AND i.GameId <> 'OBSOLETE'
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
    /// the given module families, e.g. "UP_PULSE".</summary>
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
                    SELECT i.GameId, i.NameLowerEnglish, i.DescriptionEnglish, s.NameEnglish, s.DescriptionEnglish, s.UsageCategory
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    LEFT JOIN Items s ON s.GameId = 'TECHSTAT:' || i.GameId
                    WHERE c.TemplateType = 'GcProceduralTechnologyTable'
                      AND substr(i.GameId, 1, length(i.GameId) - 1) IN ({string.Join(", ", names.Select(n => n.Name))})
                      AND substr(i.GameId, -1) IN ('1', '2', '3', '4')";
                foreach (var n in names) command.Parameters.AddWithValue(n.Name, n.Value);
                using var reader = command.ExecuteReader();
                var rows = new List<(string Id, string? Plain, string? Desc, string? Stat, string? Colour, string? Template)>();
                while (reader.Read())
                    rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                        !reader.IsDBNull(5) && reader.GetString(5).StartsWith("Module:") ? reader.GetString(5)[7..] : null));
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
                    foreach (string prefix in new[] { "Exosuit ", "Starship ", "Multi-Tool " })
                        if (tech.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) tech = tech[prefix.Length..];
                    modules.Add(new CatalogPart(id, $"{tech} upgrade (S)", "Upgrade modules", null,
                        CountKey: id[..^1], Limit: 3, Seeded: true, Stat: best.Stat, Colour: best.Colour, TemplateId: best.Template));
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

    /// <summary>Ship-builder parts (catalog "PartGroups"): the part item's id
    /// (FIGHT_COCKAA - a product with a name and icon), its set (FIGHT_COCK) and
    /// the descriptor options it switches on.</summary>
    public static async Task<List<(string Id, string Set, List<string> Descriptors)>> GetPartGroupsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new List<(string, string, List<string>)>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.NameEnglish, i.UsageCategory, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'PartGroups'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    results.Add((reader.GetString(0), reader.GetString(1),
                        System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new()));
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetPartGroupsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>The game's 66 base colour palettes in file order (catalog
    /// "Palettes"); empty on game data built before they were collected.</summary>
    public static async Task<List<ArtifactX.Core.Procedural.ColourPalette>> GetPalettesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var byIndex = new SortedDictionary<int, ArtifactX.Core.Procedural.ColourPalette>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.NameEnglish, i.UsageCategory, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'Palettes'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    byIndex[int.Parse(reader.GetString(0), CultureInfo.InvariantCulture)] = new(
                        System.Text.Json.JsonSerializer.Deserialize<float[][]>(reader.GetString(2)) ?? Array.Empty<float[]>(),
                        reader.GetString(1));
                return byIndex.Values.ToList();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetPalettesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Every starship part tree (catalog "ShipDescriptors"), by
    /// lower-case descriptor path, as stored JSON (ShipParts.ParseTree).</summary>
    public static async Task<Dictionary<string, string>> GetShipPartTreesAsync()
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
                    WHERE c.TemplateType = 'ShipDescriptors'";
                using var reader = command.ExecuteReader();
                while (reader.Read()) results[reader.GetString(0)] = reader.GetString(1);
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetShipPartTreesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Freighter Recolouring blueprints (catalog category
    /// "FreighterPaints", DataCataloger add-freighter-paints): blueprint id,
    /// in-game name, and the paint colour as "#RRGGBB".</summary>
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
    /// Category).</summary>
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

    /// <summary>Organic frigate feeding rules (catalog row FRIGATEFEEDING, format
    /// 66), or null for an older catalog.</summary>
    public static Task<FrigateFeedingRules?> GetFrigateFeedingRulesAsync() => Task.Run(() =>
    {
        if (ResolveDbPath() is not string dbPath) return null;
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId = 'FRIGATEFEEDING'";
            return command.ExecuteScalar() is string json
                && System.Text.Json.JsonSerializer.Deserialize<FrigateFeedingRules>(json) is { Foods: not null, Traits: not null } rules
                ? rules : null;
        }
        catch (Exception ex) when (ex is SqliteException or System.Text.Json.JsonException)
        {
            Debug.WriteLine($"[CatalogService] Frigate feeding rules unavailable: {ex.Message}");
            return null;
        }
    });

    /// <summary>Ship Technology/Cargo max-slot counts per (ship type, class letter),
    /// sourced from the ShipCapacity category.</summary>
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
    /// from the MultiToolCapacity category.</summary>
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

    /// <summary>
    /// Word lists for procedural starship and multi-tool naming from nms_catalog.sqlite
    /// (category MultiToolNameWords). Cached after first read.
    /// </summary>
    public static async Task<Dictionary<string, IReadOnlyList<string>>> GetProceduralNameWordsAsync()
    {
        if (_namingWords is not null) return _namingWords;
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new(StringComparer.OrdinalIgnoreCase);

        return await Task.Run(() =>
        {
            if (_namingWords is not null) return _namingWords;
            var dict = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var con = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT i.NameEnglish, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType='MultiToolNameWords'";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    string json = reader.GetString(1);
                    var words = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (words is not null) dict[name] = words;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CatalogService] GetProceduralNameWordsAsync failed: {ex.Message}");
            }
            _namingWords = dict;
            return dict;
        });
    }
}
