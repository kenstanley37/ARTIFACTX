using System.Numerics;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;
public static class Scan
{
    static Matrix4x4 Local(TkTransformData t)
    {
        const float r = MathF.PI / 180f;
        var m = Matrix4x4.CreateScale(t.ScaleX, t.ScaleY, t.ScaleZ) * Matrix4x4.CreateRotationX(t.RotX * r) * Matrix4x4.CreateRotationY(t.RotY * r) * Matrix4x4.CreateRotationZ(t.RotZ * r);
        m.Translation = new Vector3(t.TransX, t.TransY, t.TransZ); return m;
    }
    static string Attr(TkSceneNodeData n, string k) => n.Attributes?.FirstOrDefault(a => string.Equals(a.Name?.Value, k, StringComparison.OrdinalIgnoreCase))?.Value?.Value ?? "";
    public static void Run(string root)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Scene(string path, Matrix4x4 parent, string chain, int depth)
        {
            path = Paks.Norm(path);
            if (depth > 20 || !active.Add(path) || Paks.Decode(path) is not TkSceneNodeData n) { active.Remove(path); return; }
            Node(n, path, "", parent, chain, depth); active.Remove(path);
        }
        void Node(TkSceneNodeData n, string scene, string parentPath, Matrix4x4 parent, string chain, int depth)
        {
            string name = n.Name?.Value ?? ""; string np = parentPath.Length == 0 ? name : parentPath + "/" + name;
            var m = Local(n.Transform) * parent;
            string att = Attr(n, "ATTACHMENT");
            if (att.Length > 0 && Paks.Decode(att) is TkAttachmentData a)
                foreach (var c in (a.Components ?? new()).Select(c => c?.Template).OfType<GcInteractionComponentData>())
                {
                    var p = Vector3.Transform(Vector3.Zero, m);
                    Console.WriteLine($"{c.InteractionType?.InteractionType}\t{p.X:R}\t{p.Y:R}\t{p.Z:R}\t{chain} :: {scene} :: {np}\t{Paks.Norm(att)}");
                }
            string sg = Attr(n, "SCENEGRAPH");
            if (sg.Length > 0) Scene(sg, m, chain + ">" + name, depth + 1);
            foreach (var ch in n.Children ?? new()) Node(ch, scene, np, m, chain, depth);
        }
        Scene(root, Matrix4x4.Identity, "", 0);
    }
}
