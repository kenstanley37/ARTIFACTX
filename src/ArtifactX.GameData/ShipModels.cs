using System.Collections.Concurrent;
using System.Numerics;
using System.Text.RegularExpressions;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

/// <summary>A ship's triangles in the game's axes (Y up), three corners each,
/// with the material each triangle is painted with.</summary>
public sealed record ShipMesh(Vector3[] Corners, string[] Materials);

/// <summary>
/// Builds a procedural ship's 3D model from the player's own game files
/// (2026-10-15): the ship's scene, without the part options its seed didn't
/// pick, each mesh's geometry (positions + indices from .GEOMETRY.MBIN.PC /
/// .GEOMETRY.DATA.MBIN.PC) placed with its accumulated transform. Read from the
/// PAKs on demand and cached for the session - nothing is stored or shipped.
///
/// Follows NMS Shipwright's model3d.py (MIT, see THIRD-PARTY-NOTICES.md):
/// only LOD0 meshes, effect shells (shields, warp bubble) and decal/shadow
/// materials left out, lights/joints/collision ignored, a long part name may be
/// cut short in the scene so names are matched by prefix. Also used for
/// multi-tools, whose scenes carry extra "Shape" pieces per option (see Keep).
/// </summary>
public sealed class ShipModels
{
    private readonly GlobalFileIndexService _index = new();
    private readonly ExtractionService _extraction = new();
    private readonly ConcurrentDictionary<string, NMSTemplate?> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Dictionary<ulong, (Vector3[] P, uint[] I)>> _geometry = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex LodName = new(@"LOD(\d)$", RegexOptions.IgnoreCase);
    private static readonly Regex Lod = new(@"LOD\d$");
    private static readonly Regex Effects = new("SHIELD|WARPBUBBLE|FORCEFIELD");
    // MULTITOOLDECAL isn't a flat decal: multi-tools use it for solid panels (the middle
    // of a long barrel's top cover went missing without it, 2026-10-16).
    private static readonly Regex Hidden = new("(?<!MULTITOOL)DECAL|SHADOW|MASKMAT|DEPTHMASK");

    public ShipModels(GameInstall game)
    {
        var reader = new PakReaderService();
        foreach (var pak in new PakDiscoveryService().Discover(game.PcbanksPath))
        {
            try { _index.Add(pak.FullPath, reader.ReadHeader(pak.FullPath), reader.Read(pak.FullPath)); }
            catch { /* unreadable PAK - skip */ }
        }
    }

    private NMSTemplate? Decode(string path) => _files.GetOrAdd(path.Replace('\\', '/').ToUpperInvariant(), p =>
    {
        if (!_index.TryFind(p, out var pak, out var entry, out var header)) return null;
        try
        {
            var bytes = _extraction.ExtractEntryBytes(pak, entry, header);
            if (bytes is null) return null;
            var mbin = new MBINFile(new MemoryStream(bytes));
            mbin.Load();
            return mbin.GetData();
        }
        catch { return null; }
    });

    /// <summary>The model for a ship type's scene. <paramref name="optionIds"/>
    /// are every part id the type can have, <paramref name="chosen"/> the ones
    /// the seed picked (both compared exactly, as ShipLook does).</summary>
    public ShipMesh? Build(string scenePath, ISet<string> optionIds, ISet<string> chosen)
    {
        if (Decode(scenePath) is not TkSceneNodeData root) return null;
        var trunc = optionIds.Where(o => o.Length >= 15).OrderByDescending(o => o.Length).ToList();
        string Canon(string up)
        {
            if (optionIds.Contains(up)) return up;
            foreach (var o in trunc) if (up.StartsWith(o, StringComparison.Ordinal)) return o;
            return up;
        }
        // A piece that isn't an option itself but is named into a group ("_B4Cap_Shape2"
        // in group "_B4CAP_") goes with the option it starts with, and is hidden when
        // that isn't the chosen one. Found 2026-10-16 on Reflex Beam: a stray
        // "_B4Cap_Shape2" box covered Barrel 4's round front; the game's own part pick
        // (run in an emulator) matched ours, so it was the drawing. No ship has such pieces.
        var groups = optionIds.Select(o => o[..(o.LastIndexOf('_') + 1)]).Where(p => p.Length > 1)
            .Distinct().OrderByDescending(p => p.Length).ToList();
        bool Keep(string name)
        {
            string up = name.ToUpperInvariant();
            string id = Canon(up);
            if (optionIds.Contains(id)) return chosen.Contains(Lod.Replace(id, "")) || chosen.Contains(id);
            foreach (var g in groups)
                if (up.StartsWith(g, StringComparison.Ordinal))
                    return chosen.Any(c => c.StartsWith(g, StringComparison.Ordinal) && up.StartsWith(c, StringComparison.Ordinal));
            return true;
        }

        var corners = new List<Vector3>();
        var materials = new List<string>();
        void Walk(TkSceneNodeData node, string geometry, Matrix4x4 world, int depth)
        {
            string name = node.Name?.Value ?? "", type = node.Type?.Value ?? "";
            if (!Keep(name) || depth > 12) return;
            world = Local(node.Transform) * world;
            string? Attr(string key) => node.Attributes?.FirstOrDefault(a => a.Name?.Value == key)?.Value?.Value;
            if (type == "MESH")
            {
                var lod = LodName.Match(name);
                if ((Attr("LODLEVEL") ?? "0") != "0" || (lod.Success && lod.Groups[1].Value != "0")) return;
                string material = (Attr("MATERIAL") ?? "").Replace('\\', '/');
                if (ulong.TryParse(Attr("HASH"), out ulong hash) && !Effects.IsMatch(geometry.ToUpperInvariant())
                    && !Hidden.IsMatch(Path.GetFileName(material).ToUpperInvariant())
                    && Geometry(geometry).TryGetValue(hash, out var mesh))
                {
                    bool mirrored = world.GetDeterminant() < 0;
                    for (int i = 0; i + 2 < mesh.I.Length; i += 3)
                    {
                        var a = Vector3.Transform(mesh.P[mesh.I[i]], world);
                        var b = Vector3.Transform(mesh.P[mesh.I[i + 1]], world);
                        var c = Vector3.Transform(mesh.P[mesh.I[i + 2]], world);
                        corners.Add(a);
                        corners.Add(mirrored ? c : b);
                        corners.Add(mirrored ? b : c);
                        materials.Add(material);
                    }
                }
            }
            else if (type == "REFERENCE" && Attr("SCENEGRAPH") is { Length: > 0 } sg && Decode(sg) is TkSceneNodeData sub)
            {
                Walk(sub, GeometryOf(sub), world, depth + 1);
            }
            else if (type is "COLLISION" or "LIGHT" or "JOINT" or "EMITTER") return;
            foreach (var child in node.Children ?? new()) Walk(child, geometry, world, depth);
        }
        Walk(root, GeometryOf(root), Matrix4x4.Identity, 0);
        return corners.Count == 0 ? null : new ShipMesh(corners.ToArray(), materials.ToArray());
    }

    private static string GeometryOf(TkSceneNodeData node) =>
        (node.Attributes?.FirstOrDefault(a => a.Name?.Value == "GEOMETRY")?.Value?.Value ?? "").Replace('\\', '/');

    /// <summary>The game's transform: scale, then rotate X, Y, Z (degrees), then
    /// translate (System.Numerics row vectors, so applied left to right).</summary>
    private static Matrix4x4 Local(TkTransformData t)
    {
        const float Rad = MathF.PI / 180;
        var m = Matrix4x4.CreateScale(t.ScaleX, t.ScaleY, t.ScaleZ)
              * Matrix4x4.CreateRotationX(t.RotX * Rad) * Matrix4x4.CreateRotationY(t.RotY * Rad) * Matrix4x4.CreateRotationZ(t.RotZ * Rad);
        m.Translation = new Vector3(t.TransX, t.TransY, t.TransZ);
        return m;
    }

    /// <summary>Every mesh of one geometry file: half-float positions and indices
    /// by mesh hash. The file's "16 bit indices" flag isn't always right (a
    /// sentinel ship says 32 and stores 16), so the width that fits wins.</summary>
    private Dictionary<ulong, (Vector3[] P, uint[] I)> Geometry(string geometry) => _geometry.GetOrAdd(geometry, g =>
    {
        var result = new Dictionary<ulong, (Vector3[], uint[])>();
        if (Decode(g + ".PC") is not TkGeometryData gd
            || Decode(Regex.Replace(g, @"\.GEOMETRY\.MBIN$", ".GEOMETRY.DATA.MBIN", RegexOptions.IgnoreCase) + ".PC") is not TkGeometryStreamData sd)
            return result;
        int stride = gd.PositionVertexLayout.Stride;
        int at = gd.PositionVertexLayout.VertexElements?.FirstOrDefault(v => v.SemanticID == 0)?.Offset ?? 0;
        bool wide = gd.Indices16Bit == 0;
        foreach (var md in sd.StreamDataArray ?? new())
        {
            var ps = md.MeshPositionDataStream ?? Array.Empty<byte>();
            if (stride <= 0) break;
            int count = ps.Length / stride;
            var pos = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * stride + at;
                pos[i] = new Vector3((float)BitConverter.ToHalf(ps, o), (float)BitConverter.ToHalf(ps, o + 2), (float)BitConverter.ToHalf(ps, o + 4));
            }
            var ms = md.MeshDataStream ?? Array.Empty<byte>();
            int start = md.VertexDataSize, size = md.IndexDataSize;
            foreach (int width in wide ? new[] { 4, 2 } : new[] { 2, 4 })
            {
                if (size % width != 0 || (size / width) % 3 != 0 || start + size > ms.Length) continue;
                var idx = new uint[size / width];
                uint max = 0;
                for (int i = 0; i < idx.Length; i++)
                {
                    idx[i] = width == 2 ? BitConverter.ToUInt16(ms, start + i * 2) : BitConverter.ToUInt32(ms, start + i * 4);
                    if (idx[i] > max) max = idx[i];
                }
                if (idx.Length > 0 && max < count) { result[md.Hash] = (pos, idx); break; }
            }
        }
        return result;
    });
}
