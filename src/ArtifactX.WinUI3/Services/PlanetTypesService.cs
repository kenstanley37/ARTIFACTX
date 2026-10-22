using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Every body in a system with its type, discovered or not (2026-10-22): the game's
/// solar-system generator ported to ArtifactX.Core.PlanetGenerator, fed the type tables
/// from the player's own game files (catalog "PlanetGeneration"). Exact on 1,072 of
/// 1,074 planets recorded in a real save - so a planet's type no longer has to be read
/// off the game screen or typed in.
/// </summary>
public static class PlanetTypesService
{
    private static BiomeTables? _tables;
    private static Task? _loading;
    private static readonly ConcurrentDictionary<ulong, PlanetType[]> Cache = new();

    /// <summary>Raised (any thread) once the tables are loaded.</summary>
    public static event Action? Loaded;

    public static bool IsReady => _tables is not null;

    /// <summary>Loads the tables once they exist. Tried again while they're missing, and
    /// again after a game data rebuild - the app asked before a rebuild had finished, kept
    /// "none" and never showed a type (2026-10-22).</summary>
    public static Task EnsureLoadedAsync()
    {
        if (!_subscribed)
        {
            _subscribed = true;
            GameDataService.BuildFinished += error => { _tables = null; _loading = null; Cache.Clear(); _ = EnsureLoadedAsync(); };
        }
        return _loading ??= LoadAsync();
    }

    private static bool _subscribed;

    private static async Task LoadAsync()
    {
        _tables = await CatalogService.GetBiomeTablesAsync();
        if (_tables is null) { _loading = null; return; }
        Cache.Clear();
        Loaded?.Invoke();
    }

    /// <summary>Each body's type in planet order (index 0 = planet 1); empty until the
    /// tables are loaded or on game data built before they were collected.</summary>
    public static PlanetType[] For(ulong system)
    {
        if (_tables is not { } tables) return Array.Empty<PlanetType>();
        system = S.ToSystemAddress(system);
        return Cache.GetOrAdd(system, a =>
        {
            ulong portal = (((a >> 40) & 0xFFF) << 32) | (a & 0xFFFFFFFF);
            try { return PlanetGenerator.Generate(portal, (int)((a >> 32) & 0xFF), tables); }
            catch { return Array.Empty<PlanetType>(); }
        });
    }

    /// <summary>The bodies in <paramref name="system"/> the save doesn't have, as planets
    /// with their predicted type.</summary>
    public static IEnumerable<KnownPlanet> Undiscovered(ulong system, ICollection<int> known)
    {
        var types = For(system);
        ulong systemAddress = S.ToSystemAddress(system);
        for (int i = 0; i < types.Length; i++)
        {
            int index = i + 1;
            if (known.Contains(index)) continue;
            yield return new KnownPlanet(systemAddress | ((ulong)index << 52), S.GalaxyNumber(systemAddress), index,
                types[i].Biome, false, "", false, 0) { Predicted = true };
        }
    }
}
