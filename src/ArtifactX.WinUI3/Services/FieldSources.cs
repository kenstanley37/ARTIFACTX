using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Where each recorded value came from, field by field (user.db step 18, 2026-10-17):
/// a planet can mix a sentinel level read off the game screen with weather the player
/// typed. For the future shared database: captured values are checked against the
/// system generator, and a value read on one build of the game can't be checked against
/// another (big updates reshuffle what planets get) - so each value carries the game
/// build it was seen on. Written in the same transaction as the value itself; only
/// fields whose value actually changed get a new row.
/// </summary>
public static class FieldSources
{
    /// <summary>The installed game's build ("4.71.0.0", or "steam:12345678"), "" when unknown.</summary>
    public static string GameBuild
    {
        get
        {
            try
            {
                var game = GameDataService.Game;
                return game?.ExeVersion is { Length: > 0 } v ? v : game?.SteamBuildId is { Length: > 0 } s ? "steam:" + s : "";
            }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>A row's current values (column -> text), empty when there is no row.</summary>
    public static Dictionary<string, string> ReadRow(SqliteConnection c, SqliteTransaction tx, string table,
        string where, IReadOnlyDictionary<string, object> args, IReadOnlyList<string> columns)
    {
        var row = new Dictionary<string, string>();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT {string.Join(", ", columns)} FROM {table} WHERE {where} AND deleted = 0";
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        using var r = cmd.ExecuteReader();
        if (r.Read())
            for (int i = 0; i < columns.Count; i++)
                row[columns[i]] = r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "";
        return row;
    }

    /// <summary>Records the source of every field whose value differs from before.</summary>
    public static void Record(SqliteConnection c, SqliteTransaction tx, string entity, string key,
        IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, string source)
    {
        var changed = after.Where(kv => kv.Value != (before.TryGetValue(kv.Key, out var old) ? old : "")).ToList();
        if (changed.Count == 0) return;
        string now = UserDatabase.Now(), build = GameBuild;
        foreach (var (field, value) in changed)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO field_sources (entity, entity_key, field, value, source, game_build, recorded_at, synced_at, deleted, added_by)
                VALUES ($e, $k, $f, $v, $source, $build, $now, NULL, 0, $by)
                ON CONFLICT(entity, entity_key, field) DO UPDATE SET
                    value = $v, source = $source, game_build = $build, recorded_at = $now, synced_at = NULL, deleted = 0";
            cmd.Parameters.AddWithValue("$e", entity);
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$f", field);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.Parameters.AddWithValue("$source", source);
            cmd.Parameters.AddWithValue("$build", build);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$by", GameAccountsService.ActiveKey);
            cmd.ExecuteNonQuery();
        }
    }
}
