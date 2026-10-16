using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Stations the player hid from Space Stations' "Claims in progress" (they
/// don't plan to take them over). A claim belongs to one save, so hiding is
/// per game account + save slot (table hidden_station_claims, user.db step 7).
/// Hides made before that (table hidden_stations, step 6) hid the station on
/// every save; they still do until unhidden, which then clears them too.
/// Unhiding is a soft delete, like every user.db table.
/// </summary>
public static class HiddenStationsService
{
    private static readonly object Lock = new();
    private static HashSet<string>? _everywhere;
    private static HashSet<string>? _perSave;

    private static string Key(ulong systemAddress) => systemAddress.ToString("X", CultureInfo.InvariantCulture);

    /// <summary>"account|slot" of the save in use, or null when none is loaded.</summary>
    private static string? SaveKey =>
        SaveSessionManager.ActiveSlot is { } slot && GameAccountsService.ActiveKey is { Length: > 0 } account
            ? $"{account}|{slot.SlotId}" : null;

    public static bool IsHidden(ulong systemAddress)
    {
        string key = Key(systemAddress);
        lock (Lock)
        {
            Load();
            return _everywhere!.Contains(key) || (SaveKey is string save && _perSave!.Contains($"{save}|{key}"));
        }
    }

    public static void SetHidden(ulong systemAddress, bool hidden)
    {
        string key = Key(systemAddress);
        string account = GameAccountsService.ActiveKey;
        int slot = SaveSessionManager.ActiveSlot?.SlotId ?? 0;
        bool clearEverywhere;
        lock (Lock)
        {
            Load();
            if (hidden) _perSave!.Add($"{account}|{slot}|{key}");
            else _perSave!.Remove($"{account}|{slot}|{key}");
            clearEverywhere = !hidden && _everywhere!.Remove(key);
        }

        try
        {
            using var connection = UserDatabase.Open();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO hidden_station_claims (game_account_id, slot, system_address, hidden_at, updated_at, source, synced_at, deleted)
                    VALUES ($account, $slot, $id, $now, $now, 'user', NULL, $deleted)
                    ON CONFLICT(game_account_id, slot, system_address) DO UPDATE SET
                        hidden_at = CASE WHEN $deleted = 0 THEN $now ELSE hidden_at END,
                        updated_at = $now, synced_at = NULL, deleted = $deleted";
                cmd.Parameters.AddWithValue("$account", account);
                cmd.Parameters.AddWithValue("$slot", slot);
                cmd.Parameters.AddWithValue("$id", key);
                cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
                cmd.Parameters.AddWithValue("$deleted", hidden ? 0 : 1);
                cmd.ExecuteNonQuery();
            }
            if (clearEverywhere)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE hidden_stations SET deleted = 1, updated_at = $now, synced_at = NULL WHERE system_address = $id";
                cmd.Parameters.AddWithValue("$id", key);
                cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HiddenStationsService] Couldn't save hidden station: {ex.Message}");
        }
    }

    /// <summary>Drops the cache (after a restore).</summary>
    public static void Reload()
    {
        lock (Lock) { _everywhere = null; _perSave = null; }
    }

    private static void Load()
    {
        if (_everywhere is not null && _perSave is not null) return;
        _everywhere = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _perSave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var connection = UserDatabase.Open();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT system_address FROM hidden_stations WHERE deleted = 0";
                using var r = cmd.ExecuteReader();
                while (r.Read()) _everywhere.Add(r.GetString(0));
            }
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT game_account_id, slot, system_address FROM hidden_station_claims WHERE deleted = 0";
                using var r = cmd.ExecuteReader();
                while (r.Read()) _perSave.Add($"{r.GetString(0)}|{r.GetInt32(1)}|{r.GetString(2)}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HiddenStationsService] Couldn't read hidden stations: {ex.Message}");
        }
    }
}
