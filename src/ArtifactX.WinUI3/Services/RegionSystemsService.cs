using ArtifactX.Core.Universe;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// How many star systems a region holds, from the game's own region generator
/// (<see cref="RegionStarGeneration"/>) with the catalog's galaxy-generation settings.
/// Results are cached per region; a catalog rebuild clears them.
/// </summary>
public static class RegionSystemsService
{
    private static readonly ConcurrentDictionary<ulong, RegionSystemCount> Cache = new();
    private static readonly ConcurrentDictionary<ulong, IReadOnlyList<RegionStar>?> StarCache = new();
    private static object? _catalogStamp;

    private static void CheckCatalog()
    {
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (Equals(stamp, _catalogStamp)) return;
        Cache.Clear();
        StarCache.Clear();
        _catalogStamp = stamp;
    }

    /// <summary>
    /// Every star of the region with its position (ordinary systems, then purple), from
    /// <see cref="RegionStarPositions"/>. Null for regions the port does not cover yet (spiral
    /// regions, the galactic centre) and on catalogs before format 45.
    /// </summary>
    public static async Task<IReadOnlyList<RegionStar>?> GetStarsAsync(ulong systemAddress)
    {
        CheckCatalog();
        ulong key = RegionStarGeneration.RegionKey(systemAddress);
        if (StarCache.TryGetValue(key, out var cached)) return cached;
        if (await CatalogService.GetGalaxyGenerationSettingsAsync() is not { } settings
            || await CatalogService.GetGalaxyPlacementSettingsAsync() is not { } placement) return null;
        var stars = await Task.Run(() => RegionStarPositions.Stars((int)((key >> 32) & 0xFF), (int)(key & 0xFFF),
            (int)((key >> 24) & 0xFF), (int)((key >> 12) & 0xFFF), settings, placement));
        StarCache[key] = stars;
        return stars;
    }

    /// <summary>Null when the catalog predates format 44 (no galaxy-generation settings).</summary>
    public static async Task<RegionSystemCount?> GetAsync(ulong systemAddress)
    {
        CheckCatalog();
        ulong key = RegionStarGeneration.RegionKey(systemAddress);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        if (await CatalogService.GetGalaxyGenerationSettingsAsync() is not { } settings) return null;
        var count = await Task.Run(() => RegionStarGeneration.Count(key, settings));
        Cache[key] = count;
        return count;
    }
}
