using System.Text.Json;
using ArtifactX.Core.Universe;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string BiomeListFile = "METADATA/SIMULATION/SOLARSYSTEM/BIOMES/BIOMELISTPERSTARTYPE.MBIN";
    private const string BiomeFilesFile = "METADATA/SIMULATION/SOLARSYSTEM/BIOMES/BIOMEFILENAMES.MBIN";
    private const string BiomeArchiveFile = "METADATA/SIMULATION/SOLARSYSTEM/BIOMES/BIOMEFILENAMESARCHIVE.MBIN";
    private const string WeatherListFile = "METADATA/SIMULATION/SOLARSYSTEM/WEATHER/WEATHERLIST.MBIN";
    private const string CreatureDataFile = "METADATA/SIMULATION/ECOSYSTEM/CREATUREGENERATIONDATA.MBIN";

    /// <summary>
    /// What planets are generated from (2026-10-22, ArtifactX.Core PlanetGenerator and
    /// PlanetProfileGenerator). Category "PlanetGeneration", two rows:
    /// - "BIOMETABLES": JSON in the shape of Core's BiomeTables - biomelistperstartype.mbin
    ///   (odds per star colour, prime odds, the lush/harsh-galaxy yellow lists, dead-to-weird
    ///   chance) and biomefilenames.mbin + biomefilenamesarchive.mbin (the types a start,
    ///   giant or purple-star moon may be, each type's file options with subtypes).
    /// - "PLANETPROFILE": JSON in the shape of Core's PlanetProfileTables - mined resources
    ///   (gcterrainglobals), extreme weather odds (gcsolargenerationglobals), each biome
    ///   file's weather weights, each weather type's storm odds (weatherlist.mbin), life
    ///   odds and sentinel odds (gcgameplayglobals).
    /// </summary>
    public static void AddPlanetGeneration(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath)) { ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}"); return; }
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            try { index.Add(pak.FullPath, reader.ReadHeader(pak.FullPath), reader.Read(pak.FullPath)); }
            catch { /* unreadable PAK - skip */ }
        }
        var extraction = new ExtractionService();
        NMSTemplate? Decode(string path)
        {
            path = path.Replace(".MXML", ".MBIN", StringComparison.OrdinalIgnoreCase);
            if (!index.TryFind(path, out var pak, out var entry, out var header)) return null;
            try
            {
                var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, entry, header)!));
                mbin.Load();
                return mbin.GetData();
            }
            catch (Exception ex) { ConsoleStyle.Warning($"Couldn't read {path}: {ex.Message}"); return null; }
        }

        var rows = new List<CatalogItem>();
        var biomes = Decode(BiomeListFile) as GcBiomeListPerStarType;
        var files = Decode(BiomeFilesFile) as GcBiomeFileList;
        if (biomes is null || files is null)
            ConsoleStyle.Error("Planet type tables not found - planet types left out.");
        else
        {
            static BiomeFileOption[][] Options(GcBiomeFileList list) => list.BiomeFiles
                .Select(o => (o.FileOptions ?? new()).Select(x => new BiomeFileOption(x.PurpleSystemWeight, (int)x.SubType.BiomeSubType, x.Weight)).ToArray())
                .ToArray();
            static int[] Types(List<GcBiomeType>? list) => (list ?? new()).Select(b => (int)b.Biome).ToArray();
            var lists = biomes.StarType.Concat(new[] { biomes.LushYellow, biomes.AbandonedYellow }).ToArray();
            var tables = new BiomeTables(
                lists.Select(l => l.BiomeProbability.ToArray()).ToArray(),
                lists.Select(l => l.PrimeBiomeProbability.ToArray()).ToArray(),
                biomes.ConvertDeadToWeird,
                Types(files.ValidGiantPlanetBiome),
                Types(files.ValidPurpleMoonBiome),
                Types(files.ValidStartPlanetBiome),
                Options(files),
                Decode(BiomeArchiveFile) is GcBiomeFileList archive ? Options(archive) : null);
            rows.Add(new CatalogItem { GameId = "BIOMETABLES", NameEnglish = "BiomeTables", DescriptionEnglish = JsonSerializer.Serialize(tables) });

            var details = ProfileTables(files, biomes, Decode);
            if (details is not null)
            {
                var groups = HintGroups(files);
                var keys = groups.SelectMany(g => g.Hints).ToHashSet(StringComparer.OrdinalIgnoreCase);
                details = details with { HintGroups = groups, HintNames = EnglishTexts(index, extraction, keys) };
            }
            if (details is null) ConsoleStyle.Warning("Planet detail tables not found - planet resources and weather left out.");
            else rows.Add(new CatalogItem { GameId = "PLANETPROFILE", NameEnglish = "PlanetProfileTables", DescriptionEnglish = JsonSerializer.Serialize(details) });
        }
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "PlanetGeneration", "GcBiomeListPerStarType/GcBiomeFileList", BiomeListFile.ToLowerInvariant(), rows);
        ConsoleStyle.Success($"Wrote the planet generation tables to {catalogPath}.");
    }

    /// <summary>The optional object lists whose hints are certain (Core HintGroup): always
    /// active, every option placed, at least one hint.</summary>
    private static HintGroup[] HintGroups(GcBiomeFileList files) => (files.OptionalExternalObjectLists ?? new())
        .Where(g => g.ProbabilityOfBeingActive >= 1f && g.ExternalObjectFiles is { Count: > 0 } options
            && g.MinFilesToChoose >= options.Count
            && !g.OnlyOnDeepWater && !g.OnlyOnScrapWorlds && !g.NotOnScrapWorlds
            && options.Any(o => !string.IsNullOrEmpty(o.ResourceHint?.Value)))
        .Select(g => new HintGroup((int)g.OnlyOnBiome.Biome,
            g.ExternalObjectFiles.Select(o => o.ResourceHint?.Value ?? "").Where(h => h.Length > 0).Distinct().ToArray(),
            g.NotOnInfested, g.OnlyOnInfested, g.NotOnStartPlanets, g.NotOnDeadPlanets, g.NotOnWeirdPlanets,
            g.NotOnExtremePlanets, g.NotOnGasGiant, g.OnlyOnExtremeWeather, g.OnlyOnCorruptSentinels, g.OnlyOnExtremeSentinels))
        .ToArray();

    private static PlanetProfileTables? ProfileTables(GcBiomeFileList files, GcBiomeListPerStarType biomes, Func<string, NMSTemplate?> decode)
    {
        if (decode("GCTERRAINGLOBALS.GLOBAL.MBIN") is not GcTerrainGlobals terrain
            || decode("GCSOLARGENERATIONGLOBALS.GLOBAL.MBIN") is not GcSolarGenerationGlobals solar
            || decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is not GcGameplayGlobals gameplay
            || decode(WeatherListFile) is not GcWeatherTable weatherList
            || decode(CreatureDataFile) is not GcCreatureGenerationData creatures)
            return null;

        static string[] Ids(IEnumerable<libMBIN.NMS.NMSString0x10>? list) => (list ?? Enumerable.Empty<libMBIN.NMS.NMSString0x10>()).Select(s => s?.Value ?? "").ToArray();

        // Each biome file's weather weights, by type and subtype (a file shared by several
        // options is read once).
        var cache = new Dictionary<string, float[][]?>(StringComparer.OrdinalIgnoreCase);
        var weather = new float[][]?[files.BiomeFiles.Length][];
        for (int b = 0; b < weather.Length; b++)
        {
            var options = files.BiomeFiles[b].FileOptions ?? new();
            int subTypes = options.Count == 0 ? 0 : options.Max(o => (int)o.SubType.BiomeSubType) + 1;
            weather[b] = new float[][]?[subTypes];
            foreach (var o in options)
            {
                string path = o.Filename?.Value ?? "";
                if (!cache.TryGetValue(path, out var weights))
                    cache[path] = weights = decode(path) is GcBiomeData data
                        ? data.WeatherOptions.Select(w => w.WeatherWeightings.ToArray()).ToArray()
                        : null;
                weather[b][(int)o.SubType.BiomeSubType] = weights;
            }
        }
        var storms = weatherList.Table.Select(path => decode(path?.Value ?? "") is GcWeatherProperties p
            ? new StormChance(p.LowStormsChance, p.HighStormsChance)
            : new StormChance(0f, 0f)).ToArray();

        return new PlanetProfileTables(
            Ids(terrain.MiningSubstanceBiome), Ids(terrain.MiningSubstanceRare),
            Ids(terrain.MiningSubstanceStar), Ids(terrain.MiningSubstanceStarExtreme),
            solar.ExtremePlanetChance.ToArray(), weather, storms,
            gameplay.ViciousWeatherProbability, gameplay.ViciousStormProbability,
            biomes.LifeChance.ToArray(), biomes.AbandonedLifeChance.ToArray(), creatures.LifeChance.ToArray(),
            new SentinelTables(gameplay.LowSentinelProbability, gameplay.AggressiveSentinelProbability,
                gameplay.LowSentinelProbabilitySurvival, gameplay.AggressiveSentinelProbabilitySurvival,
                gameplay.NonAggressiveLushSurvivalProbabability, gameplay.ViciousSentinelProbability,
                gameplay.CorruptSentinelProbability, (int)solar.CorruptSentinelBuildingCheckDifficulty.CombatTimerDifficultyOption));
    }
}
