using System.Numerics;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>A crashed ship's site: its key (the marker's BuildingSeed), point in system space at the planet radius,
/// the coordinates the Analysis Visor shows there, how far it is from where the search started, and the ship lying
/// there, its class C=0/B=1/A=2/S=3, and its inventory summary (slots, broken slots, value, and base-stat bonuses;
/// null when the ship pool settings, class rows, or starship layout tables are missing).</summary>
public sealed record CrashedShipSite(ulong PlanetAddress, ulong Key, double X, double Y, double Z, double Latitude,
    double Longitude, double DistanceKm, SystemStarship? Ship, int? ShipClass, bool Underwater = false, double? DepthMetres = null, bool Sentinel = false,
    CrashedShipInventorySummary? Inventory = null, StarshipDisplayedStats? DisplayedStats = null)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);

    /// <summary>The class the game shows: an Exotic is always S.</summary>
    public int? ShownClass => Ship?.Type == SystemStarshipType.Exotic ? 3 : ShipClass;
}

/// <summary>Everything the building-grid passes need about one planet and its system (<see cref="CrashedShipSites.Prepare"/>).</summary>
public sealed record PlanetSiteContext(ulong PlanetAddress, float Radius, int Divisions, int SectorSeed,
    (int Class, float Probability)[] Slots, (double X, double Y, double Z) Centre, SystemAttributes Attributes,
    IReadOnlyList<SystemStarship>? Pool, ulong SystemAddress, ShipPoolSettings? ShipPool, InventoryGenerationTables? Inventory,
    PlanetTerrainData? Terrain, bool Prime, float BuildingRadius, float WaterBuildingRadius, bool Waterworld,
    IReadOnlyDictionary<ulong, (int SizeTypeIndex, string SizeType, bool Cool)>? ModelSizes,
    IReadOnlyList<OverrideBuilding>? UnchartedWrecks, Func<string, IReadOnlyList<string>?>? Words = null)
{
    public bool Uncharted => Attributes.Race == SystemRace.None;

    /// <summary>Whether the game builds at this site (<see cref="PlanetBuildingSites.GroundAllows"/>); every site
    /// passes when the terrain can't be generated (underwater ones need a sea, so none then).</summary>
    public bool Builds(BuildingSite site) => site.Class == BuildingClass.WaterDistressSignal
        ? Terrain is { } w && PlanetBuildingSites.GroundAllows(w, site.Point, Radius, Prime, WaterBuildingRadius, underwater: true, Waterworld)
        : Terrain is not { } t || PlanetBuildingSites.GroundAllows(t, site.Point, Radius, Prime, BuildingRadius, waterworld: Waterworld);
}

/// <summary>
/// Where a planet's crashed ships are: the DistressSignal sites of the game's building grid
/// (<see cref="PlanetBuildingSites.SitesNear"/>, catalog format 49), nearest a point first, which of the system's
/// pool ships lies at each (<see cref="SystemShipPool.CrashedShip"/>, catalog format 50) and its class
/// (<see cref="SystemShipPool.CrashedShipClass"/>). Sites whose ground the game rejects (steep slopes, under the sea)
/// are left out (<see cref="PlanetBuildingSites.GroundAllows"/>). Checked 2026-10-28/29: 14/14 crashed ships found in
/// game exact (position, name, type, class) on Ribhakyam and in the pirate system Nugepan III, prime planet included,
/// and the site key the game's own Planetary Chart picked on Lora equals the predicted one (issue #35).
/// Uncharted systems (no race) are different: the grid's wrecks aren't there (Shlifs, Hamagan-Oerat VI, #34), and
/// the owner's tests 2026-11-05 (Slot 7) found only the planet's fixed override sites
/// (<see cref="PlanetOverridePlacement.ForSystem"/>). Three Planetary Chart markers were exactly the land
/// DistressSignal override - a ship-less Traveller crash scene with a small reward - and the Nautilon's "Drowned
/// Starship" on Kettinas XVII was exactly the WaterDistressSignal override (+60.83, +53.80) holding the predicted
/// ship (The Bard of the Abyss, A-class Explorer). So only that underwater site is listed there.
/// Moved from the desktop's CrashedShipService (2026-11-18) so the phone shares it.
/// </summary>
public static class CrashedShipSites
{
    private const int DistressSignal = BuildingClass.DistressSignal,
        WaterDistressSignal = BuildingClass.WaterDistressSignal,
        SentinelDistressSignal = BuildingClass.SentinelDistressSignal;

    /// <summary>Claimable wrecks: land (20), underwater (32) and crashed Sentinel Interceptors (52, only on planets with
    /// corrupted sentinels - the building table's RequiresCorruptSentinels).</summary>
    public static bool IsWreck(int cls) => cls is DistressSignal or WaterDistressSignal or SentinelDistressSignal;

    /// <summary>The planet's building grid, terrain and system facts; null when the planet can't be worked out (bad
    /// index, unknown size, missing tables). <paramref name="centre"/> is the planet's centre in system space
    /// (<see cref="PortalPositions.Centres"/>); <paramref name="partTreeJson"/> gives a ship descriptor's part tree for
    /// a wreck's inventory summary, which is left out without it.</summary>
    public static PlanetSiteContext? Prepare(ulong planetAddress, (double X, double Y, double Z) centre, BiomeTables biomes,
        PlanetProfileTables profiles, BuildingClassPlacement[] placement, BuildingOverrideRule[]? overrides, SolarLayoutTables? solar,
        Func<string, IReadOnlyList<string>?>? words, InventoryGenerationTables? inventory, Func<string, string?>? partTreeJson)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        if (index < 1) return null;
        var (code, galaxy) = SystemProfiles.PortalOf(planetAddress & ~(0xFUL << 52));
        var types = PlanetGenerator.Generate(code, galaxy, biomes);
        var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
        if (index > types.Length || index > seeds.Length
            || PlanetBuildingSites.SurfaceRadius(types[index - 1].BodyClass, types[index - 1].Biome) is not { } radius
            || PlanetBuildingSites.FactsFor(code, galaxy, index - 1, biomes, profiles) is not { } facts) return null;
        var attributes = SystemGenerator.Attributes(code, galaxy);
        var layout = solar?.ShipPool is null ? null : SolarLayoutGeneration.ForSystem(code, galaxy, solar, true);
        var pool = layout is null ? null : SystemStarshipGenerator.GenerateForSystem(code, galaxy, attributes.Race,
            attributes.Pirate, solar!, words, includeSentinel: true);
        Dictionary<ulong, (int SizeTypeIndex, string SizeType, bool Cool)>? modelSizes = null;
        if (pool is not null && inventory is not null && partTreeJson is not null)
        {
            var parsedTrees = new Dictionary<string, IReadOnlyList<PartGroup>?>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<PartGroup>? LoadTree(string path)
            {
                if (parsedTrees.TryGetValue(path, out var tree)) return tree;
                return parsedTrees[path] = partTreeJson(path) is { } json ? ShipParts.ParseTree(json) : null;
            }
            modelSizes = new Dictionary<ulong, (int SizeTypeIndex, string SizeType, bool Cool)>();
            foreach (var s in pool)
                if (StarshipInventoryGeneration.ResolveModelSize(s.Type, s.Seed, LoadTree, inventory) is { } resolved)
                    modelSizes[s.Seed] = resolved;
        }
        var body = types[index - 1];
        var terrainTables = profiles.Objects?.Terrain;
        var choice = terrainTables is null ? null : PlanetProfileGenerator.TerrainChoiceFor(seeds[index - 1], body, (int)attributes.Star, profiles);
        var terrain = choice is null || choice.SettingIndex >= terrainTables!.Settings.Length ? null
            : PlanetTerrain.Generate(seeds[index - 1], terrainTables.Settings[choice.SettingIndex], choice, terrainTables.SeaStandard,
                body.BodyClass == 3, body.DeadToWeird);
        // The crashed ship's flatness ring on prime planets: DistressSignal's OverrideRadius in the building table.
        float buildingRadius = overrides?.FirstOrDefault(r => r.Class == DistressSignal)?.OverrideRadius ?? 0f;
        float waterRadius = overrides?.FirstOrDefault(r => r.Class == WaterDistressSignal)?.OverrideRadius ?? 0f;
        // Uncharted: the planet's fixed underwater wreck (see the class summary); none when it can't be placed.
        var unchartedWrecks = attributes.Race != SystemRace.None || overrides is null || solar is null ? null
            : PlanetOverridePlacement.ForSystem(code, galaxy, biomes, profiles, solar, overrides)
                .ElementAtOrDefault(index - 1)?.Where(b => b.Placed && b.Class == WaterDistressSignal).ToArray();
        return new PlanetSiteContext(planetAddress, radius, PlanetBuildingSites.Divisions(radius),
            PlanetBuildingSites.VoronoiSeeds(seeds[index - 1]).SectorSeed, PlanetBuildingSites.Slots(placement, facts), centre,
            attributes, pool, layout?.Address ?? 0, solar?.ShipPool, inventory, terrain, body.Prime, buildingRadius, waterRadius,
            body.Biome == 14, modelSizes, unchartedWrecks, words);   // 14 = Waterworld (GcBiomeType)
    }

    /// <summary>The <paramref name="count"/> grid wrecks nearest a point on the planet, nearest first (not for uncharted
    /// systems, whose grid wrecks aren't there: use <see cref="All"/>).</summary>
    public static IReadOnlyList<CrashedShipSite> Near(PlanetSiteContext context, double latitude, double longitude, int count)
    {
        var dir = Direction(latitude, longitude);
        return PlanetBuildingSites.SitesNear(dir, context.Divisions, context.Radius, context.SectorSeed, context.Slots, IsWreck, count * 4)
            .Where(context.Builds).Take(count)
            .Select(s => Describe(context, s, dir)).ToList();
    }

    /// <summary>Every crashed ship of the planet: a pass over the whole building grid (about 1.8 million cells and
    /// 2,000 wrecks on a Large planet, 0.15-0.3 s), or the fixed underwater wreck of an uncharted system's planet.
    /// Each one's distance is from its own spot (0); callers measure from their point.</summary>
    public static IReadOnlyList<CrashedShipSite> All(PlanetSiteContext context)
    {
        var all = new List<CrashedShipSite>();
        if (context.Uncharted)
        {
            var centre = new Vector3((float)context.Centre.X, (float)context.Centre.Y, (float)context.Centre.Z);
            foreach (var wreck in context.UnchartedWrecks ?? [])
            {
                var site = new BuildingSite(0, 0, 0, wreck.Key, wreck.Position - centre, WaterDistressSignal);
                all.Add(Describe(context, site, Vector3.Normalize(site.Point)));
            }
            return all;
        }
        var seen = new HashSet<ulong>();
        for (int face = 0; face < 6; face++)
            foreach (var site in PlanetBuildingSites.Sites(face, context.Divisions, context.Radius, context.SectorSeed,
                context.Slots, IsWreck))
                if (seen.Add(site.Key) && context.Builds(site))   // a cell on a cube edge belongs to two faces
                    all.Add(Describe(context, site, Vector3.Normalize(site.Point)));
        return all;
    }

    /// <summary>The planet's wrecks of class <paramref name="minClass"/> or better as the game shows it (an Exotic is
    /// S), like <see cref="All"/> but much cheaper for a search over many planets: the class (a hash of the site key)
    /// is checked first and the ground test (terrain) only runs for the few that pass.</summary>
    public static List<CrashedShipSite> OfClass(PlanetSiteContext context, int minClass, CancellationToken cancel = default)
    {
        if (context.Uncharted) return All(context).Where(s => s.ShownClass >= minClass).ToList();
        var found = new List<CrashedShipSite>();
        var seen = new HashSet<ulong>();
        var attributes = context.Attributes;
        for (int face = 0; face < 6; face++)
            foreach (var site in PlanetBuildingSites.Sites(face, context.Divisions, context.Radius, context.SectorSeed, context.Slots, IsWreck))
            {
                if (!seen.Add(site.Key)) continue;   // a cell on a cube edge belongs to two faces
                if ((seen.Count & 255) == 0) cancel.ThrowIfCancellationRequested();
                int cls = SystemShipPool.CrashedShipClass(site.Key, attributes.Wealth, attributes.Pirate, context.Inventory) ?? -1;
                if (cls < minClass)
                {
                    // Only an Exotic lifts a lower roll to S; pick the ship just for those few.
                    if (minClass > 3 || site.Class == SentinelDistressSignal || context.Pool is null || context.ShipPool is null) continue;
                    var ship = SystemShipPool.CrashedShip(site.Key, context.Pool, attributes.Race, attributes.Pirate, context.SystemAddress, context.ShipPool);
                    if (ship?.Type != SystemStarshipType.Exotic) continue;
                }
                if (context.Builds(site)) found.Add(Describe(context, site, Vector3.Normalize(site.Point)));
            }
        return found;
    }

    /// <summary>A wreck site described: coordinates, the ship lying there, its class and inventory, depth underwater.</summary>
    public static CrashedShipSite Describe(PlanetSiteContext context, BuildingSite site, Vector3 from)
    {
        var (lat, lon) = site.Coordinates;
        var c = context.Centre;
        // A crashed Sentinel Interceptor is the system's own (pool #36) - assumed, not traced; its class roll is assumed
        // to be the same site-key roll as other wrecks.
        bool sentinel = site.Class == SentinelDistressSignal;
        var ship = context.Pool is null || context.ShipPool is null ? null
            : sentinel ? SentinelWreck(context, site.Key)
            : SystemShipPool.CrashedShip(site.Key, context.Pool, context.Attributes.Race, context.Attributes.Pirate,
                context.SystemAddress, context.ShipPool);
        int? shipClass = SystemShipPool.CrashedShipClass(site.Key, context.Attributes.Wealth, context.Attributes.Pirate, context.Inventory);
        CrashedShipInventorySummary? invSummary = null;
        if (ship is not null && shipClass is int cls && context.ModelSizes is not null
            && context.ModelSizes.TryGetValue(ship.Seed, out var modelSize))
        {
            int nativeWealth = context.Attributes.Pirate ? 3 : Math.Clamp(context.Attributes.Wealth - 1, 0, 2);
            invSummary = StarshipInventoryGeneration.ForCrashedShip(ship.Type, modelSize, site.Key, cls, context.Inventory, nativeWealth);
        }
        double angle = PlanetHotspotCluster.GroundDistance(site.Point, from, 1.0);
        return new CrashedShipSite(context.PlanetAddress, site.Key, c.X + site.Point.X, c.Y + site.Point.Y, c.Z + site.Point.Z,
            lat, lon, angle * context.Radius / 1000, ship,
            shipClass, site.Class == WaterDistressSignal,
            site.Class == WaterDistressSignal ? Depth(context, site) : null, sentinel, invSummary);
    }

    /// <summary>A crashed Sentinel ship: the system's own Sentinel Interceptor (pool #36, assumed), named from the
    /// site key with the Sentinel naming routine - the game titles them that way (Nionne Sigma 2026-10-29: Crimson-σ,
    /// Flameborn-9-MH3 and Hope-1-D are exactly ShipNames.GenerateSentinel of their site keys).</summary>
    private static SystemStarship? SentinelWreck(PlanetSiteContext context, ulong siteKey)
    {
        var ship = context.Pool?.FirstOrDefault(s => s.Type == SystemStarshipType.SentinelInterceptor);
        return ship is not null && context.Words is { } words && ShipNames.GenerateSentinel(siteKey, words) is { } name
            ? ship with { DefaultName = name } : ship;
    }

    /// <summary>How deep the sea floor is at an underwater site: sea level minus Core's generated ground height. On the
    /// water world Waterworld (2026-10-29) the game's sea floor read 1,480.9 at Rozawa Guardian III where this gives
    /// 1,456 - the game refines the ground once the area loads - so it's shown rounded.</summary>
    private static double? Depth(PlanetSiteContext context, BuildingSite site)
    {
        if (context.Terrain is not { } t || t.SeaLevel <= 0f) return null;
        var p = Vector3.Normalize(site.Point) * context.Radius;
        return t.SeaLevel - PlanetTerrain.Height(t, p.X, p.Y, p.Z);
    }

    /// <summary>The unit direction from the planet centre to a latitude / longitude (degrees).</summary>
    public static Vector3 Direction(double latitude, double longitude)
    {
        double la = latitude * Math.PI / 180, lo = longitude * Math.PI / 180;
        return new Vector3((float)(Math.Cos(la) * Math.Sin(lo)), (float)Math.Sin(la), (float)(Math.Cos(la) * Math.Cos(lo)));
    }

    /// <summary>The angle between a site's direction from the planet centre and a direction.</summary>
    public static double Angle(CrashedShipSite site, Vector3 dir) =>
        PlanetHotspotCluster.GroundDistance(Direction(site.Latitude, site.Longitude), dir, 1.0);
}
