// Converts the game's scene and L-system MBINs (and, for geometry, its bounding-hull and mesh AABB lists) to JSON with
// full game paths, for lsysport.py (docs/research/PLANETARY_MULTITOOL_LOCATIONS.md). Extracted game data: keep the
// output local, never commit it. Run from the repository root (DataCataloger's config.json holds the install path).
// usage: dotnet run --project scripts/research/region-generator/LsysDump -- <out folder> <path substring>...
//        e.g. ... -- %TEMP%\lsys MODELS/PLANETS/BIOMES/COMMON/BUILDINGS/
using System.Collections;
using System.Reflection;
using System.Text.Json;
using libMBIN;
using ArtifactX.GameData.Services;
object Conv(object o)
{
    if (o is null) return null;
    var t = o.GetType();
    if (t.IsPrimitive || o is string) return o;
    if (t.IsEnum) return o.ToString();
    if (o is libMBIN.NMS.INMSString or libMBIN.NMS.INMSVariableLengthString) return o.ToString();
    if (o is IList l) { var r = new List<object>(); foreach (var x in l) r.Add(Conv(x)); return r; }
    var d = new Dictionary<string, object> { ["$type"] = t.Name };
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) d[f.Name] = Conv(f.GetValue(o));
    return d;
}
var settings = SettingsService.Load();
var reader = new PakReaderService(); var ex = new ExtractionService();
var wanted = args.Skip(1).Select(s => s.Replace('\\', '/').ToUpperInvariant()).ToArray();
int n = 0;
foreach (var pak in new PakDiscoveryService().Discover(settings.NmsInstallationPath))
{
    ArtifactX.GameData.Models.PakHeader header; IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
    try { header = reader.ReadHeader(pak.FullPath); entries = reader.Read(pak.FullPath); } catch { continue; }
    foreach (var e in entries)
    {
        string name = e.FileName?.Replace('\\', '/').ToUpperInvariant();
        bool geom = name?.EndsWith(".GEOMETRY.MBIN.PC") == true;
        if (name is null || !(geom || name.EndsWith(".SCENE.MBIN") || name.EndsWith(".LSYSTEM.MBIN"))) continue;
        if (!wanted.Any(w => name.Contains(w))) continue;
        try
        {
            var bytes = ex.ExtractEntryBytes(pak.FullPath, e, header); if (bytes is null) continue;
            var m = new MBINFile(new MemoryStream(bytes)); m.Load();
            object data = m.GetData();
            if (geom)
            {
                // hull data only: BoundHullVerts (x, y, z) and the per-mesh AABBs
                var g = (libMBIN.NMS.Toolkit.TkGeometryData)data;
                data = new Dictionary<string, object> { ["Hull"] = g.BoundHullVerts.Select(v => new[] { v.X, v.Y, v.Z }).ToList(),
                    ["St"] = g.BoundHullVertSt, ["Ed"] = g.BoundHullVertEd,
                    ["Min"] = g.MeshAABBMin.Select(v => new[] { v.X, v.Y, v.Z }).ToList(), ["Max"] = g.MeshAABBMax.Select(v => new[] { v.X, v.Y, v.Z }).ToList() };
                name = name[..^3];
            }
            else data = Conv(data);
            string target = Path.Combine(args[0], name + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, JsonSerializer.Serialize(data)); n++;
        }
        catch (Exception x) { Console.WriteLine($"{name}: {x.Message}"); }
    }
}
Console.WriteLine($"{n} files");
