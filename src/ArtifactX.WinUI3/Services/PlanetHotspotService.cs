using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Predicts the hotspots around a point on a planet and the base computer spots that reach them, near the point and
    /// near the portal (HotspotPlanner, shared with the mobile app). Resource names are the catalog's.
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
        if (tables == null) return PlanetHotspotData.Empty;
        var itemNames = await CatalogService.GetItemNamesAsync();
        return await Task.Run(() => HotspotPlanner.Plan(planetAddress, centerLat, centerLon, tables,
            id => itemNames.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null,
            expandSearch, portalLat, portalLon));
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
