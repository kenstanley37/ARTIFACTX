using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace ArtifactX.Core.Recipes;

/// <summary>Predicted hotspots, multi-hotspot base clusters, and base spots that also reach the portal (#63) with a
/// note when there are none (or the tables predate the base radius).</summary>
public sealed record PlanetHotspotData(
    IReadOnlyList<PlanetHotspot> Hotspots,
    IReadOnlyList<PlanetHotspotCluster> Clusters,
    IReadOnlyList<PlanetHotspotCluster>? PortalSites = null,
    string? PortalNote = null,
    float BaseReach = 375f)
{
    public static readonly PlanetHotspotData Empty = new([], []);
}

/// <summary>
/// Predicts a planet's deep-level resource hotspots (Power, Mineral, Gas) around a point, from the address and the
/// game's procedural rules (native 0x1412F4AC0 / HotspotNeighborhood), and the base computer spots that reach them -
/// near the point, and near the portal. Pure: tables and names come in, so the desktop (PlanetHotspotService) and the
/// mobile app (its rules file) give the same answer.
/// </summary>
public static class HotspotPlanner
{
    /// <summary>The power hotspot has no product or substance of its own in the game's tables, so no item name; this is
    /// the name the desktop has shown for it since hotspots were added (#63).</summary>
    public const string PowerName = "Electromagnetic Power";

    /// <param name="planetAddress">A planet's packed address (planet number in bits 52-55).</param>
    /// <param name="nameOf">A resource id's display name, or null when unknown.</param>
    public static PlanetHotspotData Plan(ulong planetAddress, double centerLat, double centerLon, ExtractionWorldTables tables,
        Func<string, string?> nameOf, bool expandSearch = true, double? portalLat = null, double? portalLon = null)
    {
        int planet = (int)((planetAddress >> 52) & 15);
        int galaxy = NmsSpaceStationPaths.GalaxyNumber(planetAddress) - 1;
        if (planet <= 0) return PlanetHotspotData.Empty;

        ulong portalCode;
        try
        {
            portalCode = ulong.Parse(NmsSpaceStationPaths.PortalAddress(planetAddress, planet), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return PlanetHotspotData.Empty;
        }

        var types = PlanetGenerator.Generate(portalCode, galaxy, tables.Biomes);
        var bodies = SystemGenerator.Bodies(portalCode, galaxy);
        int index = planet - 1;
        if (index < 0 || index >= types.Length || index >= bodies.PlanetSeeds.Length) return PlanetHotspotData.Empty;

        // No surface (a true gas giant) or an unmeasured size: no hotspots rather than a guessed radius.
        if (PlanetBuildingSites.SurfaceRadius(types[index].BodyClass, types[index].Biome) is not { } radius) return PlanetHotspotData.Empty;
        var system = SystemGenerator.Attributes(portalCode, galaxy);
        var body = types[index];
        var profile = PlanetProfileGenerator.Generate(bodies.PlanetSeeds[index], index, body, (int)system.Star,
            PlanetGenerator.GalaxyType(galaxy), system.GasGiantPlanet, tables.Profiles with { Objects = null }, system.Uncharted, system.Abandoned);
        var resources = tables.Extraction.ResourcesFor(profile, body.Biome);

        var terrainTables = tables.Profiles.Objects?.Terrain;
        var choice = terrainTables is null ? null : PlanetProfileGenerator.TerrainChoiceFor(bodies.PlanetSeeds[index], body, (int)system.Star, tables.Profiles);
        var terrain = choice is null || choice.SettingIndex >= terrainTables!.Settings.Length ? null
            : PlanetTerrain.Generate(bodies.PlanetSeeds[index], terrainTables.Settings[choice.SettingIndex], choice, terrainTables.SeaStandard,
                body.BodyClass == 3, body.DeadToWeird);

        Vector3 targetDir = Direction(centerLat, centerLon);
        Vector3 targetPoint = targetDir * radius;
        Vector3? portalDir = portalLat is { } pLat && portalLon is { } pLon ? Direction(pLat, pLon) : null;

        var rules = tables.Extraction.Rules;
        var categories = tables.Extraction.Hotspots.Select(h => h.Parameters).ToArray();
        var candidates = new List<HotspotCandidate>();

        Gather(targetPoint);
        // Base spots near the portal need the hotspots around the portal, wherever the reference point is.
        if (portalDir is { } portalAround) Gather(portalAround * radius);

        void Gather(Vector3 point)
        {
            // 1. Immediate neighbourhood (4 surrounding poles)
            candidates.AddRange(HotspotNeighborhood.Around(planetAddress, point, radius, rules, categories));
            if (!expandSearch) return;

            // 2. The adjacent poles, for a ~3-5 km coverage area.
            var angles = HotspotOrientation.Inverse(HotspotOrientation.Direction(planetAddress), point);
            int divisions = (int)(radius * MathF.PI / rules.Spacing + 0.5f);
            int latitude = (int)(divisions * angles.Latitude / MathF.PI);
            float circumference = radius * 6.2831855f;
            foreach (int poleLat in new[] { latitude - 1, latitude + 2 })
            {
                if (poleLat < 0 || poleLat > divisions) continue;
                float ring = GameTrig.Sin(poleLat * MathF.PI / divisions) * circumference;
                if (ring <= 0) continue;
                int maxB = (int)(ring / rules.Spacing);
                if (maxB <= 0) continue;
                int baseB = (int)(ring * angles.Longitude / 6.2831855f / rules.Spacing);
                for (int bOffset = -1; bOffset <= 2; bOffset++)
                {
                    int poleLon = (baseB + bOffset) % maxB;
                    if (poleLon < 0) poleLon += maxB;
                    candidates.AddRange(HotspotPole.Generate(planetAddress, radius, poleLat, poleLon, rules, categories));
                }
            }
        }

        // The same hotspot found from two poles counts once.
        var unique = new List<HotspotCandidate>();
        foreach (var c in candidates)
            if (!unique.Any(u => Vector3.DistanceSquared(u.Direction, c.Direction) < 0.0000001f))
                unique.Add(c);

        var results = new List<PlanetHotspot>(unique.Count);
        foreach (var c in unique)
        {
            double cLat = Math.Asin(Math.Clamp(c.Direction.Y, -1f, 1f)) * (180.0 / Math.PI);
            double cLon = Math.Atan2(c.Direction.X, c.Direction.Z) * (180.0 / Math.PI);
            double distanceMeters = PlanetHotspotCluster.GroundDistance(targetDir, c.Direction, radius);

            string kind = c.Attributes.Flag == 1 ? "Power" : (c.Attributes.Flag & 14) != 0 ? "Mineral" : "Gas";
            string resourceId = c.Attributes.Flag == 1 ? "POWER" : tables.Extraction.ResourceForFlag(resources, c.Attributes.Flag);
            if (string.IsNullOrEmpty(resourceId)) resourceId = "UNKNOWN";
            string resourceName = c.Attributes.Flag == 1 ? nameOf(resourceId) ?? PowerName
                : nameOf(resourceId) is { Length: > 0 } name ? name : $"{kind} hotspot";

            string grade = c.Attributes.Class switch { 3 => "S", 2 => "A", 1 => "B", _ => "C" };
            int yieldPercent = c.Attributes.Class switch { 3 => 100, 2 => 80, 1 => 60, _ => 40 };

            double? portalDistance = null;
            string? portalDistanceLabel = null;
            if (portalDir.HasValue)
            {
                portalDistance = PlanetHotspotCluster.GroundDistance(portalDir.Value, c.Direction, radius);
                portalDistanceLabel = $"{FormatDistance(portalDistance.Value)} from portal";
            }

            ulong candidateKey = (ulong)c.Attributes.FinalSeed
                ^ ((ulong)Math.Round(Math.Abs(cLat) * 1000) << 32)
                ^ (ulong)Math.Round(Math.Abs(cLon) * 1000);

            float h = terrain != null ? PlanetTerrain.SurfaceHeight(terrain, c.Direction.X * radius, c.Direction.Y * radius, c.Direction.Z * radius, radius) : 0f;

            results.Add(new PlanetHotspot(
                PlanetAddress: planetAddress,
                Kind: kind,
                ResourceId: resourceId,
                ResourceName: resourceName,
                Class: grade,
                ClassGrade: c.Attributes.Class,
                Strength: c.Attributes.Strength,
                YieldPercent: yieldPercent,
                Range: c.Attributes.Range,
                Latitude: cLat,
                Longitude: cLon,
                Coordinates: NmsPlanetCoordinates.Format(cLat, cLon),
                Distance: distanceMeters,
                DistanceLabel: $"{FormatDistance(distanceMeters)} away",
                PortalDistance: portalDistance,
                PortalDistanceLabel: portalDistanceLabel,
                CandidateKey: candidateKey,
                LocalPosition: c.Direction * (radius + h)));
        }

        var sorted = (IReadOnlyList<PlanetHotspot>)results.OrderBy(r => r.Distance).ToList();
        // A new base's boundary from the catalog (format 105); older tables keep the earlier 375 for combos.
        float? reach = tables.Extraction.PlanetBaseRadius;
        var clusters = PlanetHotspotCluster.Find(sorted, maxBaseRadius: reach ?? 375.0, maxSpan: 2 * (reach ?? 325.0),
            portalLat, portalLon, terrain, radius);
        IReadOnlyList<PlanetHotspotCluster> portalSites = [];
        string? portalNote = null;
        if (portalDir is { } toPortal)
        {
            if (reach is not { } baseReach)
                portalNote = "Update your game data (Settings) to find base spots near the portal.";
            else
            {
                portalSites = PortalBaseSites.Find(sorted, toPortal, baseReach, radius, terrain);
                if (portalSites.Count == 0)
                    portalNote = PortalBaseSites.NearestPower(sorted, toPortal, radius) is { } power
                        ? $"No hotspot is close enough to the portal for one base (a base reaches {baseReach:0}u from its computer). "
                          + $"The nearest power hotspot is {FormatDistance(power.Distance)} from the portal, at {power.Hotspot.Coordinates}."
                        : $"No hotspot is close enough to the portal for one base (a base reaches {baseReach:0}u from its computer).";
            }
        }
        return new PlanetHotspotData(sorted, clusters, portalSites, portalNote, reach ?? 375f);
    }

    /// <summary>A unit vector for Analysis Visor coordinates (the game's planet axes).</summary>
    private static Vector3 Direction(double latitude, double longitude)
    {
        double lat = latitude * Math.PI / 180.0, lon = longitude * Math.PI / 180.0;
        return new Vector3((float)(Math.Cos(lat) * Math.Sin(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Cos(lon)));
    }

    public static string FormatDistance(double metres) => metres < 1000 ? $"{Math.Round(metres)}u" : $"{metres / 1000.0:F1} km";
}
