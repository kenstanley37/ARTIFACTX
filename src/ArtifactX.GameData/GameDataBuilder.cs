using System.Diagnostics;
using ArtifactX.GameData.Services;
using Microsoft.Data.Sqlite;

namespace ArtifactX.GameData;

/// <summary>One step of the build, for progress: "Step 3 of 16 - Recipes".</summary>
public sealed record GameDataProgress(int Step, int Steps, string Label, string? Detail = null);

/// <summary>
/// Builds ArtifactX's catalog (item names, icons, recipes, fish...) from the
/// player's own No Man's Sky files, so no game data ships with ArtifactX
/// (2026-10-14). Same steps the developer used to run by hand: the main build
/// (CatalogBuildService), every CatalogSteps addition, then the trim to the
/// small copy the app reads - plus a GameDataInfo table recording which game
/// build it came from.
///
/// Works in a scratch folder and only replaces the existing catalog once the
/// new one is complete, so a failed or cancelled build leaves the old one in
/// place.
/// </summary>
public static class GameDataBuilder
{
    /// <summary>Bump when the builder changes what goes in the catalog (a new
    /// step, a fix): every player's catalog then needs rebuilding, even if the
    /// game didn't update.</summary>
    public const int CatalogFormat = 1;

    /// <summary>The additions after the main build, in order.</summary>
    private static readonly (string Label, Action<string, string> Run)[] Steps =
    {
        ("Alien language words", CatalogSteps.AddLanguageWords),
        ("Guide topics", CatalogSteps.AddGuideTopics),
        ("Space station assets and glyphs", CatalogSteps.AddStationAssets),
        ("Freighter paints", CatalogSteps.AddFreighterPaints),
        ("Corvette parts", CatalogSteps.AddCorvetteParts),
        ("Technology stats", CatalogSteps.AddTechStats),
        ("System descriptions", CatalogSteps.AddSystemDescriptors),
        ("Planet descriptions", CatalogSteps.AddPlanetDescriptors),
        ("Recipes", CatalogSteps.AddRecipes),
        ("Item values", CatalogSteps.AddItemValues),
        ("Fish", CatalogSteps.AddFish),
        ("Crafting", CatalogSteps.AddCrafting),
        ("Procedural items", CatalogSteps.AddProceduralProducts),
    };

    /// <summary>Main build + additions + finishing.</summary>
    public static int StepCount => Steps.Length + 2;

    /// <summary>Builds the catalog for <paramref name="game"/> and moves it to
    /// <paramref name="outputPath"/>. Throws on failure (the old catalog stays).</summary>
    public static void Build(GameInstall game, string outputPath, string workFolder,
        IProgress<GameDataProgress>? progress = null, CancellationToken cancel = default)
    {
        Directory.CreateDirectory(workFolder);
        string full = Path.Combine(workFolder, "catalog-full.sqlite");
        string trimmed = Path.Combine(workFolder, "catalog.sqlite");
        DeleteIfThere(full);
        DeleteIfThere(trimmed);
        var clock = Stopwatch.StartNew();
        int step = 0;

        void Report(string label)
        {
            step++;
            LogService.Write($"== {label} ({clock.Elapsed:mm\\:ss})");
            progress?.Report(new GameDataProgress(step, StepCount, label));
        }

        try
        {
            Report("Reading your game files");
            var discovery = new PakDiscoveryService();
            new CatalogBuildService(discovery, new PakReaderService(), new ExtractionService())
                .Run(game.PcbanksPath, full, iconTargetSize: 128);
            if (!File.Exists(full)) throw new InvalidOperationException("The game files couldn't be read - no catalog was written.");

            foreach (var (label, run) in Steps)
            {
                cancel.ThrowIfCancellationRequested();
                Report(label);
                run(full, game.PcbanksPath);
            }

            cancel.ThrowIfCancellationRequested();
            Report("Finishing");
            CatalogTrimService.Trim(full, trimmed);
            int items = CountItems(trimmed);
            if (items < 1000) throw new InvalidOperationException($"The build only found {items} items - something went wrong reading the game files.");
            WriteInfo(trimmed, game, items);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            SqliteConnection.ClearAllPools(); // let go of the files before moving
            File.Move(trimmed, outputPath, overwrite: true);
            LogService.Write($"== Done in {clock.Elapsed:mm\\:ss}: {items} items, {game.BuildLabel}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfThere(full);
            DeleteIfThere(trimmed);
        }
    }

    private static int CountItems(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Items";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void WriteInfo(string path, GameInstall game, int items)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE IF NOT EXISTS GameDataInfo (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";
            create.ExecuteNonQuery();
        }
        var values = new Dictionary<string, string?>
        {
            [GameDataInfo.FormatKey] = CatalogFormat.ToString(),
            [GameDataInfo.FingerprintKey] = game.Fingerprint,
            [GameDataInfo.ExeVersionKey] = game.ExeVersion,
            [GameDataInfo.SteamBuildKey] = game.SteamBuildId,
            [GameDataInfo.BuiltAtKey] = DateTime.UtcNow.ToString("o"),
            [GameDataInfo.ItemCountKey] = items.ToString(),
        };
        foreach (var (key, value) in values)
        {
            if (value is null) continue;
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO GameDataInfo (Key, Value) VALUES ($k, $v)";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }
    }

    private static void DeleteIfThere(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }
}

/// <summary>What a built catalog says about itself (its GameDataInfo table).</summary>
public sealed record GameDataInfo(int Format, string? Fingerprint, string? ExeVersion, string? SteamBuildId, DateTime? BuiltAtUtc)
{
    public const string FormatKey = "CatalogFormat";
    public const string FingerprintKey = "GameFingerprint";
    public const string ExeVersionKey = "GameExeVersion";
    public const string SteamBuildKey = "SteamBuildId";
    public const string BuiltAtKey = "BuiltAtUtc";
    public const string ItemCountKey = "ItemCount";

    /// <summary>Reads a catalog's info; null when it has none (built before
    /// 2026-10-14, by hand) or can't be opened.</summary>
    public static GameDataInfo? Read(string catalogPath)
    {
        if (!File.Exists(catalogPath)) return null;
        try
        {
            using var connection = new SqliteConnection($"Data Source={catalogPath};Mode=ReadOnly");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM GameDataInfo";
            var values = new Dictionary<string, string>();
            using (var r = cmd.ExecuteReader())
                while (r.Read()) values[r.GetString(0)] = r.GetString(1);
            return new GameDataInfo(
                int.TryParse(values.GetValueOrDefault(FormatKey), out int format) ? format : 0,
                values.GetValueOrDefault(FingerprintKey),
                values.GetValueOrDefault(ExeVersionKey),
                values.GetValueOrDefault(SteamBuildKey),
                DateTime.TryParse(values.GetValueOrDefault(BuiltAtKey), null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null);
        }
        catch (SqliteException)
        {
            return null; // no GameDataInfo table: an old hand-built catalog
        }
    }

    /// <summary>True when this catalog was built from <paramref name="game"/>
    /// as it is now, by the current builder.</summary>
    public bool Matches(GameInstall game) => Format == GameDataBuilder.CatalogFormat && Fingerprint == game.Fingerprint;
}
