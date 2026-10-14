using System.Text.Json;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// The part trees starships are built from (2026-10-14, experiment/ship-parts):
    /// every *.descriptor.mbin under MODELS/COMMON/SPACECRAFT/ (TkModelDescriptorList -
    /// groups of options, an option can hold nested groups and reference other
    /// scenes that have descriptors of their own). With the ship's seed these say
    /// which parts the game picks (ArtifactX.Core ShipParts).
    ///
    /// Category "ShipDescriptors": GameId "DESC:" + lower-case descriptor path,
    /// NameEnglish = the path, DescriptionEnglish = the tree as JSON
    /// [{"t":type,"o":[{"id","n":name,"s":[scene paths],"c":[[groups]...]}]}].
    /// </summary>
    public static void AddShipDescriptors(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}");
            return;
        }

        static List<object> Groups(TkModelDescriptorList? list) =>
            list?.List?.Select(g => (object)new Dictionary<string, object>
            {
                ["t"] = g.TypeId?.Value ?? "",
                ["o"] = g.Descriptors?.Select(d => (object)new Dictionary<string, object>
                {
                    ["id"] = d.Id?.Value ?? "",
                    ["n"] = d.Name?.Value ?? "",
                    ["s"] = d.ReferencePaths?.Select(r => r?.Value ?? "").Where(r => r.Length > 0).ToList() ?? new List<string>(),
                    ["c"] = d.Children?.OfType<TkModelDescriptorList>().Select(c => (object)Groups(c)).ToList() ?? new List<object>(),
                }).ToList() ?? new List<object>(),
            }).ToList() ?? new List<object>();

        var reader = new PakReaderService();
        var extraction = new ExtractionService();
        var rows = new List<CatalogItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            Models.PakHeader header;
            IReadOnlyList<Models.PakEntry> entries;
            try { header = reader.ReadHeader(pak.FullPath); entries = reader.Read(pak.FullPath); }
            catch { continue; }
            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && e.FileName.StartsWith("MODELS/COMMON/SPACECRAFT/", StringComparison.OrdinalIgnoreCase)
                         && e.FileName.EndsWith(".DESCRIPTOR.MBIN", StringComparison.OrdinalIgnoreCase)))
            {
                if (!seen.Add(entry.FileName)) continue;
                try
                {
                    var bytes = extraction.ExtractEntryBytes(pak.FullPath, entry, header);
                    if (bytes is null || bytes.Length == 0) continue;
                    var mbin = new MBINFile(new MemoryStream(bytes));
                    mbin.Load();
                    if (mbin.GetData() is not TkModelDescriptorList list) continue;
                    string path = entry.FileName.ToLowerInvariant();
                    rows.Add(new CatalogItem
                    {
                        GameId = "DESC:" + path,
                        NameEnglish = path,
                        UsageCategory = "Spacecraft",
                        DescriptionEnglish = JsonSerializer.Serialize(Groups(list)),
                    });
                }
                catch (Exception ex) { ConsoleStyle.Warning($"{entry.FileName}: {ex.Message}"); }
            }
        }
        ConsoleStyle.Header($"Found {rows.Count} starship part trees.");

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ShipDescriptors", "TkModelDescriptorList", "models/common/spacecraft/*.descriptor.mbin", rows);
        ConsoleStyle.Success($"Wrote {rows.Count} starship part trees to {catalogPath}.");
    }
}
