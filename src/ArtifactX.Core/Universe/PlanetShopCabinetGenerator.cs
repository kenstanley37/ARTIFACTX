using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>The predicted world position and cabinet seed of a planet shop's multi-tool crate.</summary>
/// <param name="SiteKey">The shop site's Voronoi key (BuildingSeed).</param>
/// <param name="BuildingPoint">The shop's planet-relative point after cluster re-centring.</param>
/// <param name="SiteHeight">Core's <see cref="PlanetTerrain.Height"/> at the unshifted site point A.</param>
/// <param name="SpawnHeight">The Far-region spawn height above the base sphere at <paramref name="BuildingPoint"/>.</param>
/// <param name="CratePosition">The unrounded crate point in solar-system space.</param>
/// <param name="CabinetPoint">The whole-metre crate point in solar-system space (<c>floor(v + 0.5)</c>).</param>
/// <param name="CabinetSeed">The cabinet interaction seed (<see cref="MultiToolSiteGeneration.SeedForPosition"/>).</param>
/// <param name="AlternateNearEdge">When <paramref name="CratePosition"/> lies within the rounding margin of a half-metre
/// boundary on an axis, the adjacent whole-metre point and its seed.</param>
public sealed record PlanetShopCabinet(
    ulong SiteKey,
    (double X, double Y, double Z) BuildingPoint,
    double SiteHeight,
    double SpawnHeight,
    (double X, double Y, double Z) CratePosition,
    (int X, int Y, int Z) CabinetPoint,
    ulong CabinetSeed,
    ((int X, int Y, int Z) Point, ulong Seed)? AlternateNearEdge = null);

/// <summary>Precomputed planet state needed to predict shop cabinets across sites on a single planet.</summary>
public sealed record PlanetShopCabinetContext(
    BuildingLayoutTables LayoutTables,
    PlanetTerrainData Terrain,
    float PlanetRadius,
    (double X, double Y, double Z) PlanetCentre,
    BuildingLayout ShopLayout,
    IReadOnlyList<BuildingClusterEntry> ClusterLayout,
    IReadOnlyList<int> RecordClasses,
    IReadOnlyDictionary<int, double> RecordRadii);

/// <summary>
/// Spawn height of a cluster building on the Far terrain region and the shop weapon crate's whole-metre point and seed
/// (NMS.exe build 184271, ported from scripts/research/region-generator/shopspawn.py;
/// docs/research/PLANETARY_MULTITOOL_LOCATIONS.md, "Spawn height from the address"):
/// - inside a cluster's flat zone every Far-region grid node has surface = <see cref="PlanetTerrain.Height"/>(A);
///   <see cref="FarGridValue"/> models the per-layer u16 density quantization (0x1412B13F0 / 0x1412B1760) and the 0.5
///   crossing extraction (0x1412B3370);
/// - the instance creator (0x1412B7F70) divides the local cube coordinates by the 16 m cell size, reads corners P00
///   (i, j), P01 (i, j+1), P10 (i+1, j) which hold the node one cell back, interpolates lerp(lerp(P00, P01, fz), P10,
///   fx) with fx, fz = frac(g / 4) (the index divided by the cell size a second time), and adds
///   <see cref="PlanetTerrain.BaseHeight"/> at that lerped, unshifted position (<see cref="SpawnInputs"/>);
/// - the cabinet is rolled when the building spawns: crate = building spawn origin + root offset + crate in the root
///   room + floored planet centre, rounded to whole metres (<see cref="MultiToolSiteGeneration.SeedForPosition"/>).
/// Checked 2026-11-06 on Kaim 30/19 (cube face +X): 4/4 spawn heights within 2.6 cm of the live instances; 6/7 shop
/// cabinets exact (404DA563E9C5EC88, 66386EC5762B09FB, 627968CDF4A27660, 741BF5DCCD41B78A, 31CCA8AE9D607F54,
/// D2D981D73443CF98), the seventh (8924A2E5C39F854D) ~1-2 cm across a whole-metre rounding boundary in X with exact
/// <see cref="PlanetShopCabinet.AlternateNearEdge"/>. Cube faces other than +X use the same 0x140576380 face basis and
/// are not yet verified in game (tracked in GitHub issue #1).
/// </summary>
public static class PlanetShopCabinetGenerator
{
    private const float QuantScale = 1.5259021893143654e-05f;   // 0x144BF35D4, 1 / 65535
    private const float DensitySlope = 0.75f / 128f;
    private const double Cell = 16.0;

    /// <summary>A Far grid node's stored height (local units) for a column whose surface is <paramref name="surface"/>
    /// metres above the node's BaseHeight inside a cluster's flat zone.</summary>
    public static float FarGridValue(float surface)
    {
        float Decoded(int layer)
        {
            float d = (surface - 8f * layer) * DensitySlope;
            int q = (int)(((0.5f + d) / 5f + 0.5f) * 65535f);
            return (q * QuantScale - 0.5f) * 5f;
        }
        int k = 0;
        while (Decoded(k + 1) >= 0.5f) k++;
        float dk = Decoded(k), dk1 = Decoded(k + 1);
        float t = Math.Clamp((dk - 0.5f) / (dk - dk1), 0f, 1f);
        return (t + k) * 2f;
    }

    /// <summary>0x1412B7F70's sampling inputs for a building at planet-relative <paramref name="point"/>: the three
    /// triangle weights (P00, P01, P10), the three Far-grid nodes one cell back, and the lerped unshifted position.
    /// Only cube face +X is verified against live instances; the other five cube faces follow the same 0x140576380
    /// face basis and remain unverified in game (GitHub issue #1).</summary>
    public static (double[] Weights, (double X, double Y, double Z)[] ShiftedNodes, (double X, double Y, double Z) LerpedPoint) SpawnInputs(
        (double X, double Y, double Z) point,
        double planetRadius)
    {
        var n = BuildingClusterPlacement.Normalize(point);
        var (a, b) = BuildingClusterPlacement.CubeFaceBasis(point);
        var xAxis = BuildingClusterPlacement.Normalize(BuildingClusterPlacement.Cross(a, b));
        double along = Math.Abs(BuildingClusterPlacement.Dot(n, a));
        var cube = (n.X / along * planetRadius, n.Y / along * planetRadius, n.Z / along * planetRadius);
        double gx = BuildingClusterPlacement.Dot(cube, xAxis) / Cell;
        double gz = BuildingClusterPlacement.Dot(cube, b) / Cell;
        double i = Math.Floor(gx), j = Math.Floor(gz);
        double fx = gx / 4.0 - Math.Floor(gx / 4.0);
        double fz = gz / 4.0 - Math.Floor(gz / 4.0);
        double[] weights = [(1.0 - fx) * (1.0 - fz), (1.0 - fx) * fz, fx];
        (double X, double Y, double Z) CubePoint(double lx, double lz) => (
            a.X * planetRadius + xAxis.X * (lx * Cell) + b.X * (lz * Cell),
            a.Y * planetRadius + xAxis.Y * (lx * Cell) + b.Y * (lz * Cell),
            a.Z * planetRadius + xAxis.Z * (lx * Cell) + b.Z * (lz * Cell));
        (double X, double Y, double Z)[] nodes =
        [
            CubePoint(i - 1.0, j - 1.0),
            CubePoint(i - 1.0, j),
            CubePoint(i, j - 1.0),
        ];
        var lerped = CubePoint(i + fx, j + fz * (1.0 - fx));
        return (weights, nodes, lerped);
    }

    /// <summary>The Far-region spawn height above <paramref name="planetRadius"/> for a cluster building at
    /// <paramref name="buildingPoint"/> whose cluster site point is <paramref name="sitePoint"/>.</summary>
    public static (double SiteHeight, double SpawnHeight) SpawnHeight(
        PlanetTerrainData terrain,
        float planetRadius,
        (double X, double Y, double Z) sitePoint,
        (double X, double Y, double Z) buildingPoint)
    {
        var (weights, nodes, lerped) = SpawnInputs(buildingPoint, planetRadius);
        var pA = OnSphere(sitePoint, planetRadius);
        double hA = PlanetTerrain.Height(terrain, pA.X, pA.Y, pA.Z);
        var pAt = OnSphere(lerped, planetRadius);
        double bAt = PlanetTerrain.BaseHeight(terrain, pAt.X, pAt.Y, pAt.Z, planetRadius);
        double gridSum = 0;
        for (int k = 0; k < 3; k++)
        {
            var pNode = OnSphere(nodes[k], planetRadius);
            double bNode = PlanetTerrain.BaseHeight(terrain, pNode.X, pNode.Y, pNode.Z, planetRadius);
            gridSum += weights[k] * FarGridValue((float)(hA - bNode));
        }
        return (hA, gridSum * 4.0 + bAt);
    }

    /// <summary>Prepares a planet's shop layout, cluster member radii, terrain and floored solar-system centre from its
    /// portal code, galaxy and planet index.</summary>
    public static PlanetShopCabinetContext? PreparePlanet(
        ulong portalCode,
        int galaxy,
        int planetIndex,
        BiomeTables biomes,
        PlanetProfileTables profiles,
        IReadOnlyList<BuildingClassPlacement?> placement,
        BuildingLayoutTables layoutTables,
        SolarLayoutTables solarLayoutTables)
    {
        var types = PlanetGenerator.Generate(portalCode, galaxy, biomes);
        var seeds = SystemGenerator.Bodies(portalCode, galaxy).PlanetSeeds;
        if (planetIndex < 0 || planetIndex >= types.Length || planetIndex >= seeds.Length) return null;
        var body = types[planetIndex];
        ulong planetSeed = seeds[planetIndex];
        if (PlanetBuildingSites.Radius(body.BodyClass) is not { } planetRadius) return null;
        if (PlanetBuildingSites.FactsFor(portalCode, galaxy, planetIndex, biomes, profiles) is not { } facts) return null;
        if (profiles.Objects?.Terrain is not { } tt) return null;
        var attrs = SystemGenerator.Attributes(portalCode, galaxy);
        if (PlanetProfileGenerator.TerrainChoiceFor(planetSeed, body, (int)attrs.Star, profiles) is not { } choice) return null;
        if (PlanetTerrain.Generate(planetSeed, tt.Settings[choice.SettingIndex], choice, tt.SeaStandard, body.BodyClass == 3, body.DeadToWeird) is not { } terrain) return null;
        if (SolarLayoutGeneration.ForSystem(portalCode, galaxy, solarLayoutTables, placeMoons: true) is not { } solar
            || planetIndex >= solar.Centers.Length) return null;
        var rawCentre = solar.Centers[planetIndex];
        var planetCentre = (
            Math.Floor(rawCentre.X / 1000.0) * 1000.0,
            Math.Floor(rawCentre.Y / 1000.0) * 1000.0,
            Math.Floor(rawCentre.Z / 1000.0) * 1000.0);

        const int shopClass = 5;
        int raceIndex = attrs.Race switch
        {
            SystemRace.Gek => 0,
            SystemRace.Vykeen => 1,
            SystemRace.Korvax => 2,
            _ => -1,
        };
        if (raceIndex < 0) return null;
        int[] recordClasses = PlanetBuildingSites.RecordClasses(placement, layoutTables, facts, body.Biome);
        ulong[] recordSeeds = PlanetBuildingSites.RecordSeeds(planetSeed, recordClasses.Length);
        int firstShopRecord = Array.IndexOf(recordClasses, shopClass);
        int systemType = facts.HasNpcs ? 0 : 1;
        if (firstShopRecord < 0
            || BuildingLayoutGenerator.LSystemPath(layoutTables, raceIndex, shopClass, systemType) is not { } shopPath
            || BuildingLayoutGenerator.Generate(layoutTables, shopPath, BuildingLayoutGenerator.SampleSeed(recordSeeds[firstShopRecord], 0)) is not { WeaponCrate: not null } shopLayout
            || BuildingClusterPlacement.ClusterForClass(layoutTables, shopClass) is not { } shopCluster)
            return null;

        var recordRadii = new Dictionary<int, double>();
        foreach (int memberClass in shopCluster.Select(e => e.BuildingClass).Distinct())
        {
            int firstMemberRecord = Array.IndexOf(recordClasses, memberClass);
            if (firstMemberRecord < 0) continue;
            float overrideRadius = layoutTables.RecordRules is { } rr && memberClass >= 0 && memberClass < rr.Length
                ? rr[memberClass].OverrideRadius : 0f;
            string? memberPath = overrideRadius > 0f
                ? null
                : BuildingLayoutGenerator.LSystemPath(layoutTables, raceIndex, memberClass, systemType);
            int sampleOrdinal = 0;
            for (int r = firstMemberRecord; r < recordClasses.Length; r++)
            {
                if (recordClasses[r] != memberClass) continue;
                double radius = overrideRadius > 0f
                    ? overrideRadius
                    : memberPath is not null
                        ? BuildingLayoutGenerator.Generate(layoutTables, memberPath, BuildingLayoutGenerator.SampleSeed(recordSeeds[firstMemberRecord], sampleOrdinal))?.Radius ?? layoutTables.MinRadius
                        : layoutTables.MinRadius;
                recordRadii[r] = radius;
                sampleOrdinal++;
            }
        }
        return new PlanetShopCabinetContext(layoutTables, terrain, planetRadius, planetCentre, shopLayout, shopCluster, recordClasses, recordRadii);
    }

    /// <summary>Predicts the multi-tool cabinet position and seed for a shop site on a prepared planet.</summary>
    public static PlanetShopCabinet? ForShopSite(
        PlanetShopCabinetContext context,
        ulong siteKey,
        (double X, double Y, double Z) sitePoint,
        double edgeMargin = 0.05) =>
        ForShopSite(
            context.LayoutTables,
            context.Terrain,
            context.PlanetRadius,
            context.PlanetCentre,
            siteKey,
            sitePoint,
            context.ShopLayout,
            context.ClusterLayout,
            context.RecordClasses,
            context.RecordRadii,
            edgeMargin);

    /// <summary>Predicts the multi-tool cabinet position and seed for a shop site from its layout, cluster, terrain
    /// and floored planet centre.</summary>
    public static PlanetShopCabinet? ForShopSite(
        BuildingLayoutTables layoutTables,
        PlanetTerrainData terrain,
        float planetRadius,
        (double X, double Y, double Z) planetCentre,
        ulong siteKey,
        (double X, double Y, double Z) sitePoint,
        BuildingLayout shopLayout,
        IReadOnlyList<BuildingClusterEntry> clusterLayout,
        IReadOnlyList<int> recordClasses,
        IReadOnlyDictionary<int, double> recordRadii,
        double edgeMargin = 0.05)
    {
        if (shopLayout.WeaponCrate is not { } crateLocal) return null;
        int[] chosen = BuildingClusterPlacement.ChooseMembers(siteKey, clusterLayout, recordClasses, layoutTables.ChanceOfAddingShelter);
        var radii = new double[1 + chosen.Length];
        radii[0] = shopLayout.Radius;
        for (int i = 0; i < chosen.Length; i++)
            radii[i + 1] = recordRadii.TryGetValue(chosen[i], out double r) ? r : layoutTables.MinRadius;
        var memberPositions = BuildingClusterPlacement.PlaceMembers(sitePoint, radii);
        var allPositions = new (double X, double Y, double Z)[1 + memberPositions.Length];
        allPositions[0] = sitePoint;
        Array.Copy(memberPositions, 0, allPositions, 1, memberPositions.Length);
        var buildingPoint = BuildingClusterPlacement.Recentre(sitePoint, allPositions, radii)[0];
        var (siteHeight, spawnHeight) = SpawnHeight(terrain, planetRadius, sitePoint, buildingPoint);
        var (up, forward, side) = BuildingClusterPlacement.Facing(sitePoint, siteKey);
        var pDir = BuildingClusterPlacement.Normalize(buildingPoint);
        double rSpawn = planetRadius + spawnHeight;
        var crate = (
            X: pDir.X * rSpawn + side.X * crateLocal.X + up.X * crateLocal.Y + forward.X * crateLocal.Z + planetCentre.X,
            Y: pDir.Y * rSpawn + side.Y * crateLocal.X + up.Y * crateLocal.Y + forward.Y * crateLocal.Z + planetCentre.Y,
            Z: pDir.Z * rSpawn + side.Z * crateLocal.X + up.Z * crateLocal.Y + forward.Z * crateLocal.Z + planetCentre.Z);
        var rounded = ((int)Math.Floor(crate.X + 0.5), (int)Math.Floor(crate.Y + 0.5), (int)Math.Floor(crate.Z + 0.5));
        ulong seed = MultiToolSiteGeneration.SeedForPosition(new Vector3(rounded.Item1, rounded.Item2, rounded.Item3));
        ((int X, int Y, int Z) Point, ulong Seed)? alternate = null;
        double[] coords = [crate.X, crate.Y, crate.Z];
        int[] basePt = [rounded.Item1, rounded.Item2, rounded.Item3];
        for (int axis = 0; axis < 3; axis++)
        {
            double diff = (coords[axis] + 0.5) - Math.Floor(coords[axis] + 0.5);
            if (diff < edgeMargin || diff > 1.0 - edgeMargin)
            {
                int[] alt = [.. basePt];
                alt[axis] += diff < edgeMargin ? -1 : 1;
                var altPt = (alt[0], alt[1], alt[2]);
                alternate = (altPt, MultiToolSiteGeneration.SeedForPosition(new Vector3(altPt.Item1, altPt.Item2, altPt.Item3)));
                break;
            }
        }
        return new PlanetShopCabinet(siteKey, buildingPoint, siteHeight, spawnHeight, crate, rounded, seed, alternate);
    }

    private static Vector3 OnSphere((double X, double Y, double Z) q, float radius) =>
        Vector3.Normalize(new Vector3((float)q.X, (float)q.Y, (float)q.Z)) * radius;
}

