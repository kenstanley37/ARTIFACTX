using ArtifactX.Core;
using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Reads the planets known to every OTHER save slot in a platform folder, for
/// PlanetsPage's finder. Each slot's newest .hg file is decrypted straight from
/// disk (not the Working/ JSON cache, which can be stale) and only its
/// discovery list is used. Results are cached per file until its timestamp
/// changes, so revisiting the page doesn't re-decrypt anything.
/// </summary>
public static class KnownPlanetsService
{
    private static readonly Dictionary<string, (DateTime WriteTime, List<KnownPlanet> Planets, List<KnownSystem> Systems)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    public static Task<List<KnownPlanet>> ReadOtherSlotsAsync(string platformFolder, int excludeSlotId) =>
        Task.Run(async () =>
        {
            var results = new List<KnownPlanet>();
            foreach (var (slotId, path) in NewestFilePerSlot(platformFolder))
            {
                if (slotId == excludeSlotId) continue;
                foreach (var planet in (await ReadFileAsync(path)).Planets)
                    results.Add(planet with { Source = $"Slot {slotId}" });
            }
            return results;
        });

    /// <summary>Same as ReadOtherSlotsAsync but every known system, not just planets.</summary>
    public static Task<List<KnownSystem>> ReadOtherSlotsSystemsAsync(string platformFolder, int excludeSlotId) =>
        Task.Run(async () =>
        {
            var results = new List<KnownSystem>();
            foreach (var (slotId, path) in NewestFilePerSlot(platformFolder))
            {
                if (slotId == excludeSlotId) continue;
                foreach (var system in (await ReadFileAsync(path)).Systems)
                    results.Add(system with { Source = $"Slot {slotId}" });
            }
            return results;
        });

    private static IEnumerable<(int SlotId, string Path)> NewestFilePerSlot(string folder)
    {
        if (!Directory.Exists(folder)) return Enumerable.Empty<(int, string)>();

        return Directory.GetFiles(folder, "save*.hg", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetExtension(f).Equals(".hg", StringComparison.OrdinalIgnoreCase))
            .GroupBy(SlotIdFor)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.OrderByDescending(File.GetLastWriteTimeUtc).First()));
    }

    // Same pairing as SaveFolderIndexingService: (save, save2) -> 1, (save3, save4) -> 2...
    private static int SlotIdFor(string filePath)
    {
        string digits = new(Path.GetFileNameWithoutExtension(filePath).Where(char.IsDigit).ToArray());
        int fileIndex = int.TryParse(digits, out int n) ? n : 1;
        return ((fileIndex - 1) / 2) + 1;
    }

    private static async Task<(List<KnownPlanet> Planets, List<KnownSystem> Systems)> ReadFileAsync(string path)
    {
        DateTime writeTime = File.GetLastWriteTimeUtc(path);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out var cached) && cached.WriteTime == writeTime)
                return (cached.Planets, cached.Systems);
        }

        List<KnownPlanet> planets;
        List<KnownSystem> systems;
        try
        {
            string json = await SaveStreamProcessor.ExtractRawJsonAsync(path);
            var root = JObject.Parse(json.TrimEnd('\0'));
            var discoveries = root["fDu"]?["ETO"]?["OsQ"]?["?fB"] as JArray;
            planets = NmsPlanets.ReadAll(discoveries);
            systems = NmsPlanets.ReadSystems(discoveries);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KnownPlanetsService] Skipping {Path.GetFileName(path)}: {ex.Message}");
            planets = new();
            systems = new();
        }

        lock (CacheLock) Cache[path] = (writeTime, planets, systems);
        return (planets, systems);
    }
}

/// <summary>What PlanetsPage needs from one save: the player's location
/// (yhJ), stat groups (per-planet ^PLANET_STATS), the discovery list, the
/// "where you saved" line (n:R, e.g. "In the Plyimed system") and the map
/// markers (nlG - named space stations and bases), plus the bases (F?0)
/// and fishing records (bTf, see NmsFishingPaths) for other Tools pages.
/// Spawn (rnc) and PlanetPositions (aHM) give the player's coordinates on the
/// planet (NmsPlanetCoordinates).</summary>
public sealed record PlanetSnapshot(JToken? Universe, JArray? StatGroups, JArray? Discoveries, string? Summary = null, JArray? Markers = null, JArray? Bases = null, JToken? Fishing = null,
    JToken? Spawn = null, JToken? PlanetPositions = null)
{
    /// <summary>The player's coordinates on the planet they're on, if any.</summary>
    public (double Latitude, double Longitude)? Coordinates =>
        NmsPlanetCoordinates.Read(Spawn, PlanetPositions, Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0);
}

public static class LiveSaveReader
{
    /// <summary>Decrypts a save straight from disk for PlanetsPage's live
    /// follow, using whichever character is active (main game, or the
    /// expedition root when XTp is "Season" - same rule as
    /// SaveSessionManager). Read-only; returns null if the file can't be read
    /// yet (e.g. the game is still writing it).</summary>
    public static Task<PlanetSnapshot?> ReadAsync(string path) => Task.Run(async () =>
    {
        try
        {
            string json = await SaveStreamProcessor.ExtractRawJsonAsync(path);
            var root = JObject.Parse(json.TrimEnd('\0'));
            bool expedition = root[NmsSaveContext.ActiveContextKey]?.Value<string>() == NmsSaveContext.ExpeditionContextValue
                && root[NmsSaveContext.ExpeditionRootKey]?["6f="] is JObject;
            var context = root[expedition ? NmsSaveContext.ExpeditionRootKey : NmsSaveContext.MainRootKey];
            var player = context?["6f="];
            return new PlanetSnapshot(player?["yhJ"], player?["gUR"] as JArray, root["fDu"]?["ETO"]?["OsQ"]?["?fB"] as JArray,
                player?["n:R"]?.Value<string>(), player?["nlG"] as JArray, player?["F?0"] as JArray, player?["bTf"],
                context?["rnc"], player?["aHM"]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiveSaveReader] Couldn't read {Path.GetFileName(path)}: {ex.Message}");
            return (PlanetSnapshot?)null;
        }
    });
}

/// <summary>
/// Shared "follow the game" helper for read-only Tools pages: watches the
/// loaded slot's save files and, ~2.5s after the game finishes writing,
/// decrypts the newest one (LiveSaveReader) and raises SnapshotRead on the UI
/// thread. Never touches SaveSessionManager's editing session. Dispose (or
/// Stop) when the page unloads.
/// </summary>
public sealed class LiveSaveWatcher : IDisposable
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
    private FileSystemWatcher? _watcher;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _timer;

    /// <summary>Raised on the UI thread with each new snapshot.</summary>
    public event Action<PlanetSnapshot>? SnapshotRead;

    public LiveSaveWatcher(Microsoft.UI.Dispatching.DispatcherQueue dispatcher) => _dispatcher = dispatcher;

    /// <summary>Snapshot of the loaded session (what the page shows before any live update).</summary>
    public static PlanetSnapshot FromSession() => new(
        SaveSessionManager.GetValue(NmsSpaceStationPaths.CurrentUniverseAddressPath),
        SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) as JArray,
        SaveSessionManager.GetValue(NmsSpaceStationPaths.DiscoveriesPath) as JArray,
        SaveSessionManager.GetValue(NmsSpaceStationPaths.LocationDescriptionPath)?.Value<string>(),
        SaveSessionManager.GetValue(NmsSpaceStationPaths.MarkersPath) as JArray,
        SaveSessionManager.GetValue("vLc", "6f=", "F?0") as JArray,
        SaveSessionManager.GetValue(NmsFishingPaths.RecordContainerPath),
        SaveSessionManager.GetValue(NmsPlanetCoordinates.SpawnStatePath),
        SaveSessionManager.GetValue(NmsPlanetCoordinates.PlanetPositionsPath));

    public void Start()
    {
        Stop();
        if (SaveSessionManager.ActivePlatformFolder is not string folder || !Directory.Exists(folder)) return;

        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(2.5);
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => _ = ReadAsync(retry: true);

        _watcher = new FileSystemWatcher(folder, "save*.hg")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnWritten;
        _watcher.Created += OnWritten;
        _watcher.Renamed += OnWritten;
        _watcher.EnableRaisingEvents = true;

        // Read the newest save straight away: pages start from the copy loaded
        // when ArtifactX started, which can be long out of date - Finds' "Use my
        // current location" gave a spot the player had left (2026-10-14).
        _timer.Start();
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnWritten;
            _watcher.Created -= OnWritten;
            _watcher.Renamed -= OnWritten;
            _watcher.Dispose();
            _watcher = null;
        }
        _timer?.Stop();
        _timer = null;
    }

    public void Dispose() => Stop();

    private void OnWritten(object sender, FileSystemEventArgs e)
    {
        var slot = SaveSessionManager.ActiveSlot;
        if (slot is null || !slot.Files.Any(f => string.Equals(f.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase))) return;
        _dispatcher.TryEnqueue(() =>
        {
            _timer?.Stop();
            _timer?.Start();
        });
    }

    private async Task ReadAsync(bool retry)
    {
        if (SaveSessionManager.ActiveSlot is not { } slot) return;
        string newest = slot.Files.Select(f => f.FullPath).Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? "";
        if (newest.Length == 0) return;

        var snapshot = await LiveSaveReader.ReadAsync(newest);
        if (snapshot is null)
        {
            if (retry) { await Task.Delay(3000); await ReadAsync(retry: false); }
            return;
        }
        SystemNameLearner.Learn(NmsSpaceStationPaths.ReadSystemAddress(snapshot.Universe),
            snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0, snapshot.Summary);
        SnapshotRead?.Invoke(snapshot);
    }
}
