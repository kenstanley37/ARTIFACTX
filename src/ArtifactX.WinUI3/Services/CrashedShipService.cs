using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A crashed ship's site: its key (the marker's BuildingSeed), point in system space at the planet radius,
/// the coordinates the Analysis Visor shows there, and how far it is from where the search started.</summary>
public sealed record CrashedShipSite(ulong PlanetAddress, ulong Key, double X, double Y, double Z, double Latitude,
    double Longitude, double DistanceKm)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);
}

/// <summary>
/// Where a planet's crashed ships are: the DistressSignal sites of the game's building grid
/// (<see cref="PlanetBuildingSites.SitesNear"/>, catalog format 49), nearest a point first. Which ship stands there
/// (name, class, type) isn't known yet - its seed comes from the building, and how is still being worked out
/// (issue #35). A site may rarely stay empty where the ground doesn't allow a building (terrain isn't checked here).
/// Checked 2026-10-28: Final Eokak III on Ribhakyam at the predicted site (+66.88, +81.50 vs +66.88, +81.49).
/// </summary>
public static class CrashedShipService
{
    private const int DistressSignal = 20;

    /// <summary>The <paramref name="count"/> crashed-ship sites nearest a point on the planet; empty when the catalog
    /// doesn't have the tables yet or the planet can't be generated.</summary>
    public static async Task<IReadOnlyList<CrashedShipSite>> NearAsync(ulong planetAddress, double latitude, double longitude, int count = 5)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        var biomes = await CatalogService.GetBiomeTablesAsync();
        var profiles = await CatalogService.GetPlanetProfileTablesAsync();
        var placement = await CatalogService.GetBuildingPlacementAsync();
        var centre = await PlanetCentreService.GetAsync(planetAddress);
        if (index < 1 || biomes is null || profiles is null || placement is null || centre is not { } c) return [];
        var (code, galaxy) = UniverseInfo.Generator(planetAddress & ~(0xFUL << 52));
        return await Task.Run(() =>
        {
            var types = PlanetGenerator.Generate(code, galaxy, biomes);
            var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
            if (index > types.Length || index > seeds.Length
                || PlanetBuildingSites.Radius(types[index - 1].BodyClass) is not { } radius
                || PlanetBuildingSites.FactsFor(code, galaxy, index - 1, biomes, profiles) is not { } facts) return (IReadOnlyList<CrashedShipSite>)[];
            var slots = PlanetBuildingSites.Slots(placement, facts);
            double la = latitude * Math.PI / 180, lo = longitude * Math.PI / 180;
            var dir = new Vector3((float)(Math.Cos(la) * Math.Sin(lo)), (float)Math.Sin(la), (float)(Math.Cos(la) * Math.Cos(lo)));
            var sites = PlanetBuildingSites.SitesNear(dir, PlanetBuildingSites.Divisions(radius), radius,
                PlanetBuildingSites.VoronoiSeeds(seeds[index - 1]).SectorSeed, slots, cls => cls == DistressSignal, count);
            return sites.Select(s =>
            {
                var (lat, lon) = s.Coordinates;
                double angle = Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(s.Point), dir), -1f, 1f));
                return new CrashedShipSite(planetAddress, s.Key, c.X + s.Point.X, c.Y + s.Point.Y, c.Z + s.Point.Z, lat, lon,
                    angle * radius / 1000);
            }).ToList();
        });
    }
}
