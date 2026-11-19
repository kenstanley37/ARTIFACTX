using ArtifactX.Core.Universe;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// The object tables with <see cref="ObjectTables.Spawn"/> and <see cref="ObjectTables.DiscoveryKinds"/> filled
    /// (catalog format 108, PlanetProfileGenerator.FloraAndMinerals): every spawn file the lists name, its objects by
    /// category in file order, and which discovery kinds each placed scene can be scanned as.
    /// </summary>
    private static ObjectTables ObjectTablesWithSpawns(ObjectTables tables, Func<string, NMSTemplate?> decode)
    {
        var lists = tables.BiomeFiles.SelectMany(b => b).OfType<BiomeObjects>().SelectMany(b => b.Lists)
            .Concat(tables.Common).Concat(tables.Groups.SelectMany(g => g.Files));
        var spawn = new Dictionary<string, SpawnObject[][]>(StringComparer.Ordinal);
        var kinds = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var sceneKinds = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (string file in lists.SelectMany(l => l.Files ?? []).Distinct(StringComparer.Ordinal))
        {
            if (decode(file) is not GcExternalObjectList list || list.Objects is not { } objects) continue;
            var categories = new[] { objects.Landmarks, objects.Objects, objects.DetailObjects, objects.DistantObjects }
                .Select(c => (c ?? new()).Select(o => new SpawnObject(Scene(o.Resource?.Filename?.Value),
                    (o.AltResources ?? new()).Select(a => Scene(a.Filename?.Value)).ToArray(),
                    o.Order, (int)o.PlacementPriority, o.SwapPrimaryForRandomColour)).ToArray())
                .ToArray();
            spawn[file] = categories;
            foreach (string model in categories.SelectMany(c => c).SelectMany(o => o.Alts.Prepend(o.Model)))
                if (model.Length > 0 && !kinds.ContainsKey(model) && Kinds(model, 0) is { Length: > 0 } found)
                    kinds[model] = found;
        }
        if (spawn.Count == 0) ConsoleStyle.Warning("No spawn files read - planets' plant and mineral discoveries left out.");
        return tables with { Spawn = spawn, DiscoveryKinds = kinds };

        static string Scene(string? path) => (path ?? "").Replace('\\', '/').ToUpperInvariant();

        // The kinds a scene's GcEncyclopediaComponentData entities give, its own and those of scenes it references
        // (FISHFIENDROCK and HQTREEREF have theirs only in referenced scenes). A scene with both (LARGEFUNGHI,
        // RARELARGE) is recorded under each: both kinds appear in the owner's Slot 8 save (2026-11-19).
        string[] Kinds(string scene, int depth)
        {
            if (sceneKinds.TryGetValue(scene, out var known)) return known;
            sceneKinds[scene] = []; // a scene referencing itself ends here
            if (depth > 8 || decode(scene) is not TkSceneNodeData root) return [];
            var found = new SortedSet<string>(StringComparer.Ordinal);
            var stack = new Stack<TkSceneNodeData>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var attr in node.Attributes ?? new())
                {
                    string value = Scene(attr.Value?.Value);
                    if (value.Length == 0) continue;
                    if (attr.Name?.Value == "ATTACHMENT" && decode(value) is TkAttachmentData entity)
                        foreach (var component in entity.Components ?? new())
                            if (component.Template is GcEncyclopediaComponentData { Type.DiscoveryType: var type }
                                && type is GcDiscoveryType.DiscoveryTypeEnum.Flora or GcDiscoveryType.DiscoveryTypeEnum.Mineral)
                                found.Add(type.ToString());
                    if (attr.Name?.Value == "SCENEGRAPH" && node.Type?.Value == "REFERENCE")
                        found.UnionWith(Kinds(value, depth + 1));
                }
                foreach (var child in node.Children ?? new()) stack.Push(child);
            }
            return sceneKinds[scene] = found.ToArray();
        }
    }
}
