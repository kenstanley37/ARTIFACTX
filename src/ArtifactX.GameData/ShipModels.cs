using System.Collections.Concurrent;
using System.Numerics;
using System.Text.RegularExpressions;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

/// <summary>A ship's triangles in the game's axes (Y up), three corners each,
/// with the material each triangle is painted with.</summary>
public sealed record ShipMesh(Vector3[] Corners, string[] Materials)
{
    /// <summary>Texture coordinates, one per corner (2026-10-16, for painted previews).</summary>
    public Vector2[] Uvs { get; init; } = Array.Empty<Vector2>();
    /// <summary>The second texture coordinates (decal sheets), one per corner.</summary>
    public Vector2[] Uvs2 { get; init; } = Array.Empty<Vector2>();
}

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
public sealed partial class ShipModels
{
    private readonly GlobalFileIndexService _index = new();
    private readonly ExtractionService _extraction = new();
    private readonly ConcurrentDictionary<string, NMSTemplate?> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Dictionary<ulong, (Vector3[] P, Vector4[] T, uint[] I)>> _geometry = new(StringComparer.OrdinalIgnoreCase);

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
        var uvs = new List<Vector2>();
        var uvs2 = new List<Vector2>();
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
                        Vector4 ua = mesh.T[mesh.I[i]], ub = mesh.T[mesh.I[i + 1]], uc = mesh.T[mesh.I[i + 2]];
                        uvs.Add(new(ua.X, ua.Y));
                        uvs.Add(mirrored ? new(uc.X, uc.Y) : new(ub.X, ub.Y));
                        uvs.Add(mirrored ? new(ub.X, ub.Y) : new(uc.X, uc.Y));
                        uvs2.Add(new(ua.Z, ua.W));
                        uvs2.Add(mirrored ? new(uc.Z, uc.W) : new(ub.Z, ub.W));
                        uvs2.Add(mirrored ? new(ub.Z, ub.W) : new(uc.Z, uc.W));
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
        return corners.Count == 0 ? null : new ShipMesh(corners.ToArray(), materials.ToArray()) { Uvs = uvs.ToArray(), Uvs2 = uvs2.ToArray() };
    }

    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _weaponTags = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex WeaponTag = new("WEAPON_[A-Z_]+");

    /// <summary>The weapon tags a multi-tool scene's parts carry, by part name
    /// (upper case): a part's ATTACHMENT entity holds a tag like "WEAPON_LARGE".
    /// The game sets a tool's class from them (NMS.exe 0x1404dd980, 2026-10-16):
    /// WEAPON_LARGE/MEDIUM make a rifle, nothing makes a pistol; ALIEN, PRISTINE,
    /// ROYAL, ROBOT, ATLAS, STAFF their own classes. Barrels carry them on the main model.</summary>
    public IReadOnlyDictionary<string, string> WeaponTags(string scenePath) => _weaponTags.GetOrAdd(scenePath, s =>
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Walk(TkSceneNodeData node, int depth)
        {
            if (depth > 12) return;
            string? attachment = node.Attributes?.FirstOrDefault(a => a.Name?.Value == "ATTACHMENT")?.Value?.Value;
            if (!string.IsNullOrEmpty(attachment) && EntityTag(attachment) is { } tag)
                tags[(node.Name?.Value ?? "").ToUpperInvariant()] = tag;
            foreach (var child in node.Children ?? new()) Walk(child, depth + 1);
        }
        if (Decode(s) is TkSceneNodeData root) Walk(root, 0);
        return tags;
    });

    private string? EntityTag(string entityPath)
    {
        string p = entityPath.Replace('\\', '/').ToUpperInvariant();
        if (!_index.TryFind(p, out var pak, out var entry, out var header)) return null;
        try
        {
            var bytes = _extraction.ExtractEntryBytes(pak, entry, header);
            return bytes is null ? null : WeaponTag.Match(System.Text.Encoding.ASCII.GetString(bytes)) is { Success: true } m ? m.Value : null;
        }
        catch { return null; }
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
    private Dictionary<ulong, (Vector3[] P, Vector4[] T, uint[] I)> Geometry(string geometry) => _geometry.GetOrAdd(geometry, g =>
    {
        var result = new Dictionary<ulong, (Vector3[], Vector4[], uint[])>();
        if (Decode(g + ".PC") is not TkGeometryData gd
            || Decode(Regex.Replace(g, @"\.GEOMETRY\.MBIN$", ".GEOMETRY.DATA.MBIN", RegexOptions.IgnoreCase) + ".PC") is not TkGeometryStreamData sd)
            return result;
        int stride = gd.PositionVertexLayout.Stride;
        int at = gd.PositionVertexLayout.VertexElements?.FirstOrDefault(v => v.SemanticID == 0)?.Offset ?? 0;
        // Texture coordinates share the position stream (semantic 1, half floats).
        int uvAt = gd.PositionVertexLayout.VertexElements?.FirstOrDefault(v => v.SemanticID == 1)?.Offset ?? -1;
        bool wide = gd.Indices16Bit == 0;
        foreach (var md in sd.StreamDataArray ?? new())
        {
            var ps = md.MeshPositionDataStream ?? Array.Empty<byte>();
            if (stride <= 0) break;
            int count = ps.Length / stride;
            var pos = new Vector3[count];
            var tex = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * stride + at;
                pos[i] = new Vector3((float)BitConverter.ToHalf(ps, o), (float)BitConverter.ToHalf(ps, o + 2), (float)BitConverter.ToHalf(ps, o + 4));
                // Two sets: (x, y) the main texture, (z, w) decal sheets.
                if (uvAt >= 0 && i * stride + uvAt + 8 <= ps.Length)
                    tex[i] = new Vector4((float)BitConverter.ToHalf(ps, i * stride + uvAt), (float)BitConverter.ToHalf(ps, i * stride + uvAt + 2),
                        (float)BitConverter.ToHalf(ps, i * stride + uvAt + 4), (float)BitConverter.ToHalf(ps, i * stride + uvAt + 6));
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
                if (idx.Length > 0 && max < count) { result[md.Hash] = (pos, tex, idx); break; }
            }
        }
        return result;
    });
}
