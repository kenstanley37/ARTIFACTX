using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
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
        var dataTask = Task.Run<(FoodIngestorTables? Food, ExtractionWorldTables? Extraction)>(() =>
        {
            if (CatalogService.ResolveDbPath() is not string path) return (null, null);
            using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
            db.Open();
            using var command = db.CreateCommand();
            T? Read<T>(string id) where T : class
            {
                token.ThrowIfCancellationRequested();
                command.Parameters.Clear();
                command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
                command.Parameters.AddWithValue("$id", id);
                return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<T>(json) : null;
            }
            var food = Read<FoodIngestorTables>("FOODINGESTOR");
            ExtractionWorldTables? extraction = null;
            if (Read<ExtractionTables>("EXTRACTIONTABLES") is { } settings
                && Read<BiomeTables>("BIOMETABLES") is { } biomes
                && Read<PlanetProfileTables>("PLANETPROFILE") is { } profiles)
                extraction = new(biomes, profiles, settings);
            return (food, extraction);
        }, token);
        var recipes = await recipesTask;
        var names = await namesTask;
        var sources = await RecipeIngredientSourcesService.LoadAsync(
            recipes.SelectMany(r => r.Ingredients.Select(i => i.Id).Append(r.Result.Id)), token);
        var data = await dataTask;
        return new(recipes, names, sources, data.Food, data.Extraction);
    }

}
