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

    /// <summary>An S-class wreck, or an Exotic (always S).</summary>
    public bool IsNotable => ShipClass == 3 || Ship?.Type == SystemStarshipType.Exotic;
}

/// <summary>A planet's S-class and Exotic crashed ships nearest a point, out of <see cref="Total"/> on the whole
/// planet. <see cref="Unverified"/>: a system with no race, where the predictions haven't held in game yet.</summary>
public sealed record NotableCrashedShips(IReadOnlyList<CrashedShipSite> Nearest, int Total, bool Unverified);

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

    /// <summary>Every notable (S-class or Exotic) wreck of a planet, worked out once per planet by a pass over the whole
    /// building grid (about 1.8 million cells on a Large planet, off the UI thread).</summary>
    private static readonly ConcurrentDictionary<ulong, Task<NotableOnPlanet?>> Notable = new();

    static CrashedShipService() => CatalogService.Reloaded += () => Notable.Clear();

    private sealed record NotableOnPlanet(IReadOnlyList<CrashedShipSite> Sites, float Radius, bool Unverified);

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

    /// <summary>The planet's S-class and Exotic crashed ships nearest a point (you, or the portal), and how many the
    /// whole planet has; null when the planet can't be worked out yet.</summary>
    public static async Task<NotableCrashedShips?> NotableAsync(ulong planetAddress, double latitude, double longitude, int count = 1)
    {
        var task = Notable.GetOrAdd(planetAddress, NotableOnPlanetAsync);
        if (await task is not { } found)
        {
            Notable.TryRemove(new KeyValuePair<ulong, Task<NotableOnPlanet?>>(planetAddress, task));   // retry once the catalog is ready
            return null;
        }
        var dir = Direction(latitude, longitude);
        var nearest = found.Sites
            .Select(s => s with { DistanceKm = Angle(s, dir) * found.Radius / 1000 })
            .OrderBy(s => s.DistanceKm).Take(count).ToList();
        return new NotableCrashedShips(nearest, found.Sites.Count, found.Unverified);
    }

    private static async Task<NotableOnPlanet?> NotableOnPlanetAsync(ulong planetAddress)
    {
        if (await ContextAsync(planetAddress) is not { Pool: not null } context) return null;
        return await Task.Run(() =>
        {
            var seen = new HashSet<ulong>();
            var notable = new List<CrashedShipSite>();
            for (int face = 0; face < 6; face++)
                foreach (var site in PlanetBuildingSites.Sites(face, context.Divisions, context.Radius, context.SectorSeed,
                    context.Slots, cls => cls == DistressSignal))
                {
                    if (!seen.Add(site.Key)) continue;   // a cell on a cube edge belongs to two faces
                    var described = Describe(context, site, Vector3.Normalize(site.Point));
                    if (described.IsNotable) notable.Add(described);
                }
            return new NotableOnPlanet(notable, context.Radius, context.Attributes.Race == SystemRace.None);
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
