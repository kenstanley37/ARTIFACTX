using ArtifactX.Core.Inventory;
using ArtifactX.Core.NmsModels;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The game's stack sizes for the loaded save (Bug 78): the catalog's stack-limit tables (CatalogSteps.AddStackLimits,
/// format 86) with the save's stack-limit setting (NmsDifficultySettings.InventoryStackLimitsPath). Read at start-up
/// and again whenever the game data is rebuilt; null sizes (older catalogs, unknown groups) leave each page's own
/// fixed sizes in place.
/// </summary>
public static class InventoryStackLimitService
{
    private static InventoryStackLimitTables? _tables;

    public static void Start()
    {
        CatalogService.Reloaded += () => _ = LoadAsync();
        _ = LoadAsync();
    }

    /// <summary>The product and substance stack sizes of an inventory's stack group (its WA4.rri) under the loaded
    /// save's setting; null when unknown (see Core InventoryStackLimits.Sizes).</summary>
    public static (int Product, int ProductLimit, int? Substance)? SizesFor(string? stackGroup)
    {
        if (_tables is not { } tables) return null;
        string setting = SaveSessionManager.GetValue(NmsDifficultySettings.InventoryStackLimitsPath)?.Value<string>() ?? "High";
        return InventoryStackLimits.Sizes(tables, stackGroup, setting);
    }

    /// <summary>A full stack of an item in an inventory of <paramref name="stackGroup"/>: the game's sizes when known,
    /// else <paramref name="fallbackProductMultiplier"/> (the page's fixed size) and 9999 for substances.</summary>
    public static int CapFor(string? categoryLabel, int? maxStackSize, string? stackGroup, int fallbackProductMultiplier)
    {
        var sizes = SizesFor(stackGroup);
        return Helpers.Inventory.StackCaps.For(categoryLabel, maxStackSize, sizes?.Product ?? fallbackProductMultiplier,
            sizes?.ProductLimit ?? 9999, sizes?.Substance ?? 9999);
    }

    private static async Task LoadAsync() => _tables = await Task.Run(Read);

    private static InventoryStackLimitTables? Read()
    {
        try
        {
            if (CatalogService.ResolveDbPath() is not string dbPath) return null;
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId = 'INVENTORYSTACKLIMITS'";
            return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<InventoryStackLimitTables>(json) : null;
        }
        catch (Exception ex) when (ex is SqliteException or JsonException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"[InventoryStackLimitService] {ex.Message}");
            return null;
        }
    }
}
