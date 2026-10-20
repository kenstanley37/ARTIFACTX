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
/// %AppData%\ArtifactX\user.db. Separate from the game catalog
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

        // 8 - what the player recorded about each planet (2026-10-13): the
        // discovery screen's weather, sentinels, flora and fauna words and
        // the resources found there (comma-separated), plus free notes per
        // planet. The save stores none of these. Keyed like planet_names.
        """
        CREATE TABLE planet_details (
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            weather        TEXT NOT NULL DEFAULT '',
            sentinels      TEXT NOT NULL DEFAULT '',
            flora          TEXT NOT NULL DEFAULT '',
            fauna          TEXT NOT NULL DEFAULT '',
            resources      TEXT NOT NULL DEFAULT '',
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'user',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (system_address, planet_index)
        );
        CREATE TABLE planet_notes (
            id             TEXT PRIMARY KEY,
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            text           TEXT NOT NULL,
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'user',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX ix_planet_notes_planet ON planet_notes(system_address, planet_index);
        """,

        // 9 - ship types the player has seen in a system (2026-10-13),
        // comma-separated ("Fighter, Solar"); the save doesn't record them.
        """
        ALTER TABLE systems ADD COLUMN ships TEXT NOT NULL DEFAULT '';
        """,

        // 10 - where fish were caught, learned while following the game
        // (2026-10-13; see FishCatchService). One row per catch seen.
        """
        CREATE TABLE fish_catches (
            id             TEXT PRIMARY KEY,
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            fish_id        TEXT NOT NULL,
            count          INTEGER NOT NULL DEFAULT 1,
            caught_at      TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'save',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX ix_fish_catches_planet ON fish_catches(system_address, planet_index);
        """,

        // 11 - where on the planet a catch was (2026-10-14): the coordinates
        // the game shows, worked out from the save (NmsPlanetCoordinates).
        // NULL for catches recorded before this step.
        """
        ALTER TABLE fish_catches ADD COLUMN latitude REAL NULL;
        ALTER TABLE fish_catches ADD COLUMN longitude REAL NULL;
        """,

        // 12 - designs saved on the Ship Designer page (2026-10-17), to add or
        // apply to a ship later. payload = the design as JSON (ShipDesignsService).
        """
        CREATE TABLE ship_designs (
            id          TEXT PRIMARY KEY,
            profile_id  TEXT NOT NULL DEFAULT '',
            name        TEXT NOT NULL DEFAULT '',
            ship_type   TEXT NOT NULL DEFAULT '',
            payload     TEXT NOT NULL,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT 'user',
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0
        );
        """,

        // 13 - a picture of each saved design for the designer's list (2026-10-17):
        // PNG from the preview renderer. NULL until one is taken.
        """
        ALTER TABLE ship_designs ADD COLUMN thumbnail BLOB NULL;
        """,

        // 14 - how many kinds of fauna a planet has (2026-10-17), from its Discoveries
        // page ("0 / 6 Discovered"; 0 = "No Planetary Fauna"). NULL = not known.
        """
        ALTER TABLE planet_details ADD COLUMN fauna_total INTEGER NULL;
        """,

        // 15 - planet or moon, as the game's own pop-up said it (MARK MOON / MARK PLANET),
        // read by game screen recording (2026-10-17; BodyKindsService).
        """
        CREATE TABLE body_kinds (
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            is_moon        INTEGER NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'captured',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (system_address, planet_index)
        );
        """,

        // 16 - what each system's station sells, from its trade terminal (2026-10-17,
        // GitHub #9; SystemMarketService). price in units, change as shown ("-18.4%").
        """
        CREATE TABLE system_market (
            system_address TEXT NOT NULL,
            item_id        TEXT NOT NULL,
            name           TEXT NOT NULL DEFAULT '',
            price          INTEGER NOT NULL DEFAULT 0,
            change         TEXT NOT NULL DEFAULT '',
            stock          INTEGER NOT NULL DEFAULT 0,
            seen_at        TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'captured',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (system_address, item_id)
        );
        """,

        // 17 - market by place (2026-10-17): a planet's trading post sells other stock at
        // other prices than the system's space station. place = "station" or "planet:N".
        // Rows from step 16 are copied in as "station"; system_market is left unused.
        """
        CREATE TABLE market_items (
            system_address TEXT NOT NULL,
            place          TEXT NOT NULL DEFAULT 'station',
            item_id        TEXT NOT NULL,
            name           TEXT NOT NULL DEFAULT '',
            price          INTEGER NOT NULL DEFAULT 0,
            change         TEXT NOT NULL DEFAULT '',
            stock          INTEGER NOT NULL DEFAULT 0,
            seen_at        TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'captured',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (system_address, place, item_id)
        );
        INSERT INTO market_items (system_address, place, item_id, name, price, change, stock, seen_at, updated_at, source, synced_at, deleted, profile_id, added_by)
            SELECT system_address, 'station', item_id, name, price, change, stock, seen_at, updated_at, source, synced_at, deleted, profile_id, added_by
            FROM system_market WHERE price > 0;
        """,

        // 18 - provenance (2026-10-17, for the future shared database): which source set each
        // value - "captured" off a game screen, "user" typed, "save" learned from a save - and
        // the game build it was seen on, field by field (FieldSources). entity = "planet"
        // (key "<system hex>|<index>") or "system" (key "<system hex>"). Market rows are a
        // single reading each, so they just get the build column.
        """
        CREATE TABLE field_sources (
            entity      TEXT NOT NULL,
            entity_key  TEXT NOT NULL,
            field       TEXT NOT NULL,
            value       TEXT NOT NULL DEFAULT '',
            source      TEXT NOT NULL DEFAULT '',
            game_build  TEXT NOT NULL DEFAULT '',
            recorded_at TEXT NOT NULL,
            synced_at   TEXT NULL,
            deleted     INTEGER NOT NULL DEFAULT 0,
            profile_id  TEXT NOT NULL DEFAULT '',
            added_by    TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (entity, entity_key, field)
        );
        ALTER TABLE market_items ADD COLUMN game_build TEXT NOT NULL DEFAULT '';
        """,

        // 19 - creatures on a planet (2026-10-18), read off the game's Discoveries fauna list
        // (CreatureListService). The save keeps one record per creature but only its seeds,
        // not its name or traits. name_key = the name without its "E. " initial, which the
        // reading sometimes drops. picture = a picture was kept in creature-pictures.
        """
        CREATE TABLE planet_creatures (
            system_address TEXT NOT NULL,
            planet_index   INTEGER NOT NULL,
            name_key       TEXT NOT NULL,
            name           TEXT NOT NULL DEFAULT '',
            rarity         TEXT NOT NULL DEFAULT '',
            habitat        TEXT NOT NULL DEFAULT '',
            activity       TEXT NOT NULL DEFAULT '',
            gender         TEXT NOT NULL DEFAULT '',
            size           TEXT NOT NULL DEFAULT '',
            picture        INTEGER NOT NULL DEFAULT 0,
            seen_at        TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            source         TEXT NOT NULL DEFAULT 'captured',
            game_build     TEXT NOT NULL DEFAULT '',
            synced_at      TEXT NULL,
            deleted        INTEGER NOT NULL DEFAULT 0,
            profile_id     TEXT NOT NULL DEFAULT '',
            added_by       TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (system_address, planet_index, name_key)
        );
        """,
    };

    public static int SchemaVersion => Migrations.Length;

    /// <summary>Returns the total size on disk of user.db (including WAL/SHM companion files if present).</summary>
    public static long GetDatabaseSizeBytes()
    {
        long total = 0;
        try
        {
            if (File.Exists(FilePath))
                total += new FileInfo(FilePath).Length;

            string wal = FilePath + "-wal";
            if (File.Exists(wal))
                total += new FileInfo(wal).Length;

            string shm = FilePath + "-shm";
            if (File.Exists(shm))
                total += new FileInfo(shm).Length;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserDatabase] Failed to get database size: {ex.Message}");
        }
        return total;
    }

    /// <summary>Returns size details for user.db, WAL, and total bytes.</summary>
    public static (long MainBytes, long WalBytes, long TotalBytes) GetDatabaseSizeDetails()
    {
        long main = 0;
        long wal = 0;
        long shm = 0;
        try
        {
            if (File.Exists(FilePath))
                main = new FileInfo(FilePath).Length;

            string walPath = FilePath + "-wal";
            if (File.Exists(walPath))
                wal = new FileInfo(walPath).Length;

            string shmPath = FilePath + "-shm";
            if (File.Exists(shmPath))
                shm = new FileInfo(shmPath).Length;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UserDatabase] Failed to get database size details: {ex.Message}");
        }
        return (main, wal, main + wal + shm);
    }

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

    public const int DefaultBackupsToKeep = 14;
    private const string BackupsToKeepKey = "DataBackupsToKeep";
    private const string DailyBackupKey = "DataBackupDaily";

    /// <summary>How many backups of user.db to keep (Settings page).</summary>
    public static int BackupsToKeep
    {
        get => int.TryParse(SettingsStore.Get(SettingsStore.Scope.Profile, BackupsToKeepKey), out int keep) && keep > 0 ? keep : DefaultBackupsToKeep;
        set => SettingsStore.Set(SettingsStore.Scope.Profile, BackupsToKeepKey, value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Whether a backup is made automatically once a day (on by default).</summary>
    public static bool DailyBackups
    {
        get => SettingsStore.Get(SettingsStore.Scope.Profile, DailyBackupKey) != "false";
        set => SettingsStore.Set(SettingsStore.Scope.Profile, DailyBackupKey, value ? "true" : "false");
    }

    private static string BackupFolder => BackupLocation.Sub(BackupService.UserDataFolder);

    /// <summary>One backup of the player's data, for the Settings page.</summary>
    public sealed record DataBackup(string Path, DateTime Created, long Bytes, string Reason);

    public static List<DataBackup> ListBackups() =>
        new DirectoryInfo(BackupFolder).GetFiles("user-*.db")
            .OrderByDescending(MadeAt)
            .Select(f => new DataBackup(f.FullName, MadeAt(f), f.Length, ReasonOf(f.Name)))
            .ToList();

    /// <summary>When a backup was made, from its name ("user-20261012-153334-...").
    /// Not the file's own dates: moving the backups folder (e.g. to OneDrive)
    /// gives every file a new creation time (seen 2026-10-12, all showing the
    /// move's time).</summary>
    private static DateTime MadeAt(FileInfo file)
    {
        var parts = System.IO.Path.GetFileNameWithoutExtension(file.Name).Split('-');
        return parts.Length >= 3 && DateTime.TryParseExact(parts[1] + parts[2], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var made)
            ? made : file.LastWriteTime;
    }

    /// <summary>"user-20261012-153334-before-v7.db" -> "before-v7".</summary>
    private static string ReasonOf(string fileName)
    {
        var parts = System.IO.Path.GetFileNameWithoutExtension(fileName).Split('-', 4);
        return parts.Length == 4 ? parts[3] : "";
    }

    public static void DeleteBackup(string path)
    {
        if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) is string folder
            && string.Equals(folder, System.IO.Path.GetFullPath(BackupFolder), StringComparison.OrdinalIgnoreCase))
            File.Delete(path);
    }

    /// <summary>Makes the day's automatic backup if it's on and none was made
    /// in the last 20 hours. Called once at startup, off the UI thread.</summary>
    public static void DailyBackupIfDue()
    {
        if (!DailyBackups || !File.Exists(FilePath)) return;
        var newest = ListBackups().FirstOrDefault(b => b.Reason == "daily");
        if (newest is not null && DateTime.Now - newest.Created < TimeSpan.FromHours(20)) return;
        Backup("daily");
    }

    /// <summary>Copies user.db (and its WAL) to the backups folder's user-db,
    /// keeping the newest BackupsToKeep. Runs before upgrades and restores too,
    /// when the settings can't be read yet - then the default count is kept.</summary>
    public static void Backup(string reason)
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            string folder = BackupFolder;
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

            foreach (var old in new DirectoryInfo(folder).GetFiles("user-*.db").OrderByDescending(MadeAt).Skip(_initialized ? BackupsToKeep : DefaultBackupsToKeep))
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
