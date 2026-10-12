using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Stations the player hid from Space Stations' "Claims in progress" (they
/// don't plan to take them over). Kept in user.db (table hidden_stations,
/// UserDatabase migration step 6), keyed by system address (hex) like the
/// other system tables. Unhiding is a soft delete, like every user.db table.
/// </summary>
public static class HiddenStationsService
{
    private static readonly object Lock = new();
    private static HashSet<string>? _hidden;

    private static string Key(ulong systemAddress) => systemAddress.ToString("X", CultureInfo.InvariantCulture);

    private static HashSet<string> Hidden
    {
        get
        {
            lock (Lock)
            {
                if (_hidden is not null) return _hidden;
                _hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var connection = UserDatabase.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT system_address FROM hidden_stations WHERE deleted = 0";
                    using var r = cmd.ExecuteReader();
                    while (r.Read()) _hidden.Add(r.GetString(0));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HiddenStationsService] Couldn't read hidden stations: {ex.Message}");
                }
                return _hidden;
            }
        }
    }

    public static bool IsHidden(ulong systemAddress)
    {
        lock (Lock) return Hidden.Contains(Key(systemAddress));
    }

    public static void SetHidden(ulong systemAddress, bool hidden)
    {
        string key = Key(systemAddress);
        lock (Lock)
        {
            if (hidden) Hidden.Add(key);
            else Hidden.Remove(key);
        }

        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO hidden_stations (system_address, hidden_at, updated_at, source, synced_at, deleted)
                VALUES ($id, $now, $now, 'user', NULL, $deleted)
                ON CONFLICT(system_address) DO UPDATE SET
                    hidden_at = CASE WHEN $deleted = 0 THEN $now ELSE hidden_at END,
                    updated_at = $now, synced_at = NULL, deleted = $deleted";
            cmd.Parameters.AddWithValue("$id", key);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.Parameters.AddWithValue("$deleted", hidden ? 0 : 1);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HiddenStationsService] Couldn't save hidden station: {ex.Message}");
        }
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _hidden = null;
    }
}
