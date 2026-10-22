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
    private const string VoxelSettingsFile = "METADATA/SIMULATION/SOLARSYSTEM/VOXELGENERATORSETTINGS.MBIN";
    private const string BuildingTableFile = "METADATA/SIMULATION/ENVIRONMENT/PLANETBUILDINGTABLE.MBIN";

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
    ///   odds and sentinel odds (gcgameplayglobals), scrapyard density (planetbuildingtable.mbin),
    ///   and what the terrain and object steps read (voxelgeneratorsettings.mbin, each biome
    ///   file's terrain chances and object lists, biomefilenames' common and optional lists).
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
            if (details?.Objects is { } objects)
            {
                var keys = objects.Groups.SelectMany(g => g.Files).Select(f => f.Hint).Where(h => h.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
                details = details with { HintNames = EnglishTexts(index, extraction, keys) };
            }
            if (details is null) ConsoleStyle.Warning("Planet detail tables not found - planet resources and weather left out.");
            else rows.Add(new CatalogItem { GameId = "PLANETPROFILE", NameEnglish = "PlanetProfileTables", DescriptionEnglish = JsonSerializer.Serialize(details) });
        }
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "PlanetGeneration", "GcBiomeListPerStarType/GcBiomeFileList", BiomeListFile.ToLowerInvariant(), rows);
        ConsoleStyle.Success($"Wrote the planet generation tables to {catalogPath}.");
    }

    private static ObjectList Objects(GcExternalObjectListOptions o) => new(o.Name?.Value ?? "", o.Order, o.Probability,
        (int)o.TileType.TileType, o.Options?.Count ?? 0, o.ResourceHint?.Value ?? "");

    private static ObjectGroup Group(GcExternalObjectFileList g)
    {
        var rules = ObjectGroupRules.None;
        void Rule(bool on, ObjectGroupRules rule) { if (on) rules |= rule; }
        Rule(g.NotOnDeadPlanets, ObjectGroupRules.NotOnDeadPlanets); Rule(g.NotOnExtremePlanets, ObjectGroupRules.NotOnExtremePlanets);
        Rule(g.NotOnGasGiant, ObjectGroupRules.NotOnGasGiant); Rule(g.NotOnInfested, ObjectGroupRules.NotOnInfested);
        Rule(g.NotOnScrapWorlds, ObjectGroupRules.NotOnScrapWorlds); Rule(g.NotOnStartPlanets, ObjectGroupRules.NotOnStartPlanets);
        Rule(g.NotOnWeirdPlanets, ObjectGroupRules.NotOnWeirdPlanets); Rule(g.OnlyOnCorruptSentinels, ObjectGroupRules.OnlyOnCorruptSentinels);
        Rule(g.OnlyOnDeepWater, ObjectGroupRules.OnlyOnDeepWater); Rule(g.OnlyOnExtremeSentinels, ObjectGroupRules.OnlyOnExtremeSentinels);
        Rule(g.OnlyOnExtremeWeather, ObjectGroupRules.OnlyOnExtremeWeather); Rule(g.OnlyOnInfested, ObjectGroupRules.OnlyOnInfested);
        Rule(g.OnlyOnScrapWorlds, ObjectGroupRules.OnlyOnScrapWorlds);
        return new ObjectGroup(g.Id?.Value ?? "", (g.ExternalObjectFiles ?? new()).Select(Objects).ToArray(), g.MinFilesToChoose, g.MaxFilesToChoose,
            (int)g.OnlyOnBiome.Biome, g.ProbabilityOfBeingActive, g.SubBiomeProbability?.ToArray() ?? Array.Empty<float>(), rules);
    }

    /// <summary>The terrain step's inputs (Core TerrainTables): each voxelgeneratorsettings entry's
    /// Min layers and caves, gcterrainglobals' index ranges and sea levels.</summary>
    private static TerrainTables? Terrain(GcTerrainGlobals terrain, Func<string, NMSTemplate?> decode)
    {
        if (decode(VoxelSettingsFile) is not libMBIN.NMS.Toolkit.TkVoxelGeneratorSettingsArray voxel) return null;
        var settings = voxel.TerrainSettings.Select(e => new TerrainSetting(
            e.Min.NoiseLayers.Select(l => l.Active).ToArray(),
            e.Min.NoiseLayers.Select(l => (int)l.VoxelType.NoiseVoxelType).ToArray(),
            e.Min.Features.Select(f => f.Active).ToArray(),
            e.Min.Caves.Length > 0 && e.Min.Caves[0].Mouth.Active)).ToArray();
        return new TerrainTables(settings, terrain.TerrainPrimeIndexStart, terrain.TerrainPurpleSystemIndexStart,
            terrain.DebugLockTerrainSettingsIndex, terrain.SeaLevelMoon, terrain.SeaLevelGasGiant, terrain.SeaLevelWaterWorld,
            terrain.SeaLevelHigh, terrain.SeaLevelStandard, terrain.PurpleSystemMaxHighWaterChance, terrain.ForcePurpleSystemHighWater);
    }

    private static PlanetProfileTables? ProfileTables(GcBiomeFileList files, GcBiomeListPerStarType biomes, Func<string, NMSTemplate?> decode)
    {
        if (decode("GCTERRAINGLOBALS.GLOBAL.MBIN") is not GcTerrainGlobals terrain
            || decode("GCSOLARGENERATIONGLOBALS.GLOBAL.MBIN") is not GcSolarGenerationGlobals solar
            || decode("GCGAMEPLAYGLOBALS.GLOBAL.MBIN") is not GcGameplayGlobals gameplay
            || decode(WeatherListFile) is not GcWeatherTable weatherList
            || decode(CreatureDataFile) is not GcCreatureGenerationData creatures)
            return null;

        static string[] Ids(IEnumerable<libMBIN.NMS.NMSString0x10>? list) => (list ?? Enumerable.Empty<libMBIN.NMS.NMSString0x10>()).Select(s => s?.Value ?? "").ToArray();

        // Each biome file's weather weights and object data, by type and subtype (a file
        // shared by several options is read once).
        var cache = new Dictionary<string, GcBiomeData?>(StringComparer.OrdinalIgnoreCase);
        var weather = new float[][]?[files.BiomeFiles.Length][];
        var objects = new BiomeObjects?[files.BiomeFiles.Length][];
        for (int b = 0; b < weather.Length; b++)
        {
            var options = files.BiomeFiles[b].FileOptions ?? new();
            int subTypes = options.Count == 0 ? 0 : options.Max(o => (int)o.SubType.BiomeSubType) + 1;
            weather[b] = new float[][]?[subTypes];
            objects[b] = new BiomeObjects?[subTypes];
            foreach (var o in options)
            {
                string path = o.Filename?.Value ?? "";
                if (!cache.TryGetValue(path, out var data)) cache[path] = data = decode(path) as GcBiomeData;
                if (data is null) continue;
                int sub = (int)o.SubType.BiomeSubType;
                weather[b][sub] = data.WeatherOptions.Select(w => w.WeatherWeightings.ToArray()).ToArray();
                var t = data.Terrain;
                objects[b][sub] = new BiomeObjects(t.WaterActiveFrequency, t.HighWaterActiveFrequency, t.RockTileFrequency,
                    t.SubstanceTileFrequency, t.NoiseLayers.ToArray(), t.Features.ToArray(), t.Caves.Length > 0 ? t.Caves[0] : 0f,
                    (data.ExternalObjectLists ?? new()).Select(Objects).ToArray());
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
                gameplay.CorruptSentinelProbability, (int)solar.CorruptSentinelBuildingCheckDifficulty.CombatTimerDifficultyOption),
            Terrain(terrain, decode) is { } terrainTables
                ? new ObjectTables(objects, (files.CommonExternalObjectLists ?? new()).Select(Objects).ToArray(),
                    (files.OptionalExternalObjectLists ?? new()).Select(Group).ToArray(), terrainTables)
                : null,
            ScrapYardDensity: decode(BuildingTableFile) is GcBuildingDefinitionTable buildings
                && (int)GcBuildingClassification.BuildingClassEnum.ScrapYard < buildings.BuildingPlacement.Length
                ? buildings.BuildingPlacement[(int)GcBuildingClassification.BuildingClassEnum.ScrapYard].Density.ToArray()
                : null);
    }
}
