using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A planet's crashed ships of one class nearest a point, out of <see cref="Total"/> of that class on the
/// whole planet. <see cref="Uncharted"/>: a system with no race, where the only claimable wreck is the planet's one
/// fixed underwater site (none on a planet without one).</summary>
public sealed record CrashedShipsOfClass(IReadOnlyList<CrashedShipSite> Nearest, int Total, bool Uncharted);

/// <summary>
/// The desktop's crashed ships: Core <see cref="CrashedShipSites"/> (the rules, shared with the phone) fed from the
/// catalog, cached per planet, with the ships' displayed stats added (CrashedShipDisplayStatsService).
/// </summary>
public static class CrashedShipService
{
    /// <summary>Every crashed ship of a planet, worked out once per planet (CrashedShipSites.All, off the UI thread).</summary>
    private static readonly ConcurrentDictionary<ulong, Task<AllOnPlanet?>> All = new();

    static CrashedShipService() => CatalogService.Reloaded += () => All.Clear();

    private sealed record AllOnPlanet(IReadOnlyList<CrashedShipSite> Sites, float Radius, bool Uncharted);

    /// <summary>The <paramref name="count"/> crashed-ship sites nearest a point on the planet; empty when the catalog
    /// doesn't have the tables yet or the planet can't be generated.</summary>
    public static async Task<IReadOnlyList<CrashedShipSite>> NearAsync(ulong planetAddress, double latitude, double longitude, int count = 5)
    {
        if (await ContextAsync(planetAddress) is not { } context) return [];
        var sites = await Task.Run(() => CrashedShipSites.Near(context, latitude, longitude, count));
        return await CrashedShipDisplayStatsService.ApplyAsync(sites);
    }

    /// <summary>The planet's crashed ships of class <paramref name="shipClass"/> (C=0 .. S=3; Exotics count as S)
    /// nearest a point (you, or the portal), and how many of that class the whole planet has; null when the planet
    /// can't be worked out yet.</summary>
    public static Task<CrashedShipsOfClass?> OfClassAsync(ulong planetAddress, int shipClass, double latitude, double longitude, int count = 1) =>
        SearchAsync(planetAddress, s => s.ShownClass == shipClass, latitude, longitude, count);

    /// <summary>The planet's crashed ships that <paramref name="match"/> accepts, nearest a point first, and how many
    /// the whole planet has; null when the planet can't be worked out yet.</summary>
    public static async Task<CrashedShipsOfClass?> SearchAsync(ulong planetAddress, Func<CrashedShipSite, bool> match,
        double latitude, double longitude, int count)
    {
        var task = All.GetOrAdd(planetAddress, AllOnPlanetAsync);
        if (await task is not { } found)
        {
            All.TryRemove(new KeyValuePair<ulong, Task<AllOnPlanet?>>(planetAddress, task));   // retry once the catalog is ready
            return null;
        }
        return await Task.Run(async () =>
        {
            var dir = Direction(latitude, longitude);
            var ofClass = found.Sites.Where(match).ToList();
            var nearest = ofClass
                .Select(s => s with { DistanceKm = CrashedShipSites.Angle(s, dir) * found.Radius / 1000 })
                .OrderBy(s => s.DistanceKm).Take(count).ToList();
            return new CrashedShipsOfClass(await CrashedShipDisplayStatsService.ApplyAsync(nearest), ofClass.Count, found.Uncharted);
        });
    }

    /// <summary>How many of the planet's crashed ships that <paramref name="match"/> accepts fall in each group of
    /// <paramref name="group"/> (largest first); empty when the planet can't be worked out yet.</summary>
    public static async Task<IReadOnlyList<(string Group, int Count)>> CountAsync(ulong planetAddress,
        Func<CrashedShipSite, bool> match, Func<CrashedShipSite, string> group)
    {
        if (await All.GetOrAdd(planetAddress, AllOnPlanetAsync) is not { } found) return [];
        return await Task.Run(() => found.Sites.Where(match).GroupBy(group).Select(g => (g.Key, g.Count()))
            .OrderByDescending(g => g.Item2).ToList());
    }

    private static async Task<AllOnPlanet?> AllOnPlanetAsync(ulong planetAddress)
    {
        if (await ContextAsync(planetAddress) is not { Pool: not null } context) return null;
        return await Task.Run(() => new AllOnPlanet(CrashedShipSites.All(context), context.Radius, context.Uncharted));
    }

    /// <summary>The planet's building grid, terrain and system facts (also used by BuildingSiteService).</summary>
    internal static async Task<PlanetSiteContext?> ContextAsync(ulong planetAddress)
    {
        var biomes = await CatalogService.GetBiomeTablesAsync();
        var profiles = await CatalogService.GetPlanetProfileTablesAsync();
        var placement = await CatalogService.GetBuildingPlacementAsync();
        var overrides = await CatalogService.GetBuildingOverrideRulesAsync();
        var solar = (await CatalogService.GetStationEquipmentTablesAsync())?.SolarLayout;
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var inventory = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();
        var partTreeJson = await CatalogService.GetShipPartTreesAsync();
        var centre = await PlanetCentreService.GetAsync(planetAddress);
        if (biomes is null || profiles is null || placement is null || centre is not { } c) return null;
        return await Task.Run(() => CrashedShipSites.Prepare(planetAddress, c, biomes, profiles, placement, overrides, solar,
            k => words.GetValueOrDefault(k), inventory, path => partTreeJson.TryGetValue(path, out var json) ? json : null));
    }

    internal static Vector3 Direction(double latitude, double longitude) => CrashedShipSites.Direction(latitude, longitude);
}
