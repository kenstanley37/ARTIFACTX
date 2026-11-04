using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ArtifactX.Core.Unlocks;
using Microsoft.Data.Sqlite;

namespace ArtifactX.WinUI3.Services;

/// <summary>Reads the catalog's product-type table (CatalogSteps.AddProductTypes)
/// off the UI thread, keyed by item id. Empty for a catalog built before format 61,
/// so callers keep their old grouping.</summary>
internal static class ProductTypesService
{
    public static Task<Dictionary<string, ProductTypeEntry>> LoadAsync() => Task.Run(() =>
    {
        var result = new Dictionary<string, ProductTypeEntry>(StringComparer.OrdinalIgnoreCase);
        if (CatalogService.ResolveDbPath() is not string path) return result;
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();
        using var query = db.CreateCommand();
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='PRODUCTTYPES'";
        if (query.ExecuteScalar() is not string text) return result;
        foreach (var entry in JsonSerializer.Deserialize<ProductTypeEntry[]>(text) ?? [])
            result.TryAdd(entry.ItemId, entry);
        return result;
    });
}
