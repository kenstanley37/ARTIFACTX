using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Helpers.Recipes;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Where to go for a wild ingredient, creature produce or fish (the Ingredient Finder, catalog format 93): the systems nearest
/// a starting system whose planets give it, from the game's own generators (Core NearbySystems, HarvestSearch and
/// CreatureProduceSearch, FishSearch, which the mobile app can run from a typed or scanned address). The start's region is searched first, then the regions around it only when
/// it holds too few. Distances inside the start's region are the galaxy map's light-years (RegionStarPositions, checked
/// on the map 2026-10-27); farther systems read "1 region away".
/// </summary>
public static class IngredientLocatorService
{
    /// <summary>What the search looks for before it stops widening.</summary>
    public const int Wanted = 5;

    /// <summary>The kinds of place that give <paramref name="itemId"/> (empty when no wild plant or object does, or
    /// before catalog format 93).</summary>
    public static async Task<HarvestRule[]> RulesAsync(string itemId)
    {
        await PlanetTypesService.EnsureLoadedAsync();
        return PlanetTypesService.ProfileTables?.Objects is { } objects ? HarvestSearch.Rules(itemId, objects) : [];
    }

    /// <summary>Whether creatures give <paramref name="itemId"/> when fed (false before catalog format 80) and when
    /// hunted.</summary>
    public static async Task<(bool Fed, bool Hunted)> CreatureWaysAsync(string itemId)
    {
        await PlanetTypesService.EnsureLoadedAsync();
        return (CreatureProduceSearch.IsProduce(itemId, PlanetTypesService.ProfileTables?.Creatures), (await HuntIdsAsync(itemId)).Count > 0);
    }

    /// <summary>The fish <paramref name="itemId"/> is, when it can be caught outside a mission (catalog "Fish").</summary>
    public static async Task<FishInfo?> FishAsync(string itemId) =>
        (await CatalogService.GetFishAsync()).FirstOrDefault(f => !f.NeedsMission && string.Equals(f.Id, itemId, StringComparison.OrdinalIgnoreCase));

    /// <summary>"Craft it from 50 Carbon" for an item the player crafts (catalog "Crafting"); null for anything
    /// else.</summary>
    public static async Task<string?> CraftLineAsync(string itemId)
    {
        if (!(await CatalogService.GetCraftingAsync()).TryGetValue(itemId, out var craft) || craft.Kind != "Product" || craft.Requirements.Count == 0)
            return null;
        var names = await ItemNamesAsync();
        return "Craft it from " + string.Join(" and ", craft.Requirements.Select(r => $"{r.Amount:N0} {names(r.Id)}")) + ".";
    }

    /// <summary>An item's name from the catalog ("Gutrot Flower"), else its id.</summary>
    public static async Task<Func<string, string>> ItemNamesAsync()
    {
        var names = await CatalogService.GetItemNamesAsync();
        return id => names.TryGetValue(id, out var n) && !string.IsNullOrEmpty(n) ? n : id;
    }

    private static async Task<HashSet<string>> HuntIdsAsync(string itemId) => CreatureProduceSearch.HuntIds(itemId,
        (await RecipeIngredientSourcesService.CreatureSourcesAsync()).Where(s => s.Method == "Hunt").Select(s => (s.ItemId, s.CreatureId)));

    /// <summary>The systems nearest <paramref name="start"/> that give <paramref name="itemId"/>, nearest first (the
    /// start itself first when it does): Core IngredientSearch with the catalog's tables, creature names and the
    /// start region's stars. Off the UI thread, in parallel; a newer search cancels through <paramref name="cancel"/>.</summary>
    public static async Task<IReadOnlyList<IngredientPlace>> FindAsync(ulong start, string itemId, CancellationToken cancel = default)
    {
        await PlanetTypesService.EnsureLoadedAsync();
        if (await CatalogService.GetGalaxyGenerationSettingsAsync() is not { } settings
            || PlanetTypesService.BiomeTables is not { } biomes || PlanetTypesService.ProfileTables is not { } tables)
            return [];
        var huntIds = await HuntIdsAsync(itemId);
        var fish = await FishAsync(itemId);
        bool creatures = CreatureProduceSearch.IsProduce(itemId, tables.Creatures) || huntIds.Count > 0;
        var names = creatures ? await CreatureNameService.LoadAsync() : null;
        var stars = await RegionSystemsService.GetStarsAsync(start);
        return await Task.Run(() => IngredientSearch.Find(start, itemId, settings, biomes, tables,
            fish is null ? null : new FishHabitat(fish.Biomes, fish.NeedsStorm), huntIds,
            names is null ? null : names.PredictedSpecies, stars, cancel), cancel);
    }
}
