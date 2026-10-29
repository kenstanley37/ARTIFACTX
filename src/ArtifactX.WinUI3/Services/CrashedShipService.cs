using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A crashed ship's site: its key (the marker's BuildingSeed), point in system space at the planet radius,
/// the coordinates the Analysis Visor shows there, how far it is from where the search started, and the ship lying
/// there and its class C=0/B=1/A=2/S=3 (null when the catalog lacks the ship pool settings or class rows).</summary>
public sealed record CrashedShipSite(ulong PlanetAddress, ulong Key, double X, double Y, double Z, double Latitude,
    double Longitude, double DistanceKm, SystemStarship? Ship, int? ShipClass)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);

    /// <summary>The class the game shows: an Exotic is always S.</summary>
    public int? ShownClass => Ship?.Type == SystemStarshipType.Exotic ? 3 : ShipClass;
}

/// <summary>A planet's crashed ships of one class nearest a point, out of <see cref="Total"/> of that class on the
/// whole planet. <see cref="Unverified"/>: a system with no race, where the predictions haven't held in game yet.</summary>
public sealed record CrashedShipsOfClass(IReadOnlyList<CrashedShipSite> Nearest, int Total, bool Unverified);

/// <summary>
/// Where a planet's crashed ships are: the DistressSignal sites of the game's building grid
/// (<see cref="PlanetBuildingSites.SitesNear"/>, catalog format 49), nearest a point first, which of the system's
/// pool ships lies at each (<see cref="SystemShipPool.CrashedShip"/>, catalog format 50) and its class
/// (<see cref="SystemShipPool.CrashedShipClass"/>). A site may rarely stay empty where the ground doesn't allow a
/// building (terrain isn't checked here). Checked 2026-10-28/29: 11/11 crashed ships opened in game exact (position,
/// name, type, class) on Ribhakyam and in the pirate system Nugepan III, prime planet included, and the site key the
/// game's own Planetary Chart picked on Lora equals the predicted one (issue #35). In the uncharted system Shlifs (no
/// race) the predicted site checked was empty - the game's building table differs there
/// (docs/research/CRASHED_SHIPS.md) - so results for systems with no race are flagged as unverified.
/// </summary>
public static class CrashedShipService
{
    private const int DistressSignal = 20;

    /// <summary>Every crashed ship of a planet, worked out once per planet by a pass over the whole building grid
    /// (about 1.8 million cells and 2,000 wrecks on a Large planet, 0.15-0.3 s off the UI thread).</summary>
    private static readonly ConcurrentDictionary<ulong, Task<AllOnPlanet?>> All = new();

    static CrashedShipService() => CatalogService.Reloaded += () => All.Clear();

    private sealed record AllOnPlanet(IReadOnlyList<CrashedShipSite> Sites, float Radius, bool Unverified);

    /// <summary>Everything the passes need about one planet and its system.</summary>
    private sealed record PlanetContext(ulong PlanetAddress, float Radius, int Divisions, int SectorSeed,
        (int Class, float Probability)[] Slots, (double X, double Y, double Z) Centre, SystemAttributes Attributes,
        IReadOnlyList<SystemStarship>? Pool, ulong SystemAddress, ShipPoolSettings? ShipPool, InventoryGenerationTables? Inventory);

    /// <summary>The <paramref name="count"/> crashed-ship sites nearest a point on the planet; empty when the catalog
    /// doesn't have the tables yet or the planet can't be generated.</summary>
    public static async Task<IReadOnlyList<CrashedShipSite>> NearAsync(ulong planetAddress, double latitude, double longitude, int count = 5)
    {
        if (await ContextAsync(planetAddress) is not { } context) return [];
        return await Task.Run(() =>
        {
            var dir = Direction(latitude, longitude);
            return (IReadOnlyList<CrashedShipSite>)PlanetBuildingSites.SitesNear(dir, context.Divisions, context.Radius,
                    context.SectorSeed, context.Slots, cls => cls == DistressSignal, count)
                .Select(s => Describe(context, s, dir)).ToList();
        });
    }

    /// <summary>The planet's crashed ships of class <paramref name="shipClass"/> (C=0 .. S=3; Exotics count as S)
    /// nearest a point (you, or the portal), and how many of that class the whole planet has; null when the planet
    /// can't be worked out yet.</summary>
    public static async Task<CrashedShipsOfClass?> OfClassAsync(ulong planetAddress, int shipClass, double latitude, double longitude, int count = 1)
    {
        var task = All.GetOrAdd(planetAddress, AllOnPlanetAsync);
        if (await task is not { } found)
        {
            All.TryRemove(new KeyValuePair<ulong, Task<AllOnPlanet?>>(planetAddress, task));   // retry once the catalog is ready
            return null;
        }
        var dir = Direction(latitude, longitude);
        var ofClass = found.Sites.Where(s => s.ShownClass == shipClass).ToList();
        var nearest = ofClass
            .Select(s => s with { DistanceKm = Angle(s, dir) * found.Radius / 1000 })
            .OrderBy(s => s.DistanceKm).Take(count).ToList();
        return new CrashedShipsOfClass(nearest, ofClass.Count, found.Unverified);
    }

    private static async Task<AllOnPlanet?> AllOnPlanetAsync(ulong planetAddress)
    {
        if (await ContextAsync(planetAddress) is not { Pool: not null } context) return null;
        return await Task.Run(() =>
        {
            var seen = new HashSet<ulong>();
            var all = new List<CrashedShipSite>();
            for (int face = 0; face < 6; face++)
                foreach (var site in PlanetBuildingSites.Sites(face, context.Divisions, context.Radius, context.SectorSeed,
                    context.Slots, cls => cls == DistressSignal))
                    if (seen.Add(site.Key))   // a cell on a cube edge belongs to two faces
                        all.Add(Describe(context, site, Vector3.Normalize(site.Point)));
            return new AllOnPlanet(all, context.Radius, context.Attributes.Race == SystemRace.None);
        });
    }

    private static async Task<PlanetContext?> ContextAsync(ulong planetAddress)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        var biomes = await CatalogService.GetBiomeTablesAsync();
        var profiles = await CatalogService.GetPlanetProfileTablesAsync();
        var placement = await CatalogService.GetBuildingPlacementAsync();
        var solar = (await CatalogService.GetStationEquipmentTablesAsync())?.SolarLayout;
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var inventory = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();
        var centre = await PlanetCentreService.GetAsync(planetAddress);
        if (index < 1 || biomes is null || profiles is null || placement is null || centre is not { } c) return null;
        var (code, galaxy) = UniverseInfo.Generator(planetAddress & ~(0xFUL << 52));
        return await Task.Run(() =>
        {
            var types = PlanetGenerator.Generate(code, galaxy, biomes);
            var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
            if (index > types.Length || index > seeds.Length
                || PlanetBuildingSites.Radius(types[index - 1].BodyClass) is not { } radius
                || PlanetBuildingSites.FactsFor(code, galaxy, index - 1, biomes, profiles) is not { } facts) return null;
            var attributes = SystemGenerator.Attributes(code, galaxy);
            var layout = solar?.ShipPool is null ? null : SolarLayoutGeneration.ForSystem(code, galaxy, solar, true);
            var pool = layout is null ? null : SystemStarshipGenerator.GenerateForSystem(code, galaxy, attributes.Race,
                attributes.Pirate, solar!, k => words.GetValueOrDefault(k));
            return new PlanetContext(planetAddress, radius, PlanetBuildingSites.Divisions(radius),
                PlanetBuildingSites.VoronoiSeeds(seeds[index - 1]).SectorSeed, PlanetBuildingSites.Slots(placement, facts), c,
                attributes, pool, layout?.Address ?? 0, solar?.ShipPool, inventory);
        });
    }

    private static CrashedShipSite Describe(PlanetContext context, BuildingSite site, Vector3 from)
    {
        var (lat, lon) = site.Coordinates;
        var c = context.Centre;
        var ship = context.Pool is null || context.ShipPool is null ? null
            : SystemShipPool.CrashedShip(site.Key, context.Pool, context.Attributes.Race, context.Attributes.Pirate,
                context.SystemAddress, context.ShipPool);
        double angle = Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(site.Point), from), -1f, 1f));
        return new CrashedShipSite(context.PlanetAddress, site.Key, c.X + site.Point.X, c.Y + site.Point.Y, c.Z + site.Point.Z,
            lat, lon, angle * context.Radius / 1000, ship,
            SystemShipPool.CrashedShipClass(site.Key, context.Attributes.Wealth, context.Attributes.Pirate, context.Inventory));
    }

    private static Vector3 Direction(double latitude, double longitude)
    {
        double la = latitude * Math.PI / 180, lo = longitude * Math.PI / 180;
        return new Vector3((float)(Math.Cos(la) * Math.Sin(lo)), (float)Math.Sin(la), (float)(Math.Cos(la) * Math.Cos(lo)));
    }

    /// <summary>The angle between a site's direction from the planet centre and a direction.</summary>
    private static double Angle(CrashedShipSite site, Vector3 dir) =>
        Math.Acos(Math.Clamp(Vector3.Dot(Direction(site.Latitude, site.Longitude), dir), -1f, 1f));
}
