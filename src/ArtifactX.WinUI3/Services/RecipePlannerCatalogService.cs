using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

internal interface IRecipePlannerCatalog
{
    Task<RecipePlannerCatalog> LoadAsync(CancellationToken token);
}

internal sealed class RecipePlannerCatalogService : IRecipePlannerCatalog
{
    public async Task<RecipePlannerCatalog> LoadAsync(CancellationToken token)
    {
        var recipesTask = CatalogService.GetRecipesAsync();
        var namesTask = CatalogService.GetItemNamesAsync();
        var foodTask = Task.Run(() =>
        {
            if (CatalogService.ResolveDbPath() is not string path) return null;
            using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='FOODINGESTOR'";
            return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<FoodIngestorTables>(json) : null;
        }, token);
        var recipes = await recipesTask;
        var names = await namesTask;
        var sources = await RecipeIngredientSourcesService.LoadAsync(
            recipes.SelectMany(r => r.Ingredients.Select(i => i.Id).Append(r.Result.Id)), token);
        return new(recipes, names, sources, await foodTask);
    }

    /// <summary>Read-only exosuit cargo totals. Uses the confirmed occupied-slot
    /// b2n/1o9 fields from NmsInventorySlot; does not claim remote storage is accessible.</summary>
    public static Dictionary<string, long> ReadOwned(JObject? liveCargo = null)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if ((liveCargo ?? SaveSessionManager.GetValue(NmsInventoryContainer.ExosuitCargoPath)) is not JObject cargo) return result;
        foreach (var slot in (cargo[":No"] as JArray ?? new()).OfType<JObject>())
            if (slot["b2n"]?.Value<string>() is string id && (slot["1o9"]?.Value<int>() ?? 0) > 0)
            {
                id = CatalogService.NormalizeId(id);
                result[id] = result.GetValueOrDefault(id) + slot["1o9"]!.Value<int>();
            }
        return result;
    }
}
