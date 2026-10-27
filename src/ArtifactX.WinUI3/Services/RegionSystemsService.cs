using ArtifactX.Core.Universe;
using System.Collections.Concurrent;
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
    private static object? _catalogStamp;

    /// <summary>Null when the catalog predates format 44 (no galaxy-generation settings).</summary>
    public static async Task<RegionSystemCount?> GetAsync(ulong systemAddress)
    {
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (!Equals(stamp, _catalogStamp)) { Cache.Clear(); _catalogStamp = stamp; }
        ulong key = RegionStarGeneration.RegionKey(systemAddress);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        if (await CatalogService.GetGalaxyGenerationSettingsAsync() is not { } settings) return null;
        var count = await Task.Run(() => RegionStarGeneration.Count(key, settings));
        Cache[key] = count;
        return count;
    }
}
