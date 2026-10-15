using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Where fish were caught. The save's fishing record (NmsFishingPaths) only
/// keeps, per fish, how many were caught and the largest - no planet, no date
/// - so this learns it going forward (user request 2026-10-13): FishCatchRecorder
/// (app-wide, every page) hands each new save to <see cref="Observe"/>, and when a
/// fish's count has gone up while the player is on a planet, the catch is
/// recorded against that planet in user.db (table fish_catches, step 10).
///
/// Safeguards against crediting the wrong planet:
///  - the starting counts are taken from the first save seen in this session,
///    so catches made while ArtifactX was closed are never guessed at;
///  - a catch only counts if that fish can live on the planet's type (a fish
///    caught on a Lush planet and saved after flying to a Frozen one is left
///    out rather than put on the Frozen planet);
///  - nothing is recorded in space (planet 0).
/// So the record is accurate but not complete - pages say so.
///
/// Each catch also keeps the player's coordinates on the planet (2026-10-14,
/// NmsPlanetCoordinates) - where they were when the game saved after the
/// catch, which is near the spot unless they moved on before it saved.
/// </summary>
public static class FishCatchService
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, Dictionary<string, uint>> Baselines = new();
    private static Dictionary<string, Dictionary<string, int>>? _byPlanet;

    private static string PlanetKey(ulong system, int planet) =>
        $"{system.ToString("X", CultureInfo.InvariantCulture)}|{planet}";

    /// <summary>The save's fishing record as fish id -> (times caught, largest).</summary>
    public static Dictionary<string, (uint Count, float Largest)> ReadCaught(JToken? records)
    {
        var caught = new Dictionary<string, (uint, float)>(StringComparer.OrdinalIgnoreCase);
        if (records?["5gB"] is not JArray ids) return caught;
        var counts = records["yv6"] as JArray;
        var largest = records["CXv"] as JArray;
        for (int i = 0; i < ids.Count; i++)
        {
            string id = CatalogService.NormalizeId(ids[i]?.Value<string>() ?? "");
            if (id.Length == 0) continue;
            uint count = counts is not null && i < counts.Count ? counts[i].Value<uint>() : 0;
            float size = largest is not null && i < largest.Count ? largest[i].Value<float>() : 0;
            caught[id] = (count, size);
        }
        return caught;
    }

    /// <summary>Looks at a new save. Returns whether a catch was recorded.</summary>
    /// <param name="saveKey">Which save slot this came from (counts are per slot).</param>
    /// <param name="fish">The fish table, to check a fish can live on the planet.</param>
    public static bool Observe(string saveKey, PlanetSnapshot snapshot, IReadOnlyList<FishInfo> fish)
    {
        // A missing record counts as "nothing caught yet", so the first catches
        // on a brand-new save aren't swallowed as the starting point.
        if (saveKey.Length == 0) return false;
        var counts = ReadCaught(snapshot.Fishing).ToDictionary(c => c.Key, c => c.Value.Count, StringComparer.OrdinalIgnoreCase);

        // The starting point only ever moves up. Pages hand in the copy of the
        // save loaded when ArtifactX started as their first look; letting that
        // reset the counts made the next save count earlier catches again
        // (found 2026-10-14).
        Dictionary<string, uint>? before;
        lock (Lock)
        {
            Baselines.TryGetValue(saveKey, out before);
            var merged = new Dictionary<string, uint>(counts, StringComparer.OrdinalIgnoreCase);
            if (before is not null)
                foreach (var (id, was) in before)
                    if (!merged.TryGetValue(id, out var now) || now < was) merged[id] = was;
            Baselines[saveKey] = merged;
        }
        if (before is null || fish.Count == 0) return false; // first look: just remember

        ulong? system = NmsSpaceStationPaths.ReadSystemAddress(snapshot.Universe);
        int planet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        if (system is not ulong here || planet <= 0) return false;
        int? biome = NmsPlanets.ReadAll(snapshot.Discoveries)
            .FirstOrDefault(p => p.SystemAddress == here && p.PlanetIndex == planet)?.BiomeIndex;
        if (biome is not int type) return false;

        var fishById = fish.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);
        var caughtNow = counts
            .Select(c => (Id: c.Key, More: (int)c.Value - (int)(before.TryGetValue(c.Key, out var was) ? was : 0)))
            .Where(c => c.More > 0 && fishById.TryGetValue(c.Id, out var info) && info.LivesOn(type))
            .ToList();
        if (caughtNow.Count == 0) return false;
        var spot = snapshot.Coordinates;

        try
        {
            using var connection = UserDatabase.Open();
            using var tx = connection.BeginTransaction();
            string now = UserDatabase.Now();
            foreach (var (id, more) in caughtNow)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO fish_catches (id, system_address, planet_index, fish_id, count, caught_at, updated_at, source, synced_at, deleted, added_by, latitude, longitude)
                    VALUES ($id, $s, $p, $fish, $count, $now, $now, 'save', NULL, 0, $added_by, $lat, $lon)";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
                cmd.Parameters.AddWithValue("$s", here.ToString("X", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$p", planet);
                cmd.Parameters.AddWithValue("$fish", id);
                cmd.Parameters.AddWithValue("$count", more);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
                cmd.Parameters.AddWithValue("$lat", spot is { } a ? a.Latitude : DBNull.Value);
                cmd.Parameters.AddWithValue("$lon", spot is { } b ? b.Longitude : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FishCatchService] Couldn't record catches: {ex.Message}");
            return false;
        }

        // A catch proves there's water: record it, and correct a "no water"
        // mark - it would otherwise be wrong for everyone once notes are shared
        // through the API (user, 2026-10-13).
        if (PlanetWaterService.Get(here, planet) != true)
            PlanetWaterService.Set(here, planet, true);

        lock (Lock)
        {
            var tally = ByPlanet.TryGetValue(PlanetKey(here, planet), out var t) ? t
                : ByPlanet[PlanetKey(here, planet)] = new(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, more) in caughtNow)
                tally[id] = (tally.TryGetValue(id, out var n) ? n : 0) + more;
        }
        return true;
    }

    private static Dictionary<string, Dictionary<string, int>> ByPlanet
    {
        get
        {
            if (_byPlanet is not null) return _byPlanet;
            _byPlanet = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var connection = UserDatabase.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT system_address, planet_index, fish_id, SUM(count) FROM fish_catches WHERE deleted = 0 GROUP BY 1, 2, 3";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string key = $"{r.GetString(0)}|{r.GetInt32(1)}";
                    if (!_byPlanet.TryGetValue(key, out var tally)) _byPlanet[key] = tally = new(StringComparer.OrdinalIgnoreCase);
                    tally[r.GetString(2)] = r.GetInt32(3);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FishCatchService] Couldn't read catches: {ex.Message}");
            }
            return _byPlanet;
        }
    }

    /// <summary>Fish recorded as caught on this planet, with how many.</summary>
    public static IReadOnlyDictionary<string, int> CaughtOn(ulong system, int planet)
    {
        lock (Lock)
            return ByPlanet.TryGetValue(PlanetKey(system, planet), out var tally)
                ? new Dictionary<string, int>(tally, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>();
    }

    /// <summary>Where on this planet each fish was caught, newest first
    /// (catches recorded before coordinates were kept have none).</summary>
    public static Dictionary<string, List<(double Latitude, double Longitude)>> SpotsOn(ulong system, int planet)
    {
        var spots = new Dictionary<string, List<(double, double)>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT fish_id, latitude, longitude FROM fish_catches
                WHERE deleted = 0 AND system_address = $s AND planet_index = $p AND latitude IS NOT NULL AND longitude IS NOT NULL
                ORDER BY caught_at DESC";
            cmd.Parameters.AddWithValue("$s", system.ToString("X", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$p", planet);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!spots.TryGetValue(r.GetString(0), out var list)) spots[r.GetString(0)] = list = new();
                list.Add((r.GetDouble(1), r.GetDouble(2)));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FishCatchService] Couldn't read catch spots: {ex.Message}");
        }
        return spots;
    }

    /// <summary>Every planet a fish was recorded caught on.</summary>
    public static List<(ulong System, int Planet, int Count)> WhereCaught(string fishId)
    {
        lock (Lock)
            return ByPlanet
                .Where(kv => kv.Value.ContainsKey(fishId))
                .Select(kv =>
                {
                    var parts = kv.Key.Split('|');
                    return (ulong.Parse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), kv.Value[fishId]);
                })
                .ToList();
    }

    /// <summary>A stable key for the active save slot.</summary>
    public static string ActiveSaveKey() =>
        SaveSessionManager.ActiveSlot?.Files.FirstOrDefault()?.FullPath ?? "";

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _byPlanet = null;
    }
}
