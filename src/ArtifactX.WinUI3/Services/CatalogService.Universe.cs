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
    private static Task<(string[][] Files, Dictionary<string, List<ArtifactX.Core.Procedural.ColourPalette>> Palettes)?>? _biomePalettes;

    /// <summary>The colour palettes a planet of this biome and sub-type is painted from: its biome file's
    /// ColourPaletteFile (catalog "BiomePalettes", format 91), the sub-type's own file or else the biome's first, as
    /// the biome file itself is picked. Null on older game data or a biome without one - callers use the base palettes.</summary>
    public static async Task<List<ArtifactX.Core.Procedural.ColourPalette>?> GetBiomePalettesAsync(int biome, int subType)
    {
        if (await (_biomePalettes ??= Task.Run(LoadBiomePalettes)) is not { } all) return null;
        if (biome < 0 || biome >= all.Files.Length) return null;
        var bySub = all.Files[biome];
        string path = subType >= 0 && subType < bySub.Length && bySub[subType].Length > 0 ? bySub[subType]
            : bySub.FirstOrDefault(p => p.Length > 0) ?? "";
        return all.Palettes.GetValueOrDefault(path);
    }

    private static (string[][] Files, Dictionary<string, List<ArtifactX.Core.Procedural.ColourPalette>> Palettes)? LoadBiomePalettes()
    {
        if (ResolveDbPath() is not { } dbPath) return null;
        try
        {
            string[][]? files = null;
            var palettes = new Dictionary<string, List<ArtifactX.Core.Procedural.ColourPalette>>(StringComparer.OrdinalIgnoreCase);
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT i.GameId, i.NameEnglish, i.DescriptionEnglish FROM Items i
                JOIN Categories c ON c.Id = i.CategoryId WHERE c.TemplateType = 'BiomePalettes'";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(0) == "BIOMEPALETTEFILES")
                    files = System.Text.Json.JsonSerializer.Deserialize<string[][]>(reader.GetString(2));
                else
                    palettes[reader.GetString(1)] = (System.Text.Json.JsonSerializer.Deserialize<List<BiomePaletteRow>>(reader.GetString(2)) ?? new())
                        .Select(p => new ArtifactX.Core.Procedural.ColourPalette(p.c, p.n)).ToList();
            }
            return files is null ? null : (files, palettes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] LoadBiomePalettes failed: {ex.Message}");
            return null;
        }
    }

    private sealed record BiomePaletteRow(string n, float[][] c);

    private static Task<Dictionary<string, List<(string Name, IReadOnlyList<string> Parts)>>?>? _creatureFilters;

    /// <summary>A creature scene's part filters, in file order (catalog "CreatureFilters", format 92): the entries of
    /// its &lt;scene&gt;.FILTER.MBIN, each a name and the parts a creature with that filter can't have
    /// (CreatureTables.ExcludedParts). Empty for a scene without one or on older game data.</summary>
    public static async Task<IReadOnlyList<(string Name, IReadOnlyList<string> Parts)>> GetCreatureFiltersAsync(string scenePath)
    {
        if (await (_creatureFilters ??= Task.Run(LoadCreatureFilters)) is not { } all) return [];
        // The game's own lookup (NMS.exe 184271 FUN_142ddea90): the scene path with SCENE.MBIN made FILTER.MBIN.
        string path = scenePath.ToLowerInvariant().Replace(".scene.mbin", ".filter.mbin", StringComparison.Ordinal);
        return all.TryGetValue(path, out var filters) ? filters : [];
    }

    private static Dictionary<string, List<(string Name, IReadOnlyList<string> Parts)>>? LoadCreatureFilters()
    {
        if (ResolveDbPath() is not { } dbPath) return null;
        try
        {
            var filters = new Dictionary<string, List<(string Name, IReadOnlyList<string> Parts)>>(StringComparer.OrdinalIgnoreCase);
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT i.NameEnglish, i.DescriptionEnglish FROM Items i
                JOIN Categories c ON c.Id = i.CategoryId WHERE c.TemplateType = 'CreatureFilters'";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                filters[reader.GetString(0)] = (System.Text.Json.JsonSerializer.Deserialize<List<CreatureFilterRow>>(reader.GetString(1)) ?? new())
                    .Select(f => (f.n, (IReadOnlyList<string>)f.p)).ToList();
            return filters;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] LoadCreatureFilters failed: {ex.Message}");
            return null;
        }
    }

    private sealed record CreatureFilterRow(string n, List<string> p);

    /// <summary>What planet types are picked from (catalog "PlanetGeneration", step
    /// "Planet generation"); null on game data built before it was collected.</summary>
    public static Task<ArtifactX.Core.Universe.BiomeTables?> GetBiomeTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.BiomeTables>("BIOMETABLES");

    /// <summary>What a planet's resources, weather and sentinels are picked from (same step);
    /// null on game data built before catalog format 16.</summary>
    public static Task<ArtifactX.Core.Universe.PlanetProfileTables?> GetPlanetProfileTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.PlanetProfileTables>("PLANETPROFILE");

    /// <summary>S-Class base stat roll ranges, class spawn probabilities, max inventory slot capacities,
    /// and planetary building placement densities (catalog "PlanetGeneration", row "INVENTORYGENERATION").</summary>
    public static Task<ArtifactX.Core.Universe.InventoryGenerationTables?> GetInventoryGenerationTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.InventoryGenerationTables>("INVENTORYGENERATION");

    /// <summary>Solar layout values, all station scene roots, and verified standard TypeB
    /// WeaponTerminal origins. Other station variants intentionally have no cabinet geometry.</summary>
    public static Task<ArtifactX.Core.Universe.StationEquipmentTables?> GetStationEquipmentTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.StationEquipmentTables>("STATIONEQUIPMENT");

    /// <summary>Each building class's density per level and planet restrictions (which grid sites hold which
    /// building); null on game data built before catalog format 49.</summary>
    public static Task<ArtifactX.Core.Universe.BuildingClassPlacement[]?> GetBuildingPlacementAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.BuildingClassPlacement[]>("BUILDINGPLACEMENT");

    /// <summary>Planet building L-system layouts, scene mesh bounds, weapon-crate transforms, cluster layouts and
    /// record counts (catalog row BUILDINGLAYOUTS); null on game data built before catalog format 74.</summary>
    public static Task<ArtifactX.Core.Universe.BuildingLayoutTables?> GetBuildingLayoutTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.BuildingLayoutTables>("BUILDINGLAYOUTS");

    /// <summary>Each building class's override rules (portal, shelter, base...); null on game data built before
    /// catalog format 48.</summary>
    public static Task<ArtifactX.Core.Universe.BuildingOverrideRule[]?> GetBuildingOverrideRulesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.BuildingOverrideRule[]>("BUILDINGOVERRIDES");

    public static Task<ArtifactX.Core.Universe.MultiToolTechnologyTables?> GetMultiToolTechnologyTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.MultiToolTechnologyTables>("MULTITOOLTECHNOLOGY");

    public static Task<ArtifactX.Core.Universe.StarshipEvaluationTables?> GetStarshipEvaluationTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.StarshipEvaluationTables>("STARSHIPDISPLAY");

    public static Task<ArtifactX.Core.Universe.MultiToolModelTables?> GetMultiToolModelTablesAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.MultiToolModelTables>("MULTITOOLMODEL");

    /// <summary>Region star-generation settings (catalog "PlanetGeneration", row "GALAXYGENERATION");
    /// null on game data built before catalog format 44.</summary>
    public static Task<ArtifactX.Core.Universe.GalaxyGenerationSettings?> GetGalaxyGenerationSettingsAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.GalaxyGenerationSettings>("GALAXYGENERATION");

    /// <summary>Region star-placement settings (row "GALAXYPLACEMENT"); null before catalog format 45; no spiral fields before 46.</summary>
    public static Task<ArtifactX.Core.Universe.GalaxyPlacementSettings?> GetGalaxyPlacementSettingsAsync() =>
        GetPlanetGenerationRowAsync<ArtifactX.Core.Universe.GalaxyPlacementSettings>("GALAXYPLACEMENT");

    private static async Task<T?> GetPlanetGenerationRowAsync<T>(string gameId) where T : class
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
                command.CommandText = @"
                    SELECT i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'PlanetGeneration' AND i.GameId = $id";
                command.Parameters.AddWithValue("$id", gameId);
                return command.ExecuteScalar() is string json ? System.Text.Json.JsonSerializer.Deserialize<T>(json) : null;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] Reading planet generation row {gameId} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>The words the game uses for a system's economy, wealth and
    /// conflict (catalog category "SystemDescriptors").</summary>
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

    /// <summary>The words the game's discovery screen uses for a planet's
    /// weather, sentinels and flora/fauna (catalog category "PlanetDescriptors").</summary>
    public static async Task<List<(string Word, string Group, string Tier)>> GetPlanetDescriptorsAsync()
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
                    WHERE c.TemplateType = 'PlanetDescriptors'
                    ORDER BY i.Id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    results.Add((reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetPlanetDescriptorsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Names of every raw resource (substance) - what a planet's
    /// discovery screen lists under resources - A to Z, for the planet
    /// details picker.</summary>
    public static async Task<List<string>> GetSubstanceNamesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var names = new List<string>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT COALESCE(i.NameLowerEnglish, i.NameEnglish)
                    FROM Items i
                    JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType IN ('GcSubstanceTable', 'GcProductTable') AND i.NameEnglish IS NOT NULL
                      AND (c.TemplateType = 'GcSubstanceTable'
                           OR (i.CatalogGroup = 'Curiosities' AND i.GameId NOT GLOB 'F_*'))
                      AND i.NameEnglish NOT IN ('UNITS', 'QUICKSILVER', 'NANITES', 'FEATURE', 'OUTLAWS')
                    ORDER BY 1";
                using var reader = command.ExecuteReader();
                while (reader.Read()) names.Add(reader.GetString(0));
                return names;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetSubstanceNamesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Settlement Perks ("buffs" shown in-game under "Settlement
    /// Features"), sourced from the SettlementPerks category.</summary>
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

    private static readonly Dictionary<string, string> PerkTypeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SENT"] = "Sentinel",
        ["POP"] = "Population",
        ["PROD"] = "Production",
        ["JOBLEVEL"] = "Job",
        ["GREEK"] = "Building",
    };

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

    private static string StripPerkPlaceholders(string description)
    {
        if (!description.Contains('%')) return description;
        string stripped = PerkPlaceholderRegex.Replace(description, "").Trim();
        if (stripped.StartsWith('(') && stripped.EndsWith(')'))
            stripped = stripped[1..^1];
        return stripped;
    }

    /// <summary>What each settlement building stage needs (catalog
    /// "SettlementCosts", CatalogSteps.AddSettlementCosts).</summary>
    public static async Task<List<SettlementCost>> GetSettlementCostsAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new List<SettlementCost>();
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.UsageCategory, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'SettlementCosts'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(1)) continue;
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(1));
                    var root = json.RootElement;
                    results.Add(new SettlementCost(
                        reader.GetString(0),
                        root.GetProperty("stage").GetInt32(),
                        root.GetProperty("stageName").GetString() ?? "",
                        root.GetProperty("item").GetString() ?? "",
                        root.GetProperty("amountMin").GetInt32(),
                        root.GetProperty("amountMax").GetInt32()));
                }
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetSettlementCostsAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Settlement building names by class (catalog "SettlementBuildingNames").</summary>
    public static async Task<Dictionary<string, (string Name, List<string> Names, List<string> Upgraded)>> GetSettlementBuildingNamesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();
        try
        {
            return await Task.Run(() =>
            {
                var results = new Dictionary<string, (string, List<string>, List<string>)>(StringComparer.OrdinalIgnoreCase);
                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.UsageCategory, i.NameEnglish, i.DescriptionEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'SettlementBuildingNames'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    using var json = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
                    List<string> List(string key) => json.RootElement.GetProperty(key).EnumerateArray().Select(n => n.GetString() ?? "").ToList();
                    results[reader.GetString(0)] = (reader.GetString(1), List("names"), List("upgraded"));
                }
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetSettlementBuildingNamesAsync failed: {ex.Message}");
            return new();
        }
    }

    /// <summary>Trade category each economy sells, keyed by the game's trading
    /// class - catalog "TradeClasses".</summary>
    public static async Task<Dictionary<string, string>> GetEconomySellsAsync()
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
                    SELECT i.GameId, i.NameEnglish
                    FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                    WHERE c.TemplateType = 'TradeClasses'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    results[reader.GetString(0)["TRADECLASS:".Length..]] = reader.GetString(1);
                return results;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetEconomySellsAsync failed: {ex.Message}");
            return new();
        }
    }
}

/// <summary>One settlement building stage's cost: BuildingClass is the
/// game's id (Settlement_Market...), Stage 1-7 (GcSettlementConstructionLevel:
/// GroundStorey .. Upgrade3), Item a catalog GameId.</summary>
public sealed record SettlementCost(string BuildingClass, int Stage, string StageName, string Item, int AmountMin, int AmountMax);
