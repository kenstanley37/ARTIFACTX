using ArtifactX.GameData;
using ArtifactX.GameData.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>Where the catalog stands against the game installed on this PC.</summary>
public enum GameDataState
{
    /// <summary>Built from the installed game as it is now.</summary>
    UpToDate,
    /// <summary>No catalog yet - the first-run build hasn't happened.</summary>
    NotBuilt,
    /// <summary>The game has updated since the catalog was built.</summary>
    GameUpdated,
    /// <summary>ArtifactX's builder changed (new data it collects).</summary>
    BuilderUpdated,
    /// <summary>There's a catalog, but no game install was found to check it against.</summary>
    NoGameFound,
}

/// <summary>
/// ArtifactX's item catalog (names, icons, recipes...) is built on the
/// player's PC from their own No Man's Sky files (ArtifactX.GameData), so no
/// game data ships with ArtifactX (user decision 2026-10-14). This finds the
/// install, says whether the catalog matches it - the game's own build, read
/// from the install, never a version typed into ArtifactX - and runs the build
/// in the background.
///
/// The catalog lives in LocalAppData\ArtifactX\GameData. Developer builds
/// still find the old bundled Data\nms_catalog.sqlite until a catalog has been
/// built (it has no GameDataInfo, so it reads as needing a build).
/// </summary>
public static class GameDataService
{
    private const string GameFolderKey = "GameFolder";

    public static string Folder => LocalAppDataPaths.GetSubfolder("GameData");
    public static string CatalogPath => Path.Combine(Folder, "nms_catalog.sqlite");
    public static string BundledCatalogPath => Path.Combine(AppContext.BaseDirectory, "Data", "nms_catalog.sqlite");
    public static string LogPath => Path.Combine(Folder, "build-log.txt");

    /// <summary>The catalog the app reads: the built one, else (developer
    /// builds only) the bundled one. Null when there's neither.</summary>
    public static string? ActiveCatalogPath =>
        File.Exists(CatalogPath) ? CatalogPath : File.Exists(BundledCatalogPath) ? BundledCatalogPath : null;

    private static GameInstall? _game;
    private static bool _gameLooked;

    /// <summary>The install to read: the folder the player chose, else the
    /// first one found (Steam, GOG, Xbox app).</summary>
    public static GameInstall? Game
    {
        get
        {
            if (_gameLooked) return _game;
            _gameLooked = true;
            string? chosen = SettingsStore.Get(SettingsStore.Scope.Device, GameFolderKey);
            _game = (chosen is { Length: > 0 } ? GameInstall.Inspect(chosen) : null) ?? GameInstall.Find();
            return _game;
        }
    }

    /// <summary>Uses this folder from now on. False when it isn't a No Man's Sky install.</summary>
    public static bool ChooseGameFolder(string folder)
    {
        var game = GameInstall.Inspect(folder);
        if (game is null) return false;
        SettingsStore.Set(SettingsStore.Scope.Device, GameFolderKey, game.RootFolder);
        _game = game;
        _gameLooked = true;
        StateChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    /// <summary>Looks at the install again (after the game updated while ArtifactX was open).</summary>
    public static void Refresh()
    {
        _gameLooked = false;
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public static GameDataInfo? Info => GameDataInfo.Read(CatalogPath);

    public static GameDataState State
    {
        get
        {
            if (!File.Exists(CatalogPath)) return GameDataState.NotBuilt;
            var info = Info;
            if (Game is not { } game) return GameDataState.NoGameFound;
            if (info is null || info.Format < GameDataBuilder.CatalogFormat) return GameDataState.BuilderUpdated;
            return info.Fingerprint == game.Fingerprint ? GameDataState.UpToDate : GameDataState.GameUpdated;
        }
    }

    /// <summary>Raised (any thread) when the state may have changed: a build
    /// finished, or another game folder was chosen.</summary>
    public static event EventHandler? StateChanged;

    // ------------------------------------------------------------------ building

    private static readonly object BuildLock = new();
    private static Task? _build;
    private static CancellationTokenSource? _cancel;

    public static bool IsBuilding { get { lock (BuildLock) return _build is { IsCompleted: false }; } }

    /// <summary>The latest progress of a running build (null when none).</summary>
    public static GameDataProgress? Progress { get; private set; }

    /// <summary>Raised (builder thread) with each step.</summary>
    public static event Action<GameDataProgress>? ProgressChanged;

    /// <summary>Raised (builder thread) when a build ends: null on success, else
    /// why it didn't. The listener reloads CatalogService on the UI thread.</summary>
    public static event Action<string?>? BuildFinished;

    /// <summary>Starts building in the background (does nothing if one is running).</summary>
    public static void StartBuild()
    {
        lock (BuildLock)
        {
            if (_build is { IsCompleted: false }) return;
            if (Game is not { } game)
            {
                BuildFinished?.Invoke("No Man's Sky wasn't found on this PC. Choose its folder in Settings.");
                return;
            }
            _cancel = new CancellationTokenSource();
            var token = _cancel.Token;
            Progress = new GameDataProgress(0, GameDataBuilder.StepCount, "Starting");
            _build = Task.Factory.StartNew(() => Run(game, token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    public static void CancelBuild()
    {
        lock (BuildLock) _cancel?.Cancel();
    }

    private static void Run(GameInstall game, CancellationToken token)
    {
        string? error = null;
        LogService.EchoToConsole = false;
        LogService.UseLogFile(LogPath);
        try
        {
            GameDataBuilder.Build(game, CatalogPath, Path.Combine(Folder, "work"),
                new InlineProgress(p => { Progress = p; ProgressChanged?.Invoke(p); }), token);
        }
        catch (OperationCanceledException)
        {
            error = "Cancelled - your previous game data is unchanged.";
        }
        catch (Exception ex)
        {
            LogService.Write($"Build failed: {ex}");
            error = $"Couldn't read the game files: {ex.Message}";
        }
        finally
        {
            Progress = null;
            // The builder holds every decoded table while it runs; give that back.
            GC.Collect();
        }
        BuildFinished?.Invoke(error);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Reports on the builder's own thread (Progress&lt;T&gt; would
    /// post to a thread pool and could arrive out of order).</summary>
    private sealed class InlineProgress(Action<GameDataProgress> report) : IProgress<GameDataProgress>
    {
        public void Report(GameDataProgress value) => report(value);
    }
}
