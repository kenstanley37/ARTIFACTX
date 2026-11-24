using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS;
public static class Paks
{
    static Dictionary<string, (string pak, ArtifactX.GameData.Models.PakEntry e, ArtifactX.GameData.Models.PakHeader h)> index;
    static readonly ExtractionService ex = new();
    static readonly Dictionary<string, NMSTemplate> cache = new(StringComparer.OrdinalIgnoreCase);
    public static string Norm(string p) => p.Replace((char)92,'/').Replace(".MXML",".MBIN",StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
    public static void Load()
    {
        // SettingsService reads config.json from the working directory: run from the DataCataloger folder or copy its config.json here (git-ignored by *.json).
        var s = SettingsService.Load();
        var r = new PakReaderService();
        index = new(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in new PakDiscoveryService().Discover(s.NmsInstallationPath!))
        {
            try { var h = r.ReadHeader(pak.FullPath); foreach (var e in r.Read(pak.FullPath)) if (e.FileName is { } f) index[Norm(f)] = (pak.FullPath, e, h); } catch { }
        }
        Console.Error.WriteLine($"{index.Count} pak entries");
    }
    public static IEnumerable<string> Names => index.Keys;
    public static byte[] Bytes(string path) => index.TryGetValue(Norm(path), out var x) ? ex.ExtractEntryBytes(x.pak, x.e, x.h) : null;
    public static NMSTemplate Decode(string path)
    {
        path = Norm(path);
        if (cache.TryGetValue(path, out var t)) return t;
        var b = Bytes(path); if (b is null) return cache[path] = null;
        try { using var ms = new MemoryStream(b); using var f = new MBINFile(ms); f.Load(); t = f.GetData(); } catch (Exception e) { Console.Error.WriteLine($"decode {path}: {e.Message}"); t = null; }
        return cache[path] = t;
    }
}
