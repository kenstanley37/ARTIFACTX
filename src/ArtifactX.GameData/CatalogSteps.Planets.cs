using System.Text.Json;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// What a planet's type is picked from (2026-10-22, ArtifactX.Core PlanetGenerator):
    /// biomelistperstartype.mbin (odds per star colour, prime odds, the lush/harsh-galaxy
    /// yellow lists, dead-to-weird chance) and biomefilenames.mbin (the types a start,
    /// giant or purple-star moon may be, and each type's file options with subtypes).
    /// Category "PlanetGeneration", one row "BIOMETABLES": DescriptionEnglish = JSON in
    /// the shape of Core's BiomeTables (Normal/Prime lists in the order Yellow, Green,
    /// Blue, Red, Purple, LushYellow, AbandonedYellow).
    /// </summary>
    public static void AddPlanetGeneration(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath)) { ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}"); return; }
        const string listFile = "METADATA/SIMULATION/SOLARSYSTEM/BIOMES/BIOMELISTPERSTARTYPE.MBIN";
        const string filesFile = "METADATA/SIMULATION/SOLARSYSTEM/BIOMES/BIOMEFILENAMES.MBIN";
        var reader = new PakReaderService();
        var extraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcBiomeListPerStarType? biomes = null;
        libMBIN.NMS.GameComponents.GcBiomeFileList? files = null;
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (biomes is not null && files is not null) break;
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try { header = reader.ReadHeader(pak.FullPath); entries = reader.Read(pak.FullPath); }
            catch { continue; }
            foreach (var entry in entries.Where(e => string.Equals(e.FileName, listFile, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(e.FileName, filesFile, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak.FullPath, entry, header)!));
                    mbin.Load();
                    switch (mbin.GetData())
                    {
                        case libMBIN.NMS.GameComponents.GcBiomeListPerStarType b: biomes = b; break;
                        case libMBIN.NMS.GameComponents.GcBiomeFileList f: files = f; break;
                    }
                }
                catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read {entry.FileName}: {ex.Message}"); }
            }
        }
        var rows = new List<CatalogItem>();
        if (biomes is null || files is null)
            ConsoleStyle.Error("Planet type tables not found - planet types left out.");
        else
        {
            var lists = biomes.StarType.Concat(new[] { biomes.LushYellow, biomes.AbandonedYellow }).ToArray();
            int[] Types(List<libMBIN.NMS.GameComponents.GcBiomeType>? list) => (list ?? new()).Select(b => (int)b.Biome).ToArray();
            var tables = new
            {
                Normal = lists.Select(l => l.BiomeProbability.ToArray()).ToArray(),
                Prime = lists.Select(l => l.PrimeBiomeProbability.ToArray()).ToArray(),
                biomes.ConvertDeadToWeird,
                ValidGiantPlanetBiome = Types(files.ValidGiantPlanetBiome),
                ValidPurpleMoonBiome = Types(files.ValidPurpleMoonBiome),
                ValidStartPlanetBiome = Types(files.ValidStartPlanetBiome),
                Files = files.BiomeFiles.Select(o => (o.FileOptions ?? new())
                    .Select(x => new { x.PurpleSystemWeight, SubType = (int)x.SubType.BiomeSubType, x.Weight }).ToArray()).ToArray(),
            };
            rows.Add(new CatalogItem { GameId = "BIOMETABLES", NameEnglish = "BiomeTables", DescriptionEnglish = JsonSerializer.Serialize(tables) });
        }
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "PlanetGeneration", "GcBiomeListPerStarType/GcBiomeFileList", listFile.ToLowerInvariant(), rows);
        ConsoleStyle.Success($"Wrote the planet type tables to {catalogPath}.");
    }
}
