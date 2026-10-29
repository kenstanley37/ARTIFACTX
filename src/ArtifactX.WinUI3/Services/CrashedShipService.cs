using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A crashed ship's site: its key (the marker's BuildingSeed), point in system space at the planet radius,
/// the coordinates the Analysis Visor shows there, how far it is from where the search started, and the ship lying
/// there (null when the catalog lacks the ship pool settings).</summary>
public sealed record CrashedShipSite(ulong PlanetAddress, ulong Key, double X, double Y, double Z, double Latitude,
    double Longitude, double DistanceKm, SystemStarship? Ship)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);
}

/// <summary>
/// Where a planet's crashed ships are: the DistressSignal sites of the game's building grid
/// (<see cref="PlanetBuildingSites.SitesNear"/>, catalog format 49), nearest a point first, and which of the system's
/// pool ships lies at each (<see cref="SystemShipPool.CrashedShip"/>, catalog format 50; its class isn't traced yet).
/// A site may rarely stay empty where the ground doesn't allow a building (terrain isn't checked here).
/// Checked 2026-10-28/29 on Ribhakyam: Final Eokak III at the predicted site (+66.88, +81.50 vs +66.88, +81.49), and
/// the names and types of all 3 crashed ships opened there (issue #35).
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
        var solar = (await CatalogService.GetStationEquipmentTablesAsync())?.SolarLayout;
        var words = await CatalogService.GetProceduralNameWordsAsync();
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
            var attributes = SystemGenerator.Attributes(code, galaxy);
            var layout = solar?.ShipPool is null ? null : SolarLayoutGeneration.ForSystem(code, galaxy, solar, true);
            var pool = layout is null ? null : SystemStarshipGenerator.GenerateForSystem(code, galaxy, attributes.Race,
                attributes.Pirate, solar!, k => words.GetValueOrDefault(k));
            double la = latitude * Math.PI / 180, lo = longitude * Math.PI / 180;
            var dir = new Vector3((float)(Math.Cos(la) * Math.Sin(lo)), (float)Math.Sin(la), (float)(Math.Cos(la) * Math.Cos(lo)));
            var sites = PlanetBuildingSites.SitesNear(dir, PlanetBuildingSites.Divisions(radius), radius,
                PlanetBuildingSites.VoronoiSeeds(seeds[index - 1]).SectorSeed, slots, cls => cls == DistressSignal, count);
            return sites.Select(s =>
            {
                var (lat, lon) = s.Coordinates;
                double angle = Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(s.Point), dir), -1f, 1f));
                return new CrashedShipSite(planetAddress, s.Key, c.X + s.Point.X, c.Y + s.Point.Y, c.Z + s.Point.Z, lat, lon,
                    angle * radius / 1000, pool is null ? null
                        : SystemShipPool.CrashedShip(s.Key, pool, attributes.Race, attributes.Pirate, layout!.Address, solar!.ShipPool!));
            }).ToList();
        });
    }
}
