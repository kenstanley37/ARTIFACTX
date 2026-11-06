using System.Globalization;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// "BUILDINGLAYOUTS" (Core BuildingLayoutTables): every L-system the building table names (each race's
    /// GcBuildingFilename.LSystem lists) with their templates, every model those rules place - candidate locators
    /// and mesh box, as lsysport.py reads them - and GcBuildingGlobals.MinRadius. Scene reading follows the port
    /// checked 2026-11-06 against 24/24 live layouts: the scene root's own transform is not applied; locators are
    /// direct children of the root, or children of an "Interior_" locator; the mesh box takes MESH nodes' AABB
    /// attributes and REFERENCE (SCENEGRAPH) sub-scenes, transforms composed as translation * Rx * Ry * Rz * scale.
    /// </summary>
    internal static BuildingLayoutTables? BuildingLayouts(Func<string, NMSTemplate?> decode)
    {
        if (decode(BuildingTableFile) is not GcBuildingDefinitionTable table) return null;
        var globals = decode("GCBUILDINGGLOBALS.GLOBAL.MBIN") as GcBuildingGlobals;
        float minRadius = globals?.MinRadius ?? 10f;
        float chanceOfAddingShelter = globals?.ChanceOfAddingShelter ?? 0.75f;

        static string PathOf(string? path) => (path ?? "").Replace('\\', '/').ToUpperInvariant();
        static LSystemRestriction[] Restrictions(List<TkLSystemRestrictionData>? list) =>
            (list ?? []).Select(r => new LSystemRestriction((int)r.Restriction, r.Amount)).ToArray();

        var lsystems = new Dictionary<string, LSystemFile>(StringComparer.Ordinal);
        var pending = new Queue<string>(table.BuildingFiles
            .SelectMany(f => f?.BuildingFiles ?? [])
            .SelectMany(f => f?.LSystem ?? [])
            .Select(f => PathOf(f?.Value))
            .Where(p => p.Length > 0));
        while (pending.Count > 0)
        {
            string path = pending.Dequeue();
            if (lsystems.ContainsKey(path) || decode(path) is not TkLSystemRulesData data) continue;
            var file = new LSystemFile(
                (data.Templates ?? []).Select(t => PathOf(t.LSystem?.Value)).Where(p => p.Length > 0).ToArray(),
                (data.GlobalRestriction ?? []).Select(g => new LSystemGlobal(PathOf(g.Model?.Value), Restrictions(g.Restrictions))).ToArray(),
                (data.Rules ?? []).Select(r => new LSystemRule(r.Name?.Value ?? "", PathOf(r.Model?.Value),
                    r.RuleType == TkLSystemRule.RuleTypeEnum.BaseRule,
                    (r.Rules ?? []).Select(i => new LSystemInnerRule(i.LocatorType?.Value ?? "", (int)i.MergeProbabilityOptions,
                        (i.Entries ?? []).Select(e => new LSystemEntry(PathOf(e.Model?.Value), e.Probability, Restrictions(e.Restrictions))).ToArray())).ToArray()))
                    .ToArray());
            lsystems[path] = file;
            foreach (var t in file.Templates) pending.Enqueue(t);
        }
        if (lsystems.Count == 0) return null;

        var locatorTypes = lsystems.Values.SelectMany(f => f.Rules).SelectMany(r => r.Rules)
            .Select(i => i.LocatorType.ToUpperInvariant()).Where(t => t.Length > 0).ToHashSet();
        var models = lsystems.Values.SelectMany(f => f.Rules)
            .SelectMany(r => r.Rules.SelectMany(i => i.Entries).Select(e => e.Model).Append(r.Model))
            .Where(m => m.Length > 0).ToHashSet();

        var scenes = new Dictionary<string, LayoutScene>(StringComparer.Ordinal);
        var boxes = new Dictionary<string, (double[] Min, double[] Max)?>(StringComparer.Ordinal);
        foreach (string model in models.Order(StringComparer.Ordinal))
        {
            if (decode(model) is not TkSceneNodeData root) continue;
            var locators = new List<LayoutLocator>();
            double[]? weaponCrate = null;
            void Walk(TkSceneNodeData node, LayoutTransform parent, int depth, string parentName)
            {
                var world = parent.Then(LayoutLocal(node.Transform));
                string name = node.Name?.Value ?? "";
                if (node.Type?.Value == "LOCATOR" && (depth == 1 || (depth == 2 && parentName.ToUpperInvariant() == "INTERIOR_"))
                    && locatorTypes.Any(t => name.ToUpperInvariant().Contains(t, StringComparison.Ordinal)))
                    locators.Add(new LayoutLocator(name, LayoutValues(world)));
                else if (node.Type?.Value == "REFERENCE" && weaponCrate is null
                    && (node.Attributes ?? []).Any(a => a.Name?.Value == "SCENEGRAPH"
                        && (a.Value?.Value ?? "").Contains("CRATE_WEAPON", StringComparison.OrdinalIgnoreCase)))
                    weaponCrate = LayoutValues(world);
                foreach (var c in node.Children ?? []) Walk(c, world, depth + 1, name);
            }
            foreach (var c in root.Children ?? []) Walk(c, LayoutTransform.Identity, 1, "");
            var box = ModelBox(model, decode, boxes, 0);
            scenes[model] = new LayoutScene(locators.ToArray(), box?.Min, box?.Max, weaponCrate);
        }

        var clusterLayouts = new Dictionary<string, BuildingClusterEntry[]>(StringComparer.Ordinal);
        foreach (var cl in table.ClusterLayouts ?? [])
        {
            string id = (cl.ID?.Value ?? "").ToUpperInvariant();
            if (id.Length == 0 || clusterLayouts.ContainsKey(id)) continue;
            clusterLayouts[id] = (cl.ClusterBuildings ?? [])
                .Select(e => new BuildingClusterEntry((int)e.Building.BuildingClass, e.Min, e.Max, e.Probability))
                .ToArray();
        }
        var classClusters = new Dictionary<int, string>();
        for (int c = 0; c < (table.BuildingPlacement?.Length ?? 0); c++)
        {
            string id = (table.BuildingPlacement![c]?.ClusterLayout?.Value ?? "").ToUpperInvariant();
            if (id.Length > 0) classClusters[c] = id;
        }
        var buildingLSystems = (table.BuildingFiles ?? [])
            .Select(r => (r?.BuildingFiles ?? [])
                .Select(b => (b?.LSystem ?? []).Select(f => PathOf(f?.Value)).ToArray())
                .ToArray())
            .ToArray();

        return new BuildingLayoutTables(lsystems, scenes, minRadius, chanceOfAddingShelter, clusterLayouts, classClusters, buildingLSystems);
    }

    /// <summary>The box around a model's MESH AABBs in its own frame, every mesh of REFERENCE sub-scenes taken through
    /// the whole transform chain (not the sub-scene's box: a rotated box would grow). Cached per top-level model.</summary>
    private static (double[] Min, double[] Max)? ModelBox(string path, Func<string, NMSTemplate?> decode,
        Dictionary<string, (double[] Min, double[] Max)?> cache, int depth)
    {
        if (cache.TryGetValue(path, out var cached)) return cached;
        double[] min = [double.MaxValue, double.MaxValue, double.MaxValue], max = [double.MinValue, double.MinValue, double.MinValue];
        bool any = false;
        void Scene(string scene, LayoutTransform at, int level)
        {
            if (level > 16 || decode(scene) is not TkSceneNodeData root) return;
            foreach (var c in root.Children ?? []) Walk(c, at, level);
        }
        void Walk(TkSceneNodeData node, LayoutTransform parent, int level)
        {
            var world = parent.Then(LayoutLocal(node.Transform));
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var a in node.Attributes ?? []) attributes.TryAdd(a.Name?.Value ?? "", a.Value?.Value ?? "");
            string type = node.Type?.Value ?? "";
            if (type == "MESH" && LayoutCorner(attributes, "MIN") is { } lo && LayoutCorner(attributes, "MAX") is { } hi)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = world.Apply((c & 4) != 0 ? hi[0] : lo[0], (c & 2) != 0 ? hi[1] : lo[1], (c & 1) != 0 ? hi[2] : lo[2]);
                    double[] v = [p.X, p.Y, p.Z];
                    for (int k = 0; k < 3; k++) { min[k] = Math.Min(min[k], v[k]); max[k] = Math.Max(max[k], v[k]); }
                }
                any = true;
            }
            else if (type == "REFERENCE" && attributes.TryGetValue("SCENEGRAPH", out var sub))
                Scene(sub.Replace('\\', '/').ToUpperInvariant(), world, level + 1);
            foreach (var c in node.Children ?? []) Walk(c, world, level);
        }
        Scene(path, LayoutTransform.Identity, depth);
        return cache[path] = any ? (min, max) : null;
    }

    private static LayoutTransform LayoutLocal(TkTransformData t) =>
        LayoutTransform.FromScene(t.TransX, t.TransY, t.TransZ, t.RotX, t.RotY, t.RotZ, t.ScaleX, t.ScaleY, t.ScaleZ);

    private static double[] LayoutValues(LayoutTransform m) => Enumerable.Range(0, 12).Select(i => m[i]).ToArray();

    private static double[]? LayoutCorner(Dictionary<string, string> attributes, string which) =>
        attributes.TryGetValue($"AABB{which}X", out var x) && attributes.TryGetValue($"AABB{which}Y", out var y) && attributes.TryGetValue($"AABB{which}Z", out var z)
        && double.TryParse(x, CultureInfo.InvariantCulture, out var fx)
        && double.TryParse(y, CultureInfo.InvariantCulture, out var fy)
        && double.TryParse(z, CultureInfo.InvariantCulture, out var fz)
            ? [fx, fy, fz] : null;
}
