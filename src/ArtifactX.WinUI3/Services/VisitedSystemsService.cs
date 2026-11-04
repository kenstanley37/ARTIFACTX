using ArtifactX.Core.NmsModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Remembers every system a save has been seen to know (user.db visited_systems). The
/// game's local discovery list keeps only so many visits: an old system can look
/// unvisited until it's scanned again (owner, 2026-11-03; Bug 58), so Regions counts the
/// save's list together with this history. Fed by the app-wide save feed
/// (FishCatchRecorder): each snapshot's discovered systems plus the current one. Per
/// save (account and slot, and expedition while one runs). How many visits the game
/// keeps, and which it drops first, isn't measured yet.
/// </summary>
public static class VisitedSystemsService
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, HashSet<ulong>> Known = new(StringComparer.Ordinal);

    /// <summary>The active save's key, or "" when no save is loaded.</summary>
    public static string ActiveSaveKey() =>
        SaveSessionManager.ActiveSlot is { } slot && GameAccountsService.ActiveKey is { Length: > 0 } account
            ? $"{account}/{slot.SlotId}{(SaveSessionManager.IsExpeditionActive ? "/expedition" : "")}"
            : "";

    /// <summary>Records the snapshot's systems off the UI thread; only new ones are written.</summary>
    public static void Observe(PlanetSnapshot snapshot)
    {
        string saveKey = ActiveSaveKey();
        if (saveKey.Length == 0) return;
        _ = Task.Run(() =>
        {
            var systems = NmsPlanets.ReadSystems(snapshot.Discoveries).Select(s => s.SystemAddress).ToList();
            if (NmsSpaceStationPaths.ReadSystemAddress(snapshot.Universe) is ulong here)
                systems.Add(NmsSpaceStationPaths.ToSystemAddress(here));
            Record(saveKey, systems);
        });
    }

    /// <summary>Every system remembered for the save (loaded once per save, then kept up to date).</summary>
    public static IReadOnlySet<ulong> For(string saveKey)
    {
        if (saveKey.Length == 0) return new HashSet<ulong>();
        lock (Lock) return Load(saveKey).ToHashSet();
    }

    private static void Record(string saveKey, IEnumerable<ulong> systems)
    {
        List<ulong> fresh;
        lock (Lock)
        {
            var known = Load(saveKey);
            fresh = systems.Where(s => s != 0).Distinct().Where(s => !known.Contains(s)).ToList();
            if (fresh.Count == 0) return;
            known.UnionWith(fresh);
        }
        try
        {
            using var connection = UserDatabase.Open();
            using var tx = connection.BeginTransaction();
            string now = UserDatabase.Now();
            foreach (ulong system in fresh)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT OR IGNORE INTO visited_systems (save_key, system_address, first_seen, last_seen, source, added_by)
                    VALUES ($key, $system, $now, $now, 'save', $added_by)";
                cmd.Parameters.AddWithValue("$key", saveKey);
                cmd.Parameters.AddWithValue("$system", system.ToString("X", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (SqliteException ex)
        {
            Debug.WriteLine($"[VisitedSystemsService] Couldn't record visits: {ex.Message}");
            lock (Lock) Known.Remove(saveKey); // reread next time rather than trust the memory copy
        }
    }

    /// <summary>Caller holds Lock.</summary>
    private static HashSet<ulong> Load(string saveKey)
    {
        if (Known.TryGetValue(saveKey, out var known)) return known;
        known = new HashSet<ulong>();
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT system_address FROM visited_systems WHERE save_key = $key AND deleted = 0";
            cmd.Parameters.AddWithValue("$key", saveKey);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                if (ulong.TryParse(reader.GetString(0), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong system)) known.Add(system);
        }
        catch (SqliteException ex)
        {
            Debug.WriteLine($"[VisitedSystemsService] Couldn't read visits: {ex.Message}");
        }
        Known[saveKey] = known;
        return known;
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) Known.Clear();
    }
}
