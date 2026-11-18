using System.Text.Json;
using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

namespace ArtifactX.GameData.Services;

/// <summary>
/// Writes the mobile app's rules file (<see cref="GameRules"/>) from a catalog built from the player's install: every
/// "PlanetGeneration" row as the catalog stores it, the Egg Sequencer row, every recipe, and the products' and
/// substances' names and base values. Numbers and text only -
/// no icons or other assets (docs/AGENT_HANDOFF_NOTES.md "Mobile app constraints"). Build-stamped and kept out of git,
/// like the portal glyph model; rebuilt each NMS update.
/// </summary>
public static class MobileRulesExportService
{
    /// <summary>The catalog category holding the generators' tables (CatalogSteps.Planets and friends).</summary>
    private const string RulesCategory = "PlanetGeneration";

    public static GameRules Read(string catalogPath)
    {
        var info = GameDataInfo.Read(catalogPath) ?? throw new InvalidDataException("The catalog has no game build stamp.");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();

        var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT i.GameId, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE c.TemplateType = $category AND i.DescriptionEnglish IS NOT NULL ORDER BY i.GameId
                """;
            command.Parameters.AddWithValue("$category", RulesCategory);
            using var reader = command.ExecuteReader();
            while (reader.Read()) rows[reader.GetString(0)] = reader.GetString(1);
        }

        // The same names the desktop shows (CatalogService.GetItemNamesAsync).
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT i.GameId, COALESCE(i.NameLowerEnglish, i.NameEnglish) FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE c.TemplateType IN ('GcProductTable', 'GcSubstanceTable') AND COALESCE(i.NameLowerEnglish, i.NameEnglish) IS NOT NULL
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read()) names.TryAdd(reader.GetString(0), reader.GetString(1));
        }

        // The Egg Sequencer's rules row, as the desktop reads it (CatalogService.GetEggSequencerRulesAsync).
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId = 'EGGSEQUENCER'";
            if (command.ExecuteScalar() is string json) rows["EGGSEQUENCER"] = json;
        }

        // Recipes and item values, read as the desktop reads them (CatalogService.GetRecipesAsync / GetItemValuesAsync).
        var recipes = new List<RequirementRecipe>();
        var values = new Dictionary<string, ItemBaseValue>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT c.TemplateType, i.GameId, i.UsageCategory, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE c.TemplateType IN ('Recipes', 'ItemValues') AND i.DescriptionEnglish IS NOT NULL ORDER BY i.Id
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                using var json = JsonDocument.Parse(reader.GetString(3));
                var root = json.RootElement;
                if (reader.GetString(0) == "Recipes")
                {
                    static RequirementIngredient Pair(JsonElement e) => new(e[0].GetString() ?? "", e[1].GetInt32());
                    var result = Pair(root.GetProperty("result"));
                    recipes.Add(new RequirementRecipe(reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2), result.Id,
                        result.Amount, root.GetProperty("ingredients").EnumerateArray().Select(Pair).ToArray()));
                }
                else if (root.GetProperty("kind").GetString() is "Product" or "Substance" && root.GetProperty("id").GetString() is { Length: > 0 } id)
                    values[id] = new ItemBaseValue(root.GetProperty("value").GetInt64(), root.GetProperty("kind").GetString() == "Substance");
            }
        }

        // Multi-tool name words, as the desktop reads them (CatalogService.GetProceduralNameWordsAsync).
        var words = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT i.NameEnglish, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE c.TemplateType = 'MultiToolNameWords' AND i.DescriptionEnglish IS NOT NULL
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
                if (JsonSerializer.Deserialize<List<string>>(reader.GetString(1)) is { } list) words[reader.GetString(0)] = list;
        }

        // Fish caught outside missions and where they live, as the desktop reads them (CatalogService.GetFishAsync).
        var fish = new Dictionary<string, FishHabitat>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE c.TemplateType = 'Fish' AND i.DescriptionEnglish IS NOT NULL
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                using var json = JsonDocument.Parse(reader.GetString(0));
                var root = json.RootElement;
                if (root.GetProperty("mission").GetBoolean() || root.GetProperty("id").GetString() is not { Length: > 0 } id) continue;
                fish[id] = new FishHabitat(root.GetProperty("biomes").EnumerateArray().Select(b => b.GetInt32()).ToArray(),
                    root.GetProperty("storm").GetBoolean());
            }
        }

        if (rows.Count == 0) throw new InvalidDataException("The catalog has no generation tables. Update the game data first.");
        return new GameRules
        {
            GameBuild = $"{info.ExeVersion}/{info.Fingerprint}",
            GameBuildLabel = info.ExeVersion is { Length: > 0 } v ? $"build {v}" : info.SteamBuildId is { Length: > 0 } s ? $"Steam build {s}" : "unknown build",
            CatalogFormat = info.Format,
            ExportedUtc = DateTime.UtcNow,
            Rows = rows,
            ItemNames = names,
            Recipes = recipes,
            ItemValues = values,
            NameWords = words,
            Fish = fish,
        };
    }

    public static GameRules Export(string catalogPath, string outputPath)
    {
        string destination = Path.GetFullPath(outputPath);
        if (StringComparer.OrdinalIgnoreCase.Equals(destination, Path.GetFullPath(catalogPath)))
            throw new ArgumentException("The output must differ from the catalog.");
        var rules = Read(catalogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using (var file = File.Create(destination)) rules.Write(file);
        return rules;
    }
}
