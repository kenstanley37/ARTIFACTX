using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The player's own data (system notes, learned names - later a discovery
/// cache and anything synced with the planned ArtifactX API), in
/// %LocalAppData%\ArtifactX\user.db. Separate from the game catalog
/// (Data\nms_catalog.sqlite), which is rebuilt and replaced; this file is
/// outside the install folder, so app updates never replace it.
///
/// UPGRADE RULES - every release must keep these:
///  1. Schema changes are numbered steps in <see cref="Migrations"/>, run once
///     in order and recorded in SQLite's PRAGMA user_version.
///  2. Steps only ADD: CREATE TABLE / ADD COLUMN / CREATE INDEX (each with a
///     default). Never DROP, RENAME, DELETE or UPDATE existing data in a step.
///     A column that's no longer wanted is simply left unused.
///  3. Never edit or reorder a step that has shipped - append a new one.
///  4. Each step runs in its own transaction (all or nothing), and the file is
///     copied to Backups\user-db first.
///  5. An older app opening a newer file (user_version above the steps it
///     knows) leaves the schema alone; additive steps keep it readable.
///
/// SYNC COLUMNS on every table, for the API later: updated_at (UTC ISO-8601),
/// source ("save" = learned from a save, "user" = typed, "api" = synced),
/// synced_at (NULL until uploaded) and deleted (soft delete - rows are never
/// removed, so a deletion can be synced too). Systems are keyed by their packed
/// galactic address in hex (galaxy included), the same key the API would use.
/// </summary>
public static class UserDatabase
{
    public static readonly string FilePath = Path.Combine(LocalAppDataPaths.RootFolder, "user.db");

    private static readonly object InitLock = new();
    private static bool _initialized;

    private static readonly string[] Migrations =
    {
        // 1 - system notes, learned names and the notes list (2026-10-11).
        """
        CREATE TABLE systems (
            address     TEXT PRIMARY KEY,
            galaxy      INTEGER NOT NULL DEFAULT 0,
            name        TEXT NOT NULL DEFAULT '',
            station     TEXT NOT NULL DEFAULT '',
            star_class  TEXT NOT NULL DEFAULT '',
            star_colour TEXT NOT NULL DEFAULT '',
            economy     TEXT NOT NULL DEFAULT '',
            wealth      TEXT NOT NULL DEFAULT '',
            conflict    TEXT NOT NULL DEFAULT '',
            race        TEXT NOT NULL DEFAULT '',
            moons       INTEGER NULL,
            water       INTEGER NOT NULL DEFAULT 0,
            dissonant   INTEGER NOT NULL DEFAULT 0,
            updated_at  TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT 'user',
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE planet_names (
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            name           TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'save',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (system_address, planet_index)
        );
        CREATE TABLE system_notes (
            id             TEXT PRIMARY KEY,
            system_address TEXT NOT NULL,
            text           TEXT NOT NULL,
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'user',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_system_notes_system ON system_notes(system_address);
        CREATE INDEX ix_systems_galaxy ON systems(galaxy);
        """,

        // 2 - recipes pinned on the Recipes page (2026-10-11).
        """
        CREATE TABLE pinned_recipes (
            recipe_id  TEXT PRIMARY KEY,
            pinned_at  TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            source     TEXT NOT NULL DEFAULT 'user',
            synced_at  TEXT NULL,
            deleted    INTEGER NOT NULL DEFAULT 0
        );
        """,

        // 3 - the Finds page: player-recorded finds with portal addresses (2026-10-11).
        """
        CREATE TABLE finds (
            id          TEXT PRIMARY KEY,
            type        TEXT NOT NULL,
            title       TEXT NOT NULL DEFAULT '',
            portal_code TEXT NOT NULL DEFAULT '',
            galaxy      INTEGER NOT NULL DEFAULT 1,
            class       TEXT NOT NULL DEFAULT '',
            subtype     TEXT NOT NULL DEFAULT '',
            items       TEXT NOT NULL DEFAULT '',
            location    TEXT NOT NULL DEFAULT '',
            notes       TEXT NOT NULL DEFAULT '',
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT 'user',
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_finds_type ON finds(type);
        """,

        // 4 - planets the player checked for water on the Fishing page (2026-10-11).
        """
        CREATE TABLE planet_water (
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            has_water      INTEGER NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'user',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (system_address, planet_index)
        );
        """,

        // 5 - creator video links on the Guides page (2026-10-12).
        """
        CREATE TABLE guides (
            id            TEXT PRIMARY KEY,
            url           TEXT NOT NULL,
            platform      TEXT NOT NULL,
            title         TEXT NOT NULL DEFAULT '',
            creator       TEXT NOT NULL DEFAULT '',
            thumbnail_url TEXT NOT NULL DEFAULT '',
            topic         TEXT NOT NULL DEFAULT '',
            items         TEXT NOT NULL DEFAULT '',
            notes         TEXT NOT NULL DEFAULT '',
            created_at    TEXT NOT NULL,
            updated_at    TEXT NOT NULL,
            source        TEXT NOT NULL DEFAULT 'user',
            synced_at     TEXT NULL,
            deleted       INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_guides_url ON guides(url);
        """,

        // 6 - stations hidden from Space Stations' claims-in-progress list (2026-10-12).
        """
        CREATE TABLE hidden_stations (
            system_address TEXT PRIMARY KEY,
            hidden_at      TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'user',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0
        );
        """,
        // 7 - everything in one file, owned by a profile (2026-10-12). See
        // ProfileService / GameAccountsService / SettingsStore / LegacyDataImporter.
        //  profiles       - the person (later their ArtifactX account). profile_id ''
        //                   everywhere means this PC's primary profile, so no
        //                   existing row has to be rewritten (rule 2).
        //  game_accounts  - Steam/GOG/Xbox/folder accounts seen in Save Selection,
        //                   keyed "steam:<SteamID64>", "gog:<id>", "folder:<hash>".
        //  settings       - the profile's own choices (theme, glyph size...).
        //  device_settings- this PC only (window position, folder paths); skipped
        //                   when a backup is restored on another PC.
        //  templates      - Loadout templates (were Loadouts\*.json), payload = JSON.
        //  hidden_station_claims - hide a claim per account + save slot (the old
        //                   hidden_stations hid it on every save; its rows still
        //                   count as "hidden everywhere").
        //  profile_id / added_by on existing tables: who owns a row, and which
        //                   game account a fact came from (credit, later).
        """
        CREATE TABLE profiles (
            id          TEXT PRIMARY KEY,
            name        TEXT NOT NULL DEFAULT '',
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT 'user',
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE game_accounts (
            id            TEXT PRIMARY KEY,
            profile_id    TEXT NOT NULL DEFAULT '',
            platform      TEXT NOT NULL DEFAULT '',
            account_id    TEXT NOT NULL DEFAULT '',
            display_name  TEXT NOT NULL DEFAULT '',
            nickname      TEXT NOT NULL DEFAULT '',
            first_seen_at TEXT NOT NULL,
            last_seen_at  TEXT NOT NULL,
            updated_at    TEXT NOT NULL,
            source        TEXT NOT NULL DEFAULT 'save',
            synced_at     TEXT NULL,
            deleted       INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE settings (
            profile_id  TEXT NOT NULL DEFAULT '',
            key         TEXT NOT NULL,
            value       TEXT NOT NULL DEFAULT '',
            updated_at  TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT 'user',
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (profile_id, key)
        );
        CREATE TABLE device_settings (
            key         TEXT PRIMARY KEY,
            value       TEXT NOT NULL DEFAULT '',
            updated_at  TEXT NOT NULL
        );
        CREATE TABLE templates (
            id          TEXT PRIMARY KEY,
            profile_id  TEXT NOT NULL DEFAULT '',
            kind        TEXT NOT NULL DEFAULT '',
            scope       TEXT NOT NULL DEFAULT '',
            name        TEXT NOT NULL DEFAULT '',
            payload     TEXT NOT NULL,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT 'user',
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE hidden_station_claims (
            game_account_id TEXT NOT NULL,
            slot            INTEGER NOT NULL,
            system_address  TEXT NOT NULL,
            hidden_at       TEXT NOT NULL,
            updated_at      TEXT NOT NULL,
            source          TEXT NOT NULL DEFAULT 'user',
            synced_at       TEXT NULL,
            deleted         INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (game_account_id, slot, system_address)
        );
        ALTER TABLE systems         ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE systems         ADD COLUMN added_by   TEXT NOT NULL DEFAULT '';
        ALTER TABLE planet_names    ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE planet_names    ADD COLUMN added_by   TEXT NOT NULL DEFAULT '';
        ALTER TABLE system_notes    ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE system_notes    ADD COLUMN added_by   TEXT NOT NULL DEFAULT '';
        ALTER TABLE planet_water    ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE planet_water    ADD COLUMN added_by   TEXT NOT NULL DEFAULT '';
        ALTER TABLE finds           ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE finds           ADD COLUMN added_by   TEXT NOT NULL DEFAULT '';
        ALTER TABLE guides          ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE pinned_recipes  ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        ALTER TABLE hidden_stations ADD COLUMN profile_id TEXT NOT NULL DEFAULT '';
        """,
    };

    public static int SchemaVersion => Migrations.Length;

    public static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Opens a connection, creating/upgrading the file on first use.</summary>
    public static SqliteConnection Open()
    {
        EnsureInitialized();
        var connection = new SqliteConnection($"Data Source={FilePath}");
        connection.Open();
        return connection;
    }

    private static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (_initialized) return;

            bool existed = File.Exists(FilePath);
            using (var connection = new SqliteConnection($"Data Source={FilePath}"))
            {
                connection.Open();
                Execute(connection, "PRAGMA journal_mode=WAL;");
                int current = Convert.ToInt32(Scalar(connection, "PRAGMA user_version;"), CultureInfo.InvariantCulture);

                if (current > Migrations.Length)
                {
                    Debug.WriteLine($"[UserDatabase] user.db is schema {current}, newer than this app knows ({Migrations.Length}) - leaving it unchanged.");
                }
                else if (current < Migrations.Length)
                {
                    if (existed) Backup($"before-v{current + 1}");
                    for (int step = current; step < Migrations.Length; step++)
                    {
                        using var tx = connection.BeginTransaction();
                        Execute(connection, Migrations[step], tx);
                        Execute(connection, $"PRAGMA user_version = {step + 1};", tx);
                        tx.Commit();
                    }
                }
            }
            _initialized = true;
        }
    }

    /// <summary>Copies user.db (and its WAL) to Backups\user-db, keeping the newest 10.</summary>
    public static void Backup(string reason)
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            string folder = Path.Combine(LocalAppDataPaths.GetSubfolder("Backups"), "user-db");
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string target = Path.Combine(folder, $"user-{stamp}-{reason}.db");

            // VACUUM INTO writes a consistent single-file copy even with WAL pending.
            using (var connection = new SqliteConnection($"Data Source={FilePath}"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "VACUUM INTO $target;";
                cmd.Parameters.AddWithValue("$target", target);
                cmd.ExecuteNonQuery();
            }

            foreach (var old in new DirectoryInfo(folder).GetFiles("user-*.db").OrderByDescending(f => f.CreationTimeUtc).Skip(10))
                old.Delete();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserDatabase] Backup failed: {ex.Message}");
        }
    }

    /// <summary>Writes a consistent copy of user.db to <paramref name="target"/>.</summary>
    public static void ExportTo(string target)
    {
        EnsureInitialized();
        if (File.Exists(target)) File.Delete(target);
        using var connection = new SqliteConnection($"Data Source={FilePath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "VACUUM INTO $target;";
        cmd.Parameters.AddWithValue("$target", target);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Replaces user.db with an exported copy. Checks it really is an
    /// ArtifactX user database first, backs up the current file, then upgrades
    /// the imported one to this app's schema (additive steps only). Returns an
    /// error message, or null on success.</summary>
    public static string? ImportFrom(string source)
    {
        try
        {
            using (var check = new SqliteConnection($"Data Source={source};Mode=ReadOnly"))
            {
                check.Open();
                if (Convert.ToInt32(Scalar(check, "PRAGMA user_version;"), CultureInfo.InvariantCulture) < 1
                    || Convert.ToInt32(Scalar(check, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'systems';"), CultureInfo.InvariantCulture) == 0)
                    return "That file isn't an ArtifactX data export.";
            }
        }
        catch (Exception ex)
        {
            return $"Couldn't read that file: {ex.Message}";
        }

        // This PC's own settings (window position, folder paths) stay this PC's:
        // a backup made elsewhere carries that PC's, which mean nothing here.
        var deviceSettings = new List<(string Key, string Value, string UpdatedAt)>();
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT key, value, updated_at FROM device_settings";
            using var r = cmd.ExecuteReader();
            while (r.Read()) deviceSettings.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        }
        catch (SqliteException)
        {
            // An older file without device_settings - nothing to keep.
        }

        lock (InitLock)
        {
            Backup("before-import");
            SqliteConnection.ClearAllPools(); // release pooled handles so the file can be replaced
            foreach (var extra in new[] { FilePath + "-wal", FilePath + "-shm" })
                if (File.Exists(extra)) File.Delete(extra);
            File.Copy(source, FilePath, overwrite: true);
            _initialized = false;
        }
        EnsureInitialized();

        using (var connection = Open())
        using (var tx = connection.BeginTransaction())
        {
            Execute(connection, "DELETE FROM device_settings;", tx);
            foreach (var (key, value, updatedAt) in deviceSettings)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO device_settings (key, value, updated_at) VALUES ($key, $value, $at)";
                cmd.Parameters.AddWithValue("$key", key);
                cmd.Parameters.AddWithValue("$value", value);
                cmd.Parameters.AddWithValue("$at", updatedAt);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        // Everything cached from the old file is stale now.
        SettingsStore.Reload();
        GameAccountsService.Reload();
        ProfileService.Reload();
        HiddenStationsService.Reload();
        return null;
    }

    public static void Execute(SqliteConnection connection, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
