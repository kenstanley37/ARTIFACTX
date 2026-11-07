using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Whether a planet has water to fish in, as the player found out on the spot
/// (the save doesn't record it). Kept in user.db (table planet_water,
/// UserDatabase migration step 4), keyed like planet_names: system address
/// (hex) + planet index. Clearing is a soft delete, like every user.db table.
/// </summary>
public static class PlanetWaterService
{
    private static readonly object Lock = new();
    private static Dictionary<string, bool>? _water;

    /// <summary>Raised when a planet's water status is recorded, updated, or reloaded.</summary>
    public static event Action<ulong, int>? Changed;

    private static string Key(ulong systemAddress, int planet) =>
        $"{systemAddress.ToString("X", CultureInfo.InvariantCulture)}|{planet}";

    private static Dictionary<string, bool> Water
    {
        get
        {
            lock (Lock)
            {
                if (_water is not null) return _water;
                _water = new(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var connection = UserDatabase.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT system_address, planet_index, has_water FROM planet_water WHERE deleted = 0";
                    using var r = cmd.ExecuteReader();
                    while (r.Read()) _water[$"{r.GetString(0)}|{r.GetInt32(1)}"] = r.GetInt32(2) != 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlanetWaterService] Couldn't read planet water: {ex.Message}");
                }
                return _water;
            }
        }
    }

    /// <summary>True/false when the player marked it; null when unknown.</summary>
    public static bool? Get(ulong systemAddress, int planet)
    {
        lock (Lock) return Water.TryGetValue(Key(systemAddress, planet), out var has) ? has : null;
    }

    /// <summary>Records what the player found; null clears the mark.</summary>
    public static void Set(ulong systemAddress, int planet, bool? hasWater, string source = "user")
    {
        string key = Key(systemAddress, planet);
        lock (Lock)
        {
            if (hasWater is bool has) Water[key] = has;
            else Water.Remove(key);
        }

        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO planet_water (system_address, planet_index, has_water, updated_at, source, synced_at, deleted, added_by)
                VALUES ($system, $planet, $has, $now, $source, NULL, $deleted, $added_by)
                ON CONFLICT(system_address, planet_index) DO UPDATE SET
                    has_water = $has, updated_at = $now, source = $source, synced_at = NULL, deleted = $deleted";
            cmd.Parameters.AddWithValue("$source", source);
            cmd.Parameters.AddWithValue("$system", systemAddress.ToString("X", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$planet", planet);
            cmd.Parameters.AddWithValue("$has", hasWater == true ? 1 : 0);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
            cmd.Parameters.AddWithValue("$deleted", hasWater is null ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlanetWaterService] Couldn't save planet water: {ex.Message}");
        }

        Changed?.Invoke(systemAddress, planet);
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _water = null;
        Changed?.Invoke(0, 0);
    }
}
