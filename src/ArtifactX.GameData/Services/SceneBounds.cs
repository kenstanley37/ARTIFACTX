using System.Numerics;
using libMBIN;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData.Services;

/// <summary>
/// A scene's size from its own files, the way the game sizes a placed building
/// (GcBuildingSpawnData.Radius, BuildingSizeCalculated): the box around every
/// mesh's AABB (AABBMIN/AABBMAX attributes), moved by each node's transform and
/// by REFERENCE nodes' sub-scenes, and half its ground (X/Z) diagonal.
///
/// Checked 2026-11-04 (build 184271) against the radius the running game
/// measured for the Portal on 2026-10-28: PORTAL.SCENE.MBIN gives 37.76815
/// (box -31.66..31.66 x, -20.60..20.60 z), the game 37.768147 - float rounding.
/// Its pillars and side pieces are REFERENCE sub-scenes rotated 90/180 degrees,
/// so the transform order (scale, then X, Y, Z rotation in degrees, then
/// translation) is exercised. L-system buildings (the Shelter's SHACK_*.LSYSTEM)
/// are assembled at run time and can't be sized from one scene.
/// </summary>
public static class SceneBounds
{
    /// <summary>Half the ground diagonal of the scene's mesh bounds; null when the
    /// scene is missing or has no mesh bounds.</summary>
    public static float? GroundRadius(string scenePath, Func<string, NMSTemplate?> decode)
    {
        if (decode(scenePath.ToUpperInvariant()) is not TkSceneNodeData root) return null;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        bool any = false;
        void Walk(TkSceneNodeData node, Matrix4x4 parent, int depth)
        {
            if (depth > 32) return;
            var world = Local(node.Transform) * parent;
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in node.Attributes ?? []) attributes.TryAdd(a.Name.Value, a.Value.Value);
            if (Corner(attributes, "MIN") is { } lo && Corner(attributes, "MAX") is { } hi)
            {
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) != 0 ? hi.X : lo.X, (i & 2) != 0 ? hi.Y : lo.Y, (i & 4) != 0 ? hi.Z : lo.Z);
                    var w = Vector3.Transform(c, world);
                    min = Vector3.Min(min, w);
                    max = Vector3.Max(max, w);
                }
                any = true;
            }
            if (node.Type.Value == "REFERENCE" && attributes.TryGetValue("SCENEGRAPH", out var sub)
                && decode(sub.Replace('\\', '/').ToUpperInvariant()) is TkSceneNodeData child)
                Walk(child, world, depth + 1);
            foreach (var c in node.Children ?? []) Walk(c, world, depth + 1);
        }
        Walk(root, Matrix4x4.Identity, 0);
        if (!any) return null;
        return MathF.Sqrt(MathF.Pow(max.X - min.X, 2) + MathF.Pow(max.Z - min.Z, 2)) / 2;
    }

    private static Matrix4x4 Local(TkTransformData t)
    {
        const float degrees = MathF.PI / 180;
        var rotation = Matrix4x4.CreateRotationX(t.RotX * degrees) * Matrix4x4.CreateRotationY(t.RotY * degrees) * Matrix4x4.CreateRotationZ(t.RotZ * degrees);
        return Matrix4x4.CreateScale(t.ScaleX, t.ScaleY, t.ScaleZ) * rotation * Matrix4x4.CreateTranslation(t.TransX, t.TransY, t.TransZ);
    }

    private static Vector3? Corner(Dictionary<string, string> attributes, string which) =>
        attributes.TryGetValue($"AABB{which}X", out var x) && attributes.TryGetValue($"AABB{which}Y", out var y) && attributes.TryGetValue($"AABB{which}Z", out var z)
        && float.TryParse(x, System.Globalization.CultureInfo.InvariantCulture, out var fx)
        && float.TryParse(y, System.Globalization.CultureInfo.InvariantCulture, out var fy)
        && float.TryParse(z, System.Globalization.CultureInfo.InvariantCulture, out var fz)
            ? new Vector3(fx, fy, fz) : null;
}
