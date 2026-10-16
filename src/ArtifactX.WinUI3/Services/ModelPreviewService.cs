using ArtifactX.Core.Procedural;
using ArtifactX.GameData;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// What the 3D previews (Starships, Multi-Tool) share: the model reader for
/// the player's game files, and the part ids a model's tree can hold.
/// </summary>
public static class ModelPreviewService
{
    // Indexing every PAK takes a moment, so the reader is made once per session.
    private static Task<ShipModels?>? _models;

    public static Task<ShipModels?> ModelsAsync(GameInstall game) =>
        _models ??= Task.Run(() => { try { return new ShipModels(game); } catch { return (ShipModels?)null; } });

    /// <summary>Every option id reachable from a tree, through nested groups and
    /// the trees of the scenes its options use - which parts the model can have at all.</summary>
    public static HashSet<string> AllOptionIds(IReadOnlyList<PartGroup> root, Func<string, IReadOnlyList<PartGroup>?> loadTree,
        StringComparer comparer)
    {
        var ids = new HashSet<string>(comparer);
        var seenTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(IReadOnlyList<PartGroup> groups)
        {
            foreach (var g in groups)
                foreach (var o in g.Options)
                {
                    ids.Add(o.Id);
                    foreach (var kid in o.Children) Walk(kid);
                    foreach (var scene in o.Scenes)
                    {
                        string path = ShipParts.DescriptorFor(scene);
                        if (seenTrees.Add(path) && loadTree(path) is { } sub) Walk(sub);
                    }
                }
        }
        Walk(root);
        return ids;
    }
}
