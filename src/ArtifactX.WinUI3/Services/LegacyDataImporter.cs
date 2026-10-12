using ArtifactX.WinUI3.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// One-time move of the files ArtifactX kept before everything lived in
/// user.db (2026-10-12). Each file is copied in, then renamed ".imported"
/// (never deleted), the same way system-notes.json was handled. Existing rows
/// win - an import never overwrites something already in the database.
///   app-settings.json -> settings (profile)
///   settings.json     -> device_settings, and each Steam/GOG card nickname
///                        onto its game account
///   windowstate.json  -> device_settings "WindowState"
///   Loadouts\*.json   -> templates (the folder becomes Loadouts.imported)
/// </summary>
public static class LegacyDataImporter
{
    private static readonly object Lock = new();
    private static bool _done;

    public static void RunOnce()
    {
        lock (Lock)
        {
            if (_done) return;
            _done = true;
            string root = LocalAppDataPaths.RootFolder;
            Try(() => ImportSettings(Path.Combine(root, "app-settings.json"), profile: true));
            Try(() => ImportSettings(Path.Combine(root, "settings.json"), profile: false));
            Try(() => ImportWindowState(Path.Combine(root, "windowstate.json")));
            Try(() => ImportTemplates(Path.Combine(root, "Loadouts")));
        }
    }

    private static void ImportSettings(string file, bool profile)
    {
        if (!File.Exists(file)) return;
        var values = JsonSerializer.Deserialize(File.ReadAllText(file), AppSettingsJsonContext.Default.DictionaryStringString) ?? new();
        using (var connection = UserDatabase.Open())
        using (var tx = connection.BeginTransaction())
        {
            foreach (var (key, value) in values)
                Insert(connection, tx, profile
                    ? "INSERT OR IGNORE INTO settings (profile_id, key, value, updated_at) VALUES ('', $key, $value, $now)"
                    : "INSERT OR IGNORE INTO device_settings (key, value, updated_at) VALUES ($key, $value, $now)", key, value);
            tx.Commit();
        }

        // Card names given to Steam/GOG accounts become that account's nickname.
        if (!profile && values.TryGetValue("SaveFolderCustomNames", out var namesJson) && namesJson.Length > 0)
        {
            var names = JsonSerializer.Deserialize(namesJson, AppSettingsJsonContext.Default.DictionaryStringString) ?? new();
            foreach (var (folder, name) in names)
                if (!GameAccountsService.KeyFor(folder).StartsWith("folder:", StringComparison.Ordinal) && GameAccountsService.Nickname(folder) is null)
                    GameAccountsService.SetNickname(folder, name);
        }
        File.Move(file, file + ".imported", overwrite: true);
    }

    private static void ImportWindowState(string file)
    {
        if (!File.Exists(file)) return;
        using (var connection = UserDatabase.Open())
            Insert(connection, null, "INSERT OR IGNORE INTO device_settings (key, value, updated_at) VALUES ($key, $value, $now)", "WindowState", File.ReadAllText(file));
        File.Move(file, file + ".imported", overwrite: true);
    }

    private static void ImportTemplates(string folder)
    {
        if (!Directory.Exists(folder)) return;
        var files = Directory.GetFiles(folder, "*.json");
        using (var connection = UserDatabase.Open())
        using (var tx = connection.BeginTransaction())
        {
            foreach (var file in files)
            {
                string json = File.ReadAllText(file);
                NmsLoadoutTemplate? template;
                try { template = JsonSerializer.Deserialize(json, LoadoutJsonContext.Default.NmsLoadoutTemplate); }
                catch (JsonException) { continue; } // unreadable: stays in Loadouts.imported
                if (template is null) continue;
                LoadoutTemplateService.Insert(connection, tx, template, json, ignoreExisting: true);
            }
            tx.Commit();
        }
        string imported = folder + ".imported";
        if (Directory.Exists(imported)) imported += "-" + DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        Directory.Move(folder, imported);
    }

    private static void Insert(SqliteConnection connection, SqliteTransaction? tx, string sql, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
        cmd.ExecuteNonQuery();
    }

    private static void Try(Action step)
    {
        try { step(); }
        catch (Exception ex) { Debug.WriteLine($"[LegacyDataImporter] {ex.Message}"); }
    }
}
