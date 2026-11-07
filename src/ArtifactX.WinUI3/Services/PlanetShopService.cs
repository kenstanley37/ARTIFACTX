using ArtifactX.Core.Collections;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A planetary shop site (Minor Settlement), its visor coordinates, distance, and visited status.</summary>
public sealed record PlanetShopSite(
    BuildingSiteLocation Site,
    bool IsVisited,
    bool IsCurrentLocation)
{
    public ulong Key => Site.Key;
    public double Latitude => Site.Latitude;
    public double Longitude => Site.Longitude;
    public double DistanceKm => Site.DistanceKm;
}

public sealed record ShopsOfPlanet(
    IReadOnlyList<PlanetShopSite> Nearest,
    int TotalMatching,
    int TotalUnvisited,
    int TotalVisited,
    int TotalOnPlanet);

/// <summary>
/// Discovers and evaluates all planetary shop sites (Minor Settlements, GcBuildingClassification.Shop = 5)
/// on a planet across all 6 cube faces, with exact coordinates matching the Analysis Visor, save markers,
/// and live visited-status detection from save interactions.
/// </summary>
public static class PlanetShopService
{
    private const int BuildingClassShop = 5;

    private static readonly ConcurrentDictionary<ulong, Task<AllShopsOnPlanet?>> All = new();

    static PlanetShopService() => CatalogService.Reloaded += () => All.Clear();

    private sealed record AllShopsOnPlanet(IReadOnlyList<BuildingSiteLocation> Sites, float Radius);

    /// <summary>Reads visited building/crate interaction coordinates on this planet from the save.</summary>
    public static HashSet<(long LatE2, long LonE2)> ReadVisitedLocations(ulong planetAddress, PlanetSnapshot? snapshot)
    {
        var visited = new HashSet<(long LatE2, long LonE2)>();
        if (snapshot?.StoredInteractions is not { Count: > 5 }) return visited;

        ulong targetSys = planetAddress & 0xFFFFFFFF;

        // Buffer 5 = Building, Buffer 1 = Crate
        foreach (int bufIndex in new[] { 5, 1 })
        {
            if (snapshot.StoredInteractions[bufIndex]?["O49"] is JArray entries)
            {
                foreach (var entry in entries)
                {
                    var ozw = entry["oZw"];
                    if (ozw is null) continue;

                    ulong addr = 0;
                    if (ozw.Type == JTokenType.Integer)
                    {
                        addr = ozw.Value<ulong>();
                    }
                    else if (ozw.Type == JTokenType.String && NmsSpaceStationPaths.ParsePackedAddress(ozw.Value<string>()) is ulong pa)
                    {
                        addr = pa;
                    }

                    if ((addr & 0xFFFFFFFF) == targetSys)
                    {
                        if (entry["wMC"] is JArray wmc && wmc.Count >= 3)
                        {
                            double x = wmc[0].Value<double>(), y = wmc[1].Value<double>(), z = wmc[2].Value<double>();
                            double r = Math.Sqrt(x * x + y * y + z * z);
                            if (r > 1.0)
                            {
                                double lat = Math.Asin(Math.Clamp(y / r, -1.0, 1.0)) * 180.0 / Math.PI;
                                double lon = Math.Atan2(x, z) * 180.0 / Math.PI;
                                visited.Add(((long)Math.Round(lat * 100), (long)Math.Round(lon * 100)));
                            }
                        }
                    }
                }
            }
        }

        return visited;
    }

    private static bool IsLocationVisited(double lat, double lon, HashSet<(long LatE2, long LonE2)> visited)
    {
        long lLat = (long)Math.Round(lat * 100);
        long lLon = (long)Math.Round(lon * 100);
        for (long dLat = -2; dLat <= 2; dLat++)
        {
            for (long dLon = -2; dLon <= 2; dLon++)
            {
                if (visited.Contains((lLat + dLat, lLon + dLon))) return true;
            }
        }
        return false;
    }

    /// <summary>Searches shops on this planet with distance calculation, visited status and range filters.</summary>
    public static async Task<ShopsOfPlanet?> SearchAsync(
        ulong planetAddress,
        double latitude,
        double longitude,
        bool here,
        int visitedFilter,
        double maxDistanceKm,
        int count,
        PlanetSnapshot? snapshot)
    {
        var task = All.GetOrAdd(planetAddress, AllShopsOnPlanetAsync);
        if (await task is not { } found)
        {
            All.TryRemove(new KeyValuePair<ulong, Task<AllShopsOnPlanet?>>(planetAddress, task));
            return null;
        }

        var visited = ReadVisitedLocations(planetAddress, snapshot);
        var dir = CrashedShipService.Direction(latitude, longitude);

        var allShops = found.Sites
            .Select(s =>
            {
                double angle = Math.Acos(Math.Clamp(Vector3.Dot(CrashedShipService.Direction(s.Latitude, s.Longitude), dir), -1f, 1f));
                double distKm = angle * found.Radius / 1000.0;
                bool isCurrent = here && distKm < 0.15;
                bool isVisited = isCurrent || IsLocationVisited(s.Latitude, s.Longitude, visited);
                return new PlanetShopSite(s with { DistanceKm = distKm }, isVisited, isCurrent);
            })
            .ToList();

        int totalOnPlanet = allShops.Count;
        int totalVisited = allShops.Count(s => s.IsVisited);
        int totalUnvisited = totalOnPlanet - totalVisited;

        var filtered = allShops
            .Where(s =>
            {
                if (visitedFilter == 1 && s.IsVisited) return false; // Unvisited only
                if (visitedFilter == 2 && !s.IsVisited) return false; // Visited only
                if (s.DistanceKm > maxDistanceKm) return false;
                return true;
            })
            .OrderBy(s => s.DistanceKm)
            .ToList();

        var nearest = filtered.Take(count).ToList();
        return new ShopsOfPlanet(nearest, filtered.Count, totalUnvisited, totalVisited, totalOnPlanet);
    }

    private static async Task<AllShopsOnPlanet?> AllShopsOnPlanetAsync(ulong planetAddress)
    {
        if (await CrashedShipService.ContextAsync(planetAddress) is not { } context || context.Uncharted) return null;

        var result = await Task.Run(() =>
        {
            var all = new List<BuildingSiteLocation>();
            var seen = new HashSet<ulong>();
            var c = context.Centre;

            for (int face = 0; face < 6; face++)
            {
                foreach (var site in PlanetBuildingSites.Sites(face, context.Divisions, context.Radius, context.SectorSeed,
                    context.Slots, cls => cls == BuildingClassShop))
                {
                    if (seen.Add(site.Key) && context.Builds(site))
                    {
                        var (lat, lon) = site.Coordinates;
                        var loc = new BuildingSiteLocation(planetAddress, site.Key, c.X + site.Point.X, c.Y + site.Point.Y, c.Z + site.Point.Z,
                            lat, lon, 0, null);
                        all.Add(loc);
                    }
                }
            }

            return new AllShopsOnPlanet(all, context.Radius);
        });

        return result;
    }
}
