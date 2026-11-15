using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

/// <summary>One part of a corvette build as the save keeps it (@ZJ: r&lt;7 id, wMC position, wJ0 up, aNu at),
/// in the ship's own space.</summary>
public sealed record CorvettePart(string Id, Vector3 Position, Vector3 Up, Vector3 At, ulong UserData = 0);

/// <summary>A sub-part scene the build is drawn with, where it sits in ship space, and its part's colour palette
/// (index into GcBaseBuildingTable.Palettes; -1 none).</summary>
public sealed record CorvettePiece(string Scene, Matrix4x4 World, int Palette = -1);

/// <summary>An assembled corvette: its pieces and the size the game measures for it
/// (<c>cGcBuildableSpaceshipComponent</c> +0x90 centre, +0xA0 size = half the mesh box plus 1 m).</summary>
public sealed record CorvetteBuild(IReadOnlyList<CorvettePiece> Pieces, Vector3 Centre, Vector3 Size);

/// <summary>
/// Puts a corvette together from its saved parts the way the game does (#73). A part's placement scene
/// (GcBaseBuildingEntry.PlacementScene) carries only snap points and an entity whose
/// GcBasePlacementComponentData rules pick the visible sub-parts (basebuildingpartstable style models):
/// a rule applies when its conditions hold - GcSnapPointCondition (a snap point is or isn't attached),
/// GcOutSnapSocketCondition (one of its "_Out_" sockets is attached) and GcGroupCondition (AND / OR) - and
/// places its sub-part at each locator named by PositionLocator. Two parts are attached at a snap point when
/// both have one at the same spot and one's "X_Out_" socket meets the other's "X_In_" (and vice versa).
///
/// The size is the game's RecalculatePartOffsetandExtents (NMS.exe 184271 0x14176D430): the box round every
/// visible mesh in ship space, padded 1 m each side, stored as centre and half-size. Checked 2026-11-15 against
/// the running game (read-only memory) on Slot 8's 71-part corvette: game centre (0, 7.441, 7.575), size
/// (25.973, 9.249, 28.813); this gives (0, 7.447, 7.573), (25.973, 9.238, 28.811). The width only matches with
/// the meshes the game had on screen: LOD2 where a mesh has levels (the wing's LOD2 bound 9.9729 is the game's
/// 24.9729), and a mesh group without that level left out. Placement ghosts, ExclusionVolumes / MESHBOUNDS
/// helpers and the cockpit's SpeedCool shell don't count; its ReEntryTunnel does (it sets the front, 35.38 vs
/// 35.388). The last centimetre of height is the magnet's tractor-beam cone, which the game measures wherever
/// its animation happened to be: Slot 8's Maneuverability comes out 454.81 from this size, 454.66 from the game's
/// (the game shows 454.7).
/// </summary>
public sealed class CorvetteAssembly
{
    private const int MeasuredLod = 2;
    private const float SnapDistance = 0.05f;
    private static readonly Regex LodSuffix = new(@"LOD\d$", RegexOptions.IgnoreCase);
    private static readonly HashSet<string> NotMeasured = new(StringComparer.OrdinalIgnoreCase) { "ExclusionVolumes", "MESHBOUNDS", "SpeedCool" };

    private readonly ShipModels _models;
    private readonly Dictionary<string, string> _placementScenes;
    private readonly Dictionary<string, List<(GcBaseBuildingPartStyle.StyleEnum Style, string Scene)>> _subPartScenes;
    private readonly Dictionary<string, GcBaseBuildingPartStyle.StyleEnum> _styles;
    private readonly Dictionary<string, int> _defaultPalettes;
    private readonly IReadOnlyList<float[][]> _palettes;

    private CorvetteAssembly(ShipModels models, Dictionary<string, string> placement,
        Dictionary<string, List<(GcBaseBuildingPartStyle.StyleEnum, string)>> subParts,
        Dictionary<string, GcBaseBuildingPartStyle.StyleEnum> styles, Dictionary<string, int> defaultPalettes, IReadOnlyList<float[][]> palettes)
    {
        _models = models;
        _placementScenes = placement;
        _subPartScenes = subParts;
        _styles = styles;
        _defaultPalettes = defaultPalettes;
        _palettes = palettes;
    }

    /// <summary>Null when the game's base building tables can't be read.</summary>
    public static CorvetteAssembly? Create(ShipModels models)
    {
        if (models.Decode("METADATA/REALITY/TABLES/BASEBUILDINGOBJECTSTABLE.MBIN") is not GcBaseBuildingTable objects
            || models.Decode("METADATA/REALITY/TABLES/BASEBUILDINGPARTSTABLE.MBIN") is not GcBaseBuildingPartsTable partsTable) return null;
        var placement = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in objects.Objects)
            if (o.PlacementScene?.Filename?.Value is { Length: > 0 } scene) placement.TryAdd(o.ID.Value, scene);
        var subParts = new Dictionary<string, List<(GcBaseBuildingPartStyle.StyleEnum, string)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in partsTable.Parts)
        {
            var models_ = (p.StyleModels ?? new()).Where(m => m.Model?.Filename?.Value is { Length: > 0 })
                .Select(m => (m.Style?.Style ?? default, m.Model.Filename.Value)).ToList();
            if (models_.Count > 0) subParts.TryAdd(p.ID.Value, models_);
        }
        var styles = new Dictionary<string, GcBaseBuildingPartStyle.StyleEnum>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in objects.Objects)
            if (o.Style is { } style) styles.TryAdd(o.ID.Value, style.Style);
        float[] C(libMBIN.NMS.Colour c) => [c.R, c.G, c.B];
        var palettes = (objects.Palettes ?? new()).Select(p => new[] { C(p.PrimaryColour), C(p.SecondaryColour), C(p.TernaryColour), C(p.QuaternaryColour) }).ToList();
        var paletteIndex = (objects.Palettes ?? new()).Select((p, i) => (p.Id.Value, i)).GroupBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        var defaults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in objects.Objects)
            if (o.DefaultColourPaletteId?.Value is { Length: > 0 } id && paletteIndex.TryGetValue(id, out int index)) defaults.TryAdd(o.ID.Value, index);
        return new CorvetteAssembly(models, placement, subParts, styles, defaults, palettes);
    }

    /// <summary>The build's pieces and measured size; null when no part could be placed.</summary>
    public CorvetteBuild? Assemble(IReadOnlyList<CorvettePart> saved)
    {
        var parts = new List<Placed>();
        foreach (var s in saved)
        {
            string id = s.Id.TrimStart('^');
            if (!_placementScenes.TryGetValue(id, out var scene) || _models.Decode(scene) is not TkSceneNodeData root) continue;
            var placed = new Placed(id, PartMatrix(s), scene, PaletteOf(id, s.UserData), _styles.GetValueOrDefault(id));
            CollectSnaps(root, Matrix4x4.Identity, placed, 0);
            parts.Add(placed);
        }
        if (parts.Count == 0) return null;

        var pieces = new List<CorvettePiece>();
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var part in parts) Place(part.Scene, part.World, 0, part, parts, pieces, ref min, ref max);
        if (min.X > max.X) return null;
        return new CorvetteBuild(pieces, (min + max) * 0.5f, (max + Vector3.One - (min - Vector3.One)) * 0.5f);
    }

    /// <summary>
    /// The build's outside for the 3D preview. Wall, floor and ceiling pieces carry both faces, so the inside is left
    /// out by material: the cabins' surfaces (COR_A_INT_MAT, ROUGHSHIPINTMAT, the interior tech panels) were half of
    /// Slot 8's 3.8 million LOD0 triangles. Effect and helper meshes the game draws only at times or never (the
    /// cockpit's re-entry tunnel and speed shell, the magnets' tractor-beam cones, exclusion volumes) are left out
    /// too - drawn solid they hid the ship. Detail level 3 and no triangle smaller than a preview pixel (0.002 m² -
    /// the preview is ~420 px across a ~50 m ship) keep it light enough to turn: Slot 8's 71 parts, 321,000 triangles.
    /// </summary>
    public ShipMesh? BuildOutside(CorvetteBuild build)
    {
        if (_models.BuildPlaced(build.Pieces, PreviewLod, PreviewHidden) is not { } mesh) return null;
        var keep = new List<int>();
        for (int t = 0; t * 3 + 2 < mesh.Corners.Length; t++)
        {
            int i = t * 3;
            if (Inside.IsMatch(Path.GetFileName(mesh.Materials[t]))) continue;
            float area = Vector3.Cross(mesh.Corners[i + 1] - mesh.Corners[i], mesh.Corners[i + 2] - mesh.Corners[i]).Length() * 0.5f;
            if (area >= SmallestTriangle) keep.Add(t);
        }
        if (keep.Count == 0) return null;
        Vector3[] corners = new Vector3[keep.Count * 3];
        string[] materials = new string[keep.Count];
        bool hasUv = mesh.Uvs.Length == mesh.Corners.Length, hasUv2 = mesh.Uvs2.Length == mesh.Corners.Length;
        Vector2[] uvs = hasUv ? new Vector2[corners.Length] : Array.Empty<Vector2>();
        Vector2[] uvs2 = hasUv2 ? new Vector2[corners.Length] : Array.Empty<Vector2>();
        for (int k = 0; k < keep.Count; k++)
        {
            int from = keep[k] * 3, to = k * 3;
            Array.Copy(mesh.Corners, from, corners, to, 3);
            if (hasUv) Array.Copy(mesh.Uvs, from, uvs, to, 3);
            if (hasUv2) Array.Copy(mesh.Uvs2, from, uvs2, to, 3);
            materials[k] = mesh.Materials[keep[k]];
        }
        return new ShipMesh(corners, materials) { Uvs = uvs, Uvs2 = uvs2 };
    }

    private const int PreviewLod = 3;
    private const float SmallestTriangle = 0.002f;
    private static readonly HashSet<string> PreviewHidden = new(StringComparer.Ordinal)
        { "ExclusionVolumes", "MESHBOUNDS", "ReEntryTunnel", "SpeedCool", "ConeMesh", "CANOPYFADE" };
    private static readonly Regex Inside = new("INT_?MAT|ROUGHSHIPINT|BIGGES_TECH|RODTECH", RegexOptions.IgnoreCase);

    /// <summary>A sub-part's model for the part's style (GcBaseBuildingEntry.Style): B_DECO_F is decoration style F,
    /// B_TUR_C gun C. Taking the first style for every part drew all of Slot 8's decorations as style A's 5.7 m
    /// antennas (thin rods out of the hull the game doesn't show, owner 2026-11-15). A part without that style uses
    /// the sub-part's first model.</summary>
    private string? SubPartScene(string subPartId, GcBaseBuildingPartStyle.StyleEnum style) =>
        _subPartScenes.TryGetValue(subPartId, out var models)
            ? models.FirstOrDefault(m => m.Style == style).Scene ?? models[0].Scene : null;

    /// <summary>A part's colour palette: its UserData (CVX) is the index into GcBaseBuildingTable.Palettes - all 70 of
    /// Slot 8's unpainted corvette parts have 99 = BIGGS0, the parts' DefaultColourPaletteId (2026-11-15; the owner
    /// never painted it). A value outside the table falls back to the part's default. A repainted corvette's UserData
    /// isn't checked yet.</summary>
    private int PaletteOf(string id, ulong userData) =>
        userData < (ulong)_palettes.Count ? (int)userData : _defaultPalettes.GetValueOrDefault(id, -1);

    /// <summary>The outside's textures, each material painted with its part's base colours (ShipModels.Painted
    /// colourise). Keys are BuildOutside's material names ("material#palette"). 256 px: a corvette has ~95 materials
    /// (100 MB at 512).</summary>
    public Dictionary<string, ModelTexture?> PaintOutside(ShipMesh mesh, int size = 256)
    {
        var result = new Dictionary<string, ModelTexture?>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in mesh.Materials.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int cut = key.LastIndexOf('#');
            string material = cut > 0 ? key[..cut] : key;
            float[][]? palette = cut > 0 && int.TryParse(key[(cut + 1)..], out int index) && index >= 0 && index < _palettes.Count
                ? _palettes[index] : null;
            result[key] = _models.Painted(material, (_, _) => null, size, baseColours: palette);
        }
        return result;
    }

    /// <summary>The save's up and at vectors as rows (right = up x at), then the position.</summary>
    private static Matrix4x4 PartMatrix(CorvettePart p)
    {
        var up = Vector3.Normalize(p.Up);
        var at = Vector3.Normalize(p.At);
        var right = Vector3.Normalize(Vector3.Cross(up, at));
        return new Matrix4x4(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, at.X, at.Y, at.Z, 0,
            p.Position.X, p.Position.Y, p.Position.Z, 1);
    }

    private void CollectSnaps(TkSceneNodeData node, Matrix4x4 parent, Placed part, int depth)
    {
        if (depth > 24) return;
        var local = SceneTransformMath.Local(node.Transform) * parent;
        string name = node.Name?.Value ?? "";
        if (name.StartsWith("SnapPoint", StringComparison.OrdinalIgnoreCase))
        {
            var snap = new Snap(name, Vector3.Transform(Vector3.Zero, local * part.World));
            foreach (var socket in node.Children ?? new())
            {
                string s = socket.Name?.Value ?? "";
                int o = s.IndexOf("_Out_", StringComparison.Ordinal), i = s.IndexOf("_In_", StringComparison.Ordinal);
                if (o > 0) snap.Out.Add(s[..o]);
                else if (i > 0) snap.In.Add(s[..i]);
            }
            part.Snaps.Add(snap);
        }
        if (node.Type?.Value == "REFERENCE" && Attribute(node, "SCENEGRAPH") is { Length: > 0 } sub && _models.Decode(sub) is TkSceneNodeData child)
            CollectSnaps(child, local, part, depth + 1);
        foreach (var c in node.Children ?? new()) CollectSnaps(c, local, part, depth + 1);
    }

    private void Place(string scene, Matrix4x4 world, int level, Placed part, List<Placed> all, List<CorvettePiece> pieces,
        ref Vector3 min, ref Vector3 max)
    {
        if (level > 6 || _models.Decode(scene) is not TkSceneNodeData root) return;
        // Placement scenes hold the build-mode ghost; the visible model is the rules' sub-parts.
        bool ghost = level == 0 && scene.EndsWith("_PLACEMENT.SCENE.MBIN", StringComparison.OrdinalIgnoreCase);
        if (!ghost) pieces.Add(new CorvettePiece(scene, world, part.Palette));
        var locators = new List<(string Name, Matrix4x4 World)>();
        var rules = new List<GcBasePlacementRule>();
        var lods = ghost ? null : LodLevels(root);
        var lo = min;
        var hi = max;
        void Walk(TkSceneNodeData node, Matrix4x4 parent, int depth)
        {
            // Under a JOINT the meshes are animated (the tech wall's robot arms); their scene place isn't where the
            // game has them - counted, the arms put the bottom 1 m too low (9.73 against the game's 9.25).
            if (depth > 32 || node.Type?.Value == "JOINT") return;
            var local = SceneTransformMath.Local(node.Transform) * parent;
            string name = node.Name?.Value ?? "";
            locators.Add((name, local));
            if (lods is not null && !NotMeasured.Contains(name) && Measured(node, name, lods) && Box(node) is { } box)
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) != 0 ? box.Max.X : box.Min.X, (i & 2) != 0 ? box.Max.Y : box.Min.Y,
                        (i & 4) != 0 ? box.Max.Z : box.Min.Z);
                    var w = Vector3.Transform(corner, local * world);
                    lo = Vector3.Min(lo, w);
                    hi = Vector3.Max(hi, w);
                }
            if (Attribute(node, "ATTACHMENT") is { Length: > 0 } entity && _models.Decode(entity) is TkAttachmentData attachment)
                foreach (var component in attachment.Components ?? new())
                    if (component.Template is GcBasePlacementComponentData placement) rules.AddRange(placement.Rules ?? new());
            if (node.Type?.Value == "REFERENCE" && Attribute(node, "SCENEGRAPH") is { Length: > 0 } sub && _models.Decode(sub) is TkSceneNodeData child)
                Walk(child, local, depth + 1);
            foreach (var c in node.Children ?? new()) Walk(c, local, depth + 1);
        }
        Walk(root, Matrix4x4.Identity, 0);
        min = lo;
        max = hi;
        foreach (var rule in rules)
        {
            var conditions = rule.Conditions ?? new();
            bool applies = conditions.Count == 0
                || (rule.ORConditions ? conditions.Any(c => Holds(c, part, all)) : conditions.All(c => Holds(c, part, all)));
            if (!applies) continue;
            string id = rule.PartID?.Value ?? "";
            string? subScene = SubPartScene(id, part.Style) ?? _placementScenes.GetValueOrDefault(id);
            if (subScene is null) continue;
            string locator = rule.PositionLocator?.Value ?? "";
            if (locator.Length == 0) Place(subScene, world, level + 1, part, all, pieces, ref min, ref max);
            else
                foreach (var l in locators.Where(l => l.Name.StartsWith(locator, StringComparison.OrdinalIgnoreCase)).ToList())
                    Place(subScene, l.World * world, level + 1, part, all, pieces, ref min, ref max);
        }
    }

    /// <summary>A mesh in a LOD group counts only at the measured level; one without levels always counts.</summary>
    private static bool Measured(TkSceneNodeData node, string name, Dictionary<string, int> lods)
    {
        if (Attribute(node, "LODLEVEL") is not { } level || !int.TryParse(level, out int l)) return true;
        return lods.GetValueOrDefault(LodSuffix.Replace(name, "")) == 0 || l == MeasuredLod;
    }

    /// <summary>The highest LOD level of each mesh group (name without "LODn") in a scene.</summary>
    private static Dictionary<string, int> LodLevels(TkSceneNodeData root)
    {
        var top = new Dictionary<string, int>(StringComparer.Ordinal);
        void Walk(TkSceneNodeData n)
        {
            if (Attribute(n, "LODLEVEL") is { } level && int.TryParse(level, out int l))
            {
                string stem = LodSuffix.Replace(n.Name?.Value ?? "", "");
                top[stem] = Math.Max(top.GetValueOrDefault(stem), l);
            }
            foreach (var c in n.Children ?? new()) Walk(c);
        }
        Walk(root);
        return top;
    }

    private static bool Holds(NMSTemplate condition, Placed part, List<Placed> all) => condition switch
    {
        GcSnapPointCondition s => Attached(part, all, s.SnapPoint.Value, null, s.ObjectId.Value)
            == (s.SnapState.SnapState == GcBaseSnapState.SnapStateEnum.IsSnapped),
        GcOutSnapSocketCondition o => Attached(part, all, o.SnapPoint.Value, SocketType(o.OutSocket.Value), o.ObjectId.Value)
            == (o.SnapState.SnapState == GcBaseSnapState.SnapStateEnum.IsSnapped),
        GcGroupCondition g => g.ORConditions ? g.Conditions.Any(c => Holds(c, part, all)) : g.Conditions.All(c => Holds(c, part, all)),
        _ => false,
    };

    private static string SocketType(string socket)
    {
        int cut = socket.IndexOf("_Out_", StringComparison.Ordinal);
        return cut > 0 ? socket[..cut] : socket;
    }

    /// <summary>Whether another part meets this part's snap point: one of its In sockets takes
    /// <paramref name="outSocket"/>, or (no socket named) any Out meets an In either way.</summary>
    private static bool Attached(Placed part, List<Placed> all, string snapName, string? outSocket, string objectId)
    {
        foreach (var mine in part.Snaps)
        {
            if (!mine.Name.Equals(snapName, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var other in all)
            {
                if (other == part || (objectId.Length > 0 && !other.Id.Equals(objectId, StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var theirs in other.Snaps)
                {
                    if (Vector3.Distance(theirs.World, mine.World) >= SnapDistance) continue;
                    if (outSocket is not null ? theirs.In.Contains(outSocket)
                        : mine.Out.Any(theirs.In.Contains) || mine.In.Any(theirs.Out.Contains)) return true;
                }
            }
        }
        return false;
    }

    private static (Vector3 Min, Vector3 Max)? Box(TkSceneNodeData node) =>
        Corner(node, "MIN") is { } min && Corner(node, "MAX") is { } max ? (min, max) : null;

    private static Vector3? Corner(TkSceneNodeData node, string which) =>
        float.TryParse(Attribute(node, $"AABB{which}X"), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
        && float.TryParse(Attribute(node, $"AABB{which}Y"), NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
        && float.TryParse(Attribute(node, $"AABB{which}Z"), NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
            ? new Vector3(x, y, z) : null;

    private static string? Attribute(TkSceneNodeData node, string name) =>
        node.Attributes?.FirstOrDefault(a => a.Name?.Value == name)?.Value?.Value;

    private sealed record Snap(string Name, Vector3 World)
    {
        public List<string> Out { get; } = new();
        public List<string> In { get; } = new();
    }

    private sealed record Placed(string Id, Matrix4x4 World, string Scene, int Palette, GcBaseBuildingPartStyle.StyleEnum Style)
    {
        public List<Snap> Snaps { get; } = new();
    }
}
