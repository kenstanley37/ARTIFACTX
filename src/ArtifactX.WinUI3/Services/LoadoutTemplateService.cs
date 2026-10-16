using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Loadout templates (Tech Stack / Full Build, every kind) in user.db's
/// templates table: the template as JSON in payload, with kind/scope/name
/// alongside for listing. Until 2026-10-12 each was a file in
/// %APPDATA%\ArtifactX\Loadouts; LegacyDataImporter moves those in.
/// Deleting is a soft delete (deleted = 1), so it can sync later.
/// </summary>
public static class LoadoutTemplateService
{
    public static Task SaveAsync(NmsLoadoutTemplate template) => Task.Run(() =>
    {
        LegacyDataImporter.RunOnce();
        string json = JsonSerializer.Serialize(template, LoadoutJsonContext.Default.NmsLoadoutTemplate);
        using var connection = UserDatabase.Open();
        Insert(connection, null, template, json, ignoreExisting: false);
    });

    public static Task<List<NmsLoadoutTemplate>> LoadAllAsync() => Task.Run(() =>
    {
        LegacyDataImporter.RunOnce();
        var results = new List<NmsLoadoutTemplate>();
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT payload FROM templates WHERE deleted = 0";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                try
                {
                    if (JsonSerializer.Deserialize(r.GetString(0), LoadoutJsonContext.Default.NmsLoadoutTemplate) is { } template)
                        results.Add(template);
                }
                catch (JsonException)
                {
                    // Skip one unreadable template rather than failing the whole list.
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadoutTemplateService] {ex.Message}");
        }
        return results.OrderByDescending(t => t.CreatedAt).ToList();
    });

    public static Task DeleteAsync(string id) => Task.Run(() =>
    {
        using var connection = UserDatabase.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE templates SET deleted = 1, updated_at = $now, synced_at = NULL WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
        cmd.ExecuteNonQuery();
    });

    /// <summary>Writes one template row (also used by LegacyDataImporter).</summary>
    internal static void Insert(SqliteConnection connection, SqliteTransaction? tx, NmsLoadoutTemplate template, string json, bool ignoreExisting)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = ignoreExisting
            ? @"INSERT OR IGNORE INTO templates (id, profile_id, kind, scope, name, payload, created_at, updated_at)
                VALUES ($id, '', $kind, $scope, $name, $payload, $created, $now)"
            : @"INSERT INTO templates (id, profile_id, kind, scope, name, payload, created_at, updated_at)
                VALUES ($id, '', $kind, $scope, $name, $payload, $created, $now)
                ON CONFLICT(id) DO UPDATE SET kind = $kind, scope = $scope, name = $name, payload = $payload,
                    updated_at = $now, synced_at = NULL, deleted = 0";
        cmd.Parameters.AddWithValue("$id", template.Id);
        cmd.Parameters.AddWithValue("$kind", template.SourceKind ?? "");
        cmd.Parameters.AddWithValue("$scope", template.Scope ?? "");
        cmd.Parameters.AddWithValue("$name", template.Name ?? "");
        cmd.Parameters.AddWithValue("$payload", json);
        cmd.Parameters.AddWithValue("$created", template.CreatedAt.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
        cmd.ExecuteNonQuery();
    }
}

[JsonSerializable(typeof(NmsLoadoutTemplate))]
internal partial class LoadoutJsonContext : JsonSerializerContext { }
