using ArtifactX.Core.Procedural;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The part trees (descriptor lists) of procedural models, as the catalog stores them (category "ShipDescriptors":
/// starships, multi-tools and, since format 87, creatures), parsed once per path for ShipParts. Read again after a
/// game-data rebuild. Shared by the starship, multi-tool and creature services, so none of them reaches into another's.
/// </summary>
public static class PartTreeService
{
    private static readonly Dictionary<string, IReadOnlyList<PartGroup>> Cache = new();
    private static Dictionary<string, string>? _trees;
    private static object? _stamp;

    /// <summary>Reads the trees from the catalog (again after a rebuild).</summary>
    public static async Task EnsureAsync()
    {
        var stamp = GameDataService.Info?.BuiltAtUtc;
        if (_trees is not null && _trees.Count > 0 && Equals(stamp, _stamp)) return;
        _stamp = stamp;
        lock (Cache) Cache.Clear();
        _trees = await CatalogService.GetShipPartTreesAsync();
    }

    /// <summary>A model's part tree by lower-case descriptor path, once EnsureAsync has run; null when missing.</summary>
    public static IReadOnlyList<PartGroup>? Load(string path)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var tree)) return tree;
            if (_trees is null || !_trees.TryGetValue(path, out var json)) return null;
            return Cache[path] = ShipParts.ParseTree(json);
        }
    }
}
