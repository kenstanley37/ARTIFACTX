using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Key/value settings in user.db, in two scopes:
///  Profile - the player's own choices (theme, glyph size, hyperdrive range...),
///            which travel with a backup to another PC.
///  Device  - this PC only (window position, save-folder paths), which a
///            restore on another PC keeps from that PC (UserDatabase.ImportFrom).
/// Read once into memory, written through. Replaces app-settings.json,
/// settings.json and windowstate.json, which LegacyDataImporter brings in on
/// first use.
/// </summary>
public static class SettingsStore
{
    public enum Scope { Profile, Device }

    private static readonly object Lock = new();
    private static Dictionary<string, string>? _profile;
    private static Dictionary<string, string>? _device;

    public static string? Get(Scope scope, string key)
    {
        lock (Lock) return Values(scope).TryGetValue(key, out var value) ? value : null;
    }

    public static void Set(Scope scope, string key, string? value)
    {
        lock (Lock)
        {
            var values = Values(scope);
            if (value is null) values.Remove(key);
            else values[key] = value;
        }
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            if (scope == Scope.Profile)
            {
                cmd.CommandText = @"
                    INSERT INTO settings (profile_id, key, value, updated_at, deleted) VALUES ('', $key, $value, $now, $deleted)
                    ON CONFLICT(profile_id, key) DO UPDATE SET value = $value, updated_at = $now, synced_at = NULL, deleted = $deleted";
                cmd.Parameters.AddWithValue("$deleted", value is null ? 1 : 0);
            }
            else
            {
                cmd.CommandText = value is null
                    ? "DELETE FROM device_settings WHERE key = $key"
                    : @"INSERT INTO device_settings (key, value, updated_at) VALUES ($key, $value, $now)
                        ON CONFLICT(key) DO UPDATE SET value = $value, updated_at = $now";
            }
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value ?? "");
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsStore] Couldn't save {key}: {ex.Message}");
        }
    }

    /// <summary>Drops the in-memory copies (after a restore).</summary>
    public static void Reload()
    {
        lock (Lock) { _profile = null; _device = null; }
    }

    private static Dictionary<string, string> Values(Scope scope)
    {
        if (scope == Scope.Profile && _profile is not null) return _profile;
        if (scope == Scope.Device && _device is not null) return _device;

        LegacyDataImporter.RunOnce();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = scope == Scope.Profile
                ? "SELECT key, value FROM settings WHERE profile_id = '' AND deleted = 0"
                : "SELECT key, value FROM device_settings";
            using var r = cmd.ExecuteReader();
            while (r.Read()) values[r.GetString(0)] = r.GetString(1);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsStore] Couldn't read settings: {ex.Message}");
        }
        return scope == Scope.Profile ? _profile = values : _device = values;
    }
}
