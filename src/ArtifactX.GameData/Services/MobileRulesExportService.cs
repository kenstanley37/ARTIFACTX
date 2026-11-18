using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

namespace ArtifactX.GameData.Services;

/// <summary>
/// Writes the mobile app's rules file (<see cref="GameRules"/>) from a catalog built from the player's install: every
/// "PlanetGeneration" row as the catalog stores it, and the products' and substances' names. Numbers and text only -
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

        if (rows.Count == 0) throw new InvalidDataException("The catalog has no generation tables. Update the game data first.");
        return new GameRules
        {
            GameBuild = $"{info.ExeVersion}/{info.Fingerprint}",
            GameBuildLabel = info.ExeVersion is { Length: > 0 } v ? $"build {v}" : info.SteamBuildId is { Length: > 0 } s ? $"Steam build {s}" : "unknown build",
            CatalogFormat = info.Format,
            ExportedUtc = DateTime.UtcNow,
            Rows = rows,
            ItemNames = names,
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
