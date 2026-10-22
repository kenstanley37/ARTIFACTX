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
/// off the game screen or typed in. Each body's profile too - resources, weather,
/// sentinels (PlanetProfileGenerator, 99% of 808 recorded planets), all from the address.
/// </summary>
public static class PlanetTypesService
{
    private static BiomeTables? _tables;
    private static PlanetProfileTables? _profileTables;
    private static Task? _loading;
    private static readonly ConcurrentDictionary<ulong, PlanetType[]> Cache = new();
    private static readonly ConcurrentDictionary<ulong, PlanetProfile?[]> Profiles = new();

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
            GameDataService.BuildFinished += error =>
            {
                _tables = null; _profileTables = null; _loading = null;
                Cache.Clear(); Profiles.Clear();
                _ = EnsureLoadedAsync();
            };
        }
        return _loading ??= LoadAsync();
    }

    private static bool _subscribed;

    private static async Task LoadAsync()
    {
        _tables = await CatalogService.GetBiomeTablesAsync();
        if (_tables is null) { _loading = null; return; }
        _profileTables = await CatalogService.GetPlanetProfileTablesAsync();
        Cache.Clear();
        Profiles.Clear();
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
            var (portal, galaxy) = Portal(a);
            try { return PlanetGenerator.Generate(portal, galaxy, tables); }
            catch { return Array.Empty<PlanetType>(); }
        });
    }

    /// <summary>A body's resources, weather and sentinels (planet index from 1); null until
    /// the tables are loaded, on game data built before catalog format 16, or for a body
    /// the system doesn't have.</summary>
    public static PlanetProfile? Profile(ulong system, int planetIndex)
    {
        if (_profileTables is not { } tables) return null;
        system = S.ToSystemAddress(system);
        var all = Profiles.GetOrAdd(system, a =>
        {
            var types = For(a);
            var (portal, galaxy) = Portal(a);
            var profiles = new PlanetProfile?[types.Length];
            try
            {
                var attributes = SystemGenerator.Attributes(portal, galaxy);
                var seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
                for (int i = 0; i < types.Length && i < seeds.Length; i++)
                    profiles[i] = PlanetProfileGenerator.Generate(seeds[i], i, types[i].Biome, types[i].SubType, types[i].Class,
                        (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, tables);
            }
            catch { /* leave the rest unknown */ }
            return profiles;
        });
        int index = planetIndex - 1;
        return index >= 0 && index < all.Length ? all[index] : null;
    }

    /// <summary>The game's English text for a scanner hint that isn't an item
    /// ("UI_BUGS_HINT" - "Vile Brood Detected"), or null.</summary>
    public static string? HintName(string id) =>
        _profileTables?.HintNames is { } names && names.TryGetValue(id, out var name) ? name : null;

    private static (ulong Portal, int Galaxy) Portal(ulong system) =>
        ((((system >> 40) & 0xFFF) << 32) | (system & 0xFFFFFFFF), (int)((system >> 32) & 0xFF));

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
