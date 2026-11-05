using ArtifactX.Core.NmsModels;
using Microsoft.Data.Sqlite;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Hands Core the construction limit the player's own game uses (#21): the catalog reads it
/// from NMS.exe (CatalogSteps.AddConstructionLimit, format 73). Read at start-up and again
/// whenever the game data is rebuilt; until then, and on older catalogs, the measured 21,000.
/// </summary>
public static class ConstructionLimitService
{
    public static event Action? Changed;

    public static void Start()
    {
        CatalogService.Reloaded += () => _ = LoadAsync();
        _ = LoadAsync();
    }

    private static async Task LoadAsync()
    {
        int? limit = await Task.Run(Read);
        NmsBuildCounts.UseGameLimit(limit);
        Changed?.Invoke();
    }

    private static int? Read()
    {
        try
        {
            if (CatalogService.ResolveDbPath() is not string dbPath) return null;
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId = 'CONSTRUCTIONLIMIT'";
            if (command.ExecuteScalar() is not string json) return null;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("GlobalPartLimit", out var cap) ? cap.GetInt32() : null;
        }
        catch (Exception ex) when (ex is SqliteException or JsonException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"[ConstructionLimitService] {ex.Message}");
            return null;
        }
    }
}
