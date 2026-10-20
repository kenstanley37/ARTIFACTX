using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Follows the loaded save for the whole session and hands each new save to
/// <see cref="FishCatchService.Observe"/>, whatever page is open (2026-10-15 -
/// it used to run only while the Fishing or Planets page was open, so catches
/// made on another page were missed, or all credited to the planet the player
/// was on when they came back). Those pages still observe their own reads;
/// that's harmless - the starting counts only move up, so a catch is recorded
/// once, by whichever look sees it first.
/// Started once from MainWindow; restarts when another save is loaded.
/// </summary>
public static class FishCatchRecorder
{
    private static DispatcherQueue? _dispatcher;
    private static LiveSaveWatcher? _live;
    private static List<FishInfo> _fish = new();
    private static int _version;

    /// <summary>True while a save is being followed; raised on the UI thread.</summary>
    public static bool IsActive => _live is not null;
    public static event Action? ActiveChanged;

    /// <summary>Every save snapshot it reads, the session's first - for other listeners
    /// that need the player's location (PlanetPageReader).</summary>
    public static event Action<PlanetSnapshot>? SnapshotSeen;

    public static void Start(DispatcherQueue dispatcher)
    {
        if (_dispatcher is not null) return;
        _dispatcher = dispatcher;
        SaveSessionManager.ActiveSessionChanged += (_, _) => _dispatcher.TryEnqueue(Restart);
        Restart();
    }

    private static async void Restart()
    {
        int version = ++_version;
        _live?.Dispose();
        _live = null;
        if (_dispatcher is null || !SaveSessionManager.IsSaveLoaded) { ActiveChanged?.Invoke(); return; }

        // The fish table comes from the game data, which may still be being
        // built on a first run - try again on the next restart if it's empty.
        if (_fish.Count == 0)
        {
            try { _fish = (await CatalogService.GetFishAsync()).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList(); }
            catch { _fish = new(); }
            if (version != _version) return;
        }

        var first = LiveSaveWatcher.FromSession();
        FishCatchService.Observe(FishCatchService.ActiveSaveKey(), first, _fish); // starting counts
        SnapshotSeen?.Invoke(first);
        var live = new LiveSaveWatcher(_dispatcher);
        live.SnapshotRead += snapshot =>
        {
            LiveSaveWatcher.LatestSnapshot = snapshot;
            FishCatchService.Observe(FishCatchService.ActiveSaveKey(), snapshot, _fish);
            SnapshotSeen?.Invoke(snapshot);
        };
        _live = live;
        live.Start();
        ActiveChanged?.Invoke();
    }
}
