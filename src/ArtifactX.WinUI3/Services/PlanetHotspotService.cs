using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Predicts planetary deep-level resource hotspots (Power, Mineral, Gas) for any planet
/// based on the game's procedural algorithms (native 0x1412F4AC0 / HotspotNeighborhood).
/// </summary>
public static class PlanetHotspotService
{
    private static ExtractionWorldTables? _tables;
    private static readonly object Lock = new();

    private static readonly Dictionary<string, string> FallbackResourceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POWER"] = "Electromagnetic Power",
        ["LUSH1"] = "Paraffinium",
        ["TOXIC1"] = "Ammonia",
        ["HOT1"] = "Phosphorus",
        ["RADIO1"] = "Uranium",
        ["COLD1"] = "Dioxite",
        ["DUST1"] = "Pyrite",
        ["YELLOW2"] = "Copper",
        ["RED2"] = "Cadmium",
        ["GREEN2"] = "Emeril",
        ["BLUE2"] = "Indium",
        ["ASTEROID1"] = "Silver",
        ["ASTEROID2"] = "Gold",
        ["ASTEROID3"] = "Platinum",
        ["WATER1"] = "Salt",
        ["OXYGEN"] = "Oxygen",
        ["GAS1"] = "Nitrogen",
        ["GAS2"] = "Radon",
        ["GAS3"] = "Sulphurine",
        ["CATALYST1"] = "Sodium",
        ["CAVE1"] = "Cobalt",
        ["MAGNET"] = "Magnetised Ferrite",
    };

    /// <summary>Loads extraction and profile tables from the game catalog.</summary>
    public static async Task<ExtractionWorldTables?> GetTablesAsync()
    {
        if (_tables != null) return _tables;

        return await Task.Run(() =>
        {
            lock (Lock)
            {
                if (_tables != null) return _tables;

                string? dbPath = CatalogService.ResolveDbPath();
                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return null;

                try
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                    connection.Open();

                    T? Read<T>(string id) where T : class
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
                        command.Parameters.AddWithValue("$id", id);
                        var json = command.ExecuteScalar() as string;
                        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
                    }

                    if (Read<BiomeTables>("BIOMETABLES") is { } biomes
                        && Read<PlanetProfileTables>("PLANETPROFILE") is { } profiles
                        && Read<ExtractionTables>("EXTRACTIONTABLES") is { } extraction)
                    {
                        _tables = new ExtractionWorldTables(biomes, profiles, extraction);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlanetHotspotService] Load failed: {ex.Message}");
                }

                return _tables;
            }
        });
    }

    /// <summary>
    /// Predicts and returns hotspots around a specific latitude and longitude on the given planet.
    /// Evaluates the 4 surrounding poles (and optional neighboring rings) to discover all Power, Mineral,
    /// and Gas hotspots within analysis/survey range.
    /// </summary>
    public static async Task<PlanetHotspotData> GetHotspotDataAsync(
        ulong planetAddress,
        double centerLat,
        double centerLon,
        bool expandSearch = true,
        double? portalLat = null,
        double? portalLon = null)
    {
        var tables = await GetTablesAsync();
        if (tables == null) return new PlanetHotspotData(Array.Empty<PlanetHotspot>(), Array.Empty<PlanetHotspotCluster>());

        var itemNames = await CatalogService.GetItemNamesAsync();

        return await Task.Run(() =>
        {
            int planet = (int)((planetAddress >> 52) & 15);
            int galaxy = NmsSpaceStationPaths.GalaxyNumber(planetAddress) - 1;
            if (planet <= 0) return new PlanetHotspotData(Array.Empty<PlanetHotspot>(), Array.Empty<PlanetHotspotCluster>());

            ulong portalCode;
            try
            {
                portalCode = ulong.Parse(NmsSpaceStationPaths.PortalAddress(planetAddress, planet), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            catch
            {
                return new PlanetHotspotData(Array.Empty<PlanetHotspot>(), Array.Empty<PlanetHotspotCluster>());
            }

            var types = PlanetGenerator.Generate(portalCode, galaxy, tables.Biomes);
            var bodies = SystemGenerator.Bodies(portalCode, galaxy);
            int index = planet - 1;
            if (index < 0 || index >= types.Length || index >= bodies.PlanetSeeds.Length)
                return new PlanetHotspotData(Array.Empty<PlanetHotspot>(), Array.Empty<PlanetHotspotCluster>());

            float radius = PlanetBuildingSites.Radius(types[index].BodyClass) ?? 98304f;
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

            // Compute target 3D point from center (latitude, longitude)
            double latRad = centerLat * Math.PI / 180.0;
            double lonRad = centerLon * Math.PI / 180.0;
            Vector3 targetDir = new(
                (float)(Math.Cos(latRad) * Math.Sin(lonRad)),
                (float)Math.Sin(latRad),
                (float)(Math.Cos(latRad) * Math.Cos(lonRad))
            );
            Vector3 targetPoint = targetDir * radius;

            Vector3? portalDir = null;
            if (portalLat.HasValue && portalLon.HasValue)
            {
                double pLatRad = portalLat.Value * Math.PI / 180.0;
                double pLonRad = portalLon.Value * Math.PI / 180.0;
                portalDir = new Vector3(
                    (float)(Math.Cos(pLatRad) * Math.Sin(pLonRad)),
                    (float)Math.Sin(pLatRad),
                    (float)(Math.Cos(pLatRad) * Math.Cos(pLonRad))
                );
            }

            var categories = tables.Extraction.Hotspots.Select(h => h.Parameters).ToArray();
            var candidates = new List<HotspotCandidate>();

            // 1. Immediate neighborhood (4 surrounding poles)
            candidates.AddRange(HotspotNeighborhood.Around(planetAddress, targetPoint, radius, tables.Extraction.Rules, categories));

            // 2. If expandSearch requested, query adjacent poles for a ~3-5km coverage area
            if (expandSearch)
            {
                var angles = HotspotOrientation.Inverse(HotspotOrientation.Direction(planetAddress), targetPoint);
                int divisions = (int)(radius * MathF.PI / tables.Extraction.Rules.Spacing + 0.5f);
                int latitude = (int)(divisions * angles.Latitude / MathF.PI);
                float circumference = radius * 6.2831855f;

                int[] poleLatitudes = { latitude - 1, latitude + 2 };
                foreach (int pLat in poleLatitudes)
                {
                    if (pLat < 0 || pLat > divisions) continue;
                    float ring = GameTrig.Sin(pLat * MathF.PI / divisions) * circumference;
                    if (ring <= 0) continue;

                    int maxB = (int)(ring / tables.Extraction.Rules.Spacing);
                    if (maxB <= 0) continue;

                    float scaled = ring * angles.Longitude / 6.2831855f / tables.Extraction.Rules.Spacing;
                    int baseB = (int)scaled;

                    for (int bOffset = -1; bOffset <= 2; bOffset++)
                    {
                        int pLon = (baseB + bOffset) % maxB;
                        if (pLon < 0) pLon += maxB;
                        candidates.AddRange(HotspotPole.Generate(planetAddress, radius, pLat, pLon, tables.Extraction.Rules, categories));
                    }
                }
            }

            // Deduplicate candidates by spatial proximity
            var unique = new List<HotspotCandidate>();
            foreach (var c in candidates)
            {
                if (!unique.Any(u => Vector3.DistanceSquared(u.Direction, c.Direction) < 0.0000001f))
                {
                    unique.Add(c);
                }
            }

            // Map candidates to PlanetHotspot records
            var results = new List<PlanetHotspot>(unique.Count);
            foreach (var c in unique)
            {
                double cLat = Math.Asin(Math.Clamp(c.Direction.Y, -1f, 1f)) * (180.0 / Math.PI);
                double cLon = Math.Atan2(c.Direction.X, c.Direction.Z) * (180.0 / Math.PI);

                float dot = Math.Clamp(Vector3.Dot(targetDir, c.Direction), -1f, 1f);
                double distanceMeters = Math.Acos(dot) * radius;

                string kind = c.Attributes.Flag == 1 ? "Power" : (c.Attributes.Flag & 14) != 0 ? "Mineral" : "Gas";
                string resourceId = c.Attributes.Flag == 1 ? "POWER" : tables.Extraction.ResourceForFlag(resources, c.Attributes.Flag);
                if (string.IsNullOrEmpty(resourceId)) resourceId = "UNKNOWN";

                string resourceName = FallbackResourceNames.TryGetValue(resourceId, out var fallback)
                    ? fallback
                    : itemNames.TryGetValue(resourceId, out var catalogName) ? catalogName : resourceId;

                string grade = c.Attributes.Class switch
                {
                    3 => "S",
                    2 => "A",
                    1 => "B",
                    _ => "C"
                };

                int yieldPercent = c.Attributes.Class switch
                {
                    3 => 100,
                    2 => 80,
                    1 => 60,
                    _ => 40
                };

                string distanceLabel = distanceMeters < 1000
                    ? $"{Math.Round(distanceMeters)}u away"
                    : $"{distanceMeters / 1000.0:F1} km away";

                double? portalDistance = null;
                string? portalDistanceLabel = null;
                if (portalDir.HasValue)
                {
                    float dotPortal = Math.Clamp(Vector3.Dot(portalDir.Value, c.Direction), -1f, 1f);
                    double pDist = Math.Acos(dotPortal) * radius;
                    portalDistance = pDist;
                    portalDistanceLabel = pDist < 1000
                        ? $"{Math.Round(pDist)}u from portal"
                        : $"{pDist / 1000.0:F1} km from portal";
                }

                ulong candidateKey = (ulong)c.Attributes.FinalSeed
                    ^ ((ulong)Math.Round(Math.Abs(cLat) * 1000) << 32)
                    ^ (ulong)Math.Round(Math.Abs(cLon) * 1000);

                float h = terrain != null ? PlanetTerrain.Height(terrain, c.Direction.X * radius, c.Direction.Y * radius, c.Direction.Z * radius) : 0f;
                float surfaceRadius = radius + h;
                Vector3 localPos = c.Direction * surfaceRadius;

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
                    DistanceLabel: distanceLabel,
                    PortalDistance: portalDistance,
                    PortalDistanceLabel: portalDistanceLabel,
                    CandidateKey: candidateKey,
                    LocalPosition: localPos
                ));
            }

            // Sort nearest first
            var sorted = (IReadOnlyList<PlanetHotspot>)results.OrderBy(r => r.Distance).ToList();
            var clusters = PlanetHotspotCluster.Find(sorted, maxBaseRadius: 375.0, maxSpan: 650.0, portalLat, portalLon, terrain, radius);
            return new PlanetHotspotData(sorted, clusters);
        });
    }

    /// <summary>
    /// Predicts and returns hotspots around a specific latitude and longitude on the given planet.
    /// </summary>
    public static async Task<IReadOnlyList<PlanetHotspot>> GetHotspotsAsync(
        ulong planetAddress,
        double centerLat,
        double centerLon,
        bool expandSearch = true,
        double? portalLat = null,
        double? portalLon = null)
    {
        var data = await GetHotspotDataAsync(planetAddress, centerLat, centerLon, expandSearch, portalLat, portalLon);
        return data.Hotspots;
    }

    /// <summary>
    /// Finds multi-hotspot clusters (pairs and triplets) within base building range.
    /// Calculates the optimal Base Computer coordinate (minimum enclosing circle) for each cluster.
    /// </summary>
    public static IReadOnlyList<PlanetHotspotCluster> FindClusters(
        IReadOnlyList<PlanetHotspot> hotspots,
        double maxBaseRadius = 375.0,
        double maxSpan = 650.0,
        double? portalLat = null,
        double? portalLon = null) =>
        PlanetHotspotCluster.Find(hotspots, maxBaseRadius, maxSpan, portalLat, portalLon);
}

public sealed record PlanetHotspotData(
    IReadOnlyList<PlanetHotspot> Hotspots,
    IReadOnlyList<PlanetHotspotCluster> Clusters);

