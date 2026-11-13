using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A star system that has an item, for the "Closest to you" list: its name, how far it is in words, and its
/// bodies that give the item ("Frozen planet Okiru II").</summary>
public sealed record IngredientPlace(ulong SystemAddress, string SystemName, string Distance, IReadOnlyList<string> Bodies,
    bool IsHere, double SortKey)
{
    public string BodiesText => string.Join("\n", Bodies);
    public string AccessibleName => $"{SystemName}, {Distance}: {string.Join(", ", Bodies)}";
}

/// <summary>
/// Where to go for a wild ingredient or creature produce (the Ingredient Finder, catalog format 93): the systems nearest
/// a starting system whose planets give it, from the game's own generators (Core NearbySystems, HarvestSearch and
/// CreatureProduceSearch, which the mobile app can run from a typed or scanned address). The start's region is searched first, then the regions around it only when
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

    /// <summary>Whether creatures produce <paramref name="itemId"/> when fed (false before catalog format 80).</summary>
    public static async Task<bool> IsProduceAsync(string itemId)
    {
        await PlanetTypesService.EnsureLoadedAsync();
        return CreatureProduceSearch.IsProduce(itemId, PlanetTypesService.ProfileTables?.Creatures);
    }

    /// <summary>The systems nearest <paramref name="start"/> that give <paramref name="itemId"/>, nearest first (the
    /// start itself first when it does). Off the UI thread, in parallel; a newer search cancels through
    /// <paramref name="cancel"/>.</summary>
    public static async Task<IReadOnlyList<IngredientPlace>> FindAsync(ulong start, string itemId, CancellationToken cancel = default)
    {
        await PlanetTypesService.EnsureLoadedAsync();
        if (await CatalogService.GetGalaxyGenerationSettingsAsync() is not { } settings
            || PlanetTypesService.BiomeTables is not { } biomes || PlanetTypesService.ProfileTables is not { } tables)
            return [];
        var creatures = CreatureProduceSearch.IsProduce(itemId, tables.Creatures) ? tables.Creatures : null;
        bool plants = tables.Objects?.Harvest is not null;
        if (creatures is null && !plants) return [];
        var names = creatures is null ? null : await CreatureNameService.LoadAsync();
        var stars = await RegionSystemsService.GetStarsAsync(start);
        ulong startSystem = start & ~(0xFUL << 52);
        int startIndex = (int)((start >> 40) & 0xFFF);
        var here = stars?.FirstOrDefault(s => s.System == startIndex);

        return await Task.Run(() =>
        {
            var found = new ConcurrentBag<IngredientPlace>();
            foreach (int radius in new[] { 0, 1 })
            {
                var systems = NearbySystems.Around(start, radius, settings)
                    .Where(s => radius == 0 || s.RegionsAway > 0).ToList();
                Parallel.ForEach(systems, new ParallelOptions { CancellationToken = cancel }, system =>
                {
                    var (portal, galaxy) = SystemProfiles.PortalOf(system.SystemAddress);
                    PlanetType[] types;
                    try { types = PlanetGenerator.Generate(portal, galaxy, biomes); }
                    catch { return; }
                    var profiles = SystemProfiles.Generate(system.SystemAddress, biomes, tables);
                    var bodies = new SortedDictionary<int, string>();
                    if (plants)
                        foreach (var f in HarvestSearch.On(system.SystemAddress, types, profiles, itemId))
                            bodies[f.PlanetIndex] = Body(portal, galaxy, f.PlanetIndex, f.Body)
                                + f.Harvest.TileType switch { 4 => " (underwater)", 5 => " (in caves)", _ => "" };
                    if (creatures is not null)
                        foreach (var f in CreatureProduceSearch.On(system.SystemAddress, types, profiles, itemId, creatures))
                            bodies[f.PlanetIndex] = (bodies.TryGetValue(f.PlanetIndex, out var b) ? b : Body(portal, galaxy, f.PlanetIndex, f.Body))
                                + ": " + Feed(f, names);
                    if (bodies.Count > 0) found.Add(Place(system, [.. bodies.Values], startSystem, startIndex, stars, here));
                });
                if (found.Count(p => !p.IsHere) >= Wanted) break;
            }
            return (IReadOnlyList<IngredientPlace>)found.OrderBy(p => p.SortKey).Take(Wanted + 1).ToList();
        }, cancel);
    }

    /// <summary>"Frozen planet Okiru II".</summary>
    private static string Body(ulong portal, int galaxy, int planetIndex, PlanetType type) =>
        $"{NmsPlanets.BiomeGroup(type.Biome)} {(SystemGenerator.Bodies(portal, galaxy).IsMoon(planetIndex) ? "moon" : "planet")} "
        + SystemGenerator.PlanetName(portal | ((ulong)planetIndex << 44), galaxy);

    /// <summary>"feed the Kordiussia" (the species name the game gives once scanned), "feed the Kordiussia or the
    /// Vrandeis", "... or 2 more"; "feed its creatures" when the names are unknown.</summary>
    private static string Feed(ProduceFind find, CreatureNameService? names)
    {
        var species = find.Creatures.Select(c => names?.PredictedSpecies(c, find.PlanetAddress)).OfType<string>().Distinct().ToList();
        if (species.Count == 0) return "feed its creatures";
        string shown = string.Join(" or ", species.Take(2).Select(n => "the " + n));
        return species.Count > 2 ? $"feed {shown} or {species.Count - 2} more" : $"feed {shown}";
    }

    private static IngredientPlace Place(NearbySystem system, List<string> bodies, ulong startSystem, int startIndex,
        IReadOnlyList<RegionStar>? stars, RegionStar? here)
    {
        var (portal, galaxy) = SystemProfiles.PortalOf(system.SystemAddress);
        int index = (int)((system.SystemAddress >> 40) & 0xFFF);
        bool isHere = (system.SystemAddress & ~(0xFUL << 52)) == startSystem;
        string distance;
        double sort;
        if (isHere) { distance = "You're here"; sort = -1; }
        else if (system.RegionsAway == 0 && here is { } h && stars?.FirstOrDefault(s => s.System == index) is { System: > 0 } star)
        {
            int ly = RegionStarPositions.LightYears(star, h);
            distance = $"{ly:N0} ly";
            sort = ly;
        }
        else
        {
            distance = system.RegionsAway == 0 ? "This region" : system.RegionsAway == 1 ? "1 region away" : $"{system.RegionsAway} regions away";
            sort = system.RegionsAway * 100_000 + index;
        }
        return new IngredientPlace(system.SystemAddress, SystemGenerator.SystemName(portal, galaxy), distance, bodies, isHere, sort);
    }
}
