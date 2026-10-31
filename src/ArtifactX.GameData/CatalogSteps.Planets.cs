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
    private const string CreatureArchetypesFile = "METADATA/SIMULATION/ECOSYSTEM/CREATUREGENERATIONARCHETYPES.MBIN";
    private const string CreatureDataTableFile = "METADATA/SIMULATION/ECOSYSTEM/CREATUREDATATABLE.MBIN";
    private const string CreatureFilenameTableFile = "METADATA/SIMULATION/ECOSYSTEM/CREATUREFILENAMETABLE.MBIN";
    private const string VoxelSettingsFile = "METADATA/SIMULATION/SOLARSYSTEM/VOXELGENERATORSETTINGS.MBIN";
    private const string BuildingTableFile = "METADATA/SIMULATION/ENVIRONMENT/PLANETBUILDINGTABLE.MBIN";
    private const string InventoryTableFile = "METADATA/REALITY/TABLES/INVENTORYTABLE.MBIN";

    /// <summary>
    /// What planets are generated from (2026-10-22, ArtifactX.Core PlanetGenerator and
    /// PlanetProfileGenerator). Category "PlanetGeneration", rows:
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
    /// - "INVENTORYGENERATION": JSON in the shape of Core's InventoryGenerationTables - S-Class
    ///   base stat roll ranges, class probabilities, max inventory slot capacities
    ///   (inventorytable.mbin) and planetary building placement densities (planetbuildingtable.mbin).
    /// - "BUILDINGPLACEMENT": JSON array of Core's BuildingClassPlacement (planetbuildingtable.mbin): each class's
    ///   density per level and planet restrictions, what PlanetBuildingSites' slot table (which grid site holds what,
    ///   crashed ships included) is built from.
    /// - "BUILDINGOVERRIDES": JSON array of Core's BuildingOverrideRule (planetbuildingtable.mbin): the portal,
    ///   shelter, base, distress signal and start spawn rules PlanetOverridePlacement uses.
    /// - "STATIONEQUIPMENT": JSON in the shape of Core's StationEquipmentTables - solar layout
    ///   globals, station scene roots, and verified standard TypeB WeaponTerminal transforms.
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

        if (InventoryTables(Decode) is { } invTables)
            rows.Add(new CatalogItem { GameId = "INVENTORYGENERATION", NameEnglish = "InventoryGenerationTables", DescriptionEnglish = JsonSerializer.Serialize(invTables) });

        if (Decode(BuildingTableFile) is GcBuildingDefinitionTable placementTable)
            rows.Add(new CatalogItem { GameId = "BUILDINGPLACEMENT", NameEnglish = "BuildingClassPlacement", DescriptionEnglish = JsonSerializer.Serialize(
                placementTable.BuildingPlacement.Select(d => new BuildingClassPlacement(d.Density, d.PlanetRestrictions.RequiresCorruptSentinels,
                    d.PlanetRestrictions.RequiresRelicWorld, d.PlanetRestrictions.RequiresScrapWorld, d.PlanetRestrictions.RequiresWater,
                    d.EnabledWhenPlanetHasNoNPCs)).ToArray()) });

        if (BuildingOverrides(Decode) is { } overrides)
            rows.Add(new CatalogItem { GameId = "BUILDINGOVERRIDES", NameEnglish = "BuildingOverrideRules", DescriptionEnglish = JsonSerializer.Serialize(overrides) });

        if (StationEquipment(Decode) is { } stationEquipment)
            rows.Add(new CatalogItem { GameId = "STATIONEQUIPMENT", NameEnglish = "StationEquipmentTables", DescriptionEnglish = JsonSerializer.Serialize(stationEquipment) });
        if (MultiToolTechnology(Decode) is { } technology)
            rows.Add(new CatalogItem { GameId = "MULTITOOLTECHNOLOGY", NameEnglish = "MultiToolTechnologyTables", DescriptionEnglish = JsonSerializer.Serialize(technology) });
        if (MultiToolModel(Decode) is { } multiToolModel)
            rows.Add(new CatalogItem { GameId = "MULTITOOLMODEL", NameEnglish = "MultiToolModelTables", DescriptionEnglish = JsonSerializer.Serialize(multiToolModel) });
        if (GalaxyGeneration(Decode) is { } galaxyGeneration)
            rows.Add(new CatalogItem { GameId = "GALAXYGENERATION", NameEnglish = "GalaxyGenerationSettings", DescriptionEnglish = JsonSerializer.Serialize(galaxyGeneration) });
        if (GalaxyPlacement(Decode) is { } galaxyPlacement)
            rows.Add(new CatalogItem { GameId = "GALAXYPLACEMENT", NameEnglish = "GalaxyPlacementSettings", DescriptionEnglish = JsonSerializer.Serialize(galaxyPlacement) });

        if (StarshipDisplay(Decode) is { } starshipDisplay)
            rows.Add(new CatalogItem { GameId = "STARSHIPDISPLAY", NameEnglish = "StarshipEvaluationTables", DescriptionEnglish = JsonSerializer.Serialize(starshipDisplay) });

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
    /// Min layers and caves, its Min and Max noise layers and base level (what a planet's terrain is drawn from),
    /// gcterrainglobals' index ranges and sea levels.</summary>
    private static TerrainTables? Terrain(GcTerrainGlobals terrain, Func<string, NMSTemplate?> decode)
    {
        if (decode(VoxelSettingsFile) is not libMBIN.NMS.Toolkit.TkVoxelGeneratorSettingsArray voxel) return null;
        var settings = voxel.TerrainSettings.Select(e => new TerrainSetting(
            e.Min.NoiseLayers.Select(l => l.Active).ToArray(),
            e.Min.NoiseLayers.Select(l => (int)l.VoxelType.NoiseVoxelType).ToArray(),
            e.Min.Features.Select(f => f.Active).ToArray(),
            e.Min.Caves.Length > 0 && e.Min.Caves[0].Mouth.Active,
            e.Min.NoiseLayers.Select(NoiseLayer).ToArray(),
            e.Max.NoiseLayers.Select(NoiseLayer).ToArray(),
            e.Min.NoSeaBaseLevel)).ToArray();
        return new TerrainTables(settings, terrain.TerrainPrimeIndexStart, terrain.TerrainPurpleSystemIndexStart,
            terrain.DebugLockTerrainSettingsIndex, terrain.SeaLevelMoon, terrain.SeaLevelGasGiant, terrain.SeaLevelWaterWorld,
            terrain.SeaLevelHigh, terrain.SeaLevelStandard, terrain.PurpleSystemMaxHighWaterChance, terrain.ForcePurpleSystemHighWater);
    }

    private static TerrainNoiseLayer NoiseLayer(libMBIN.NMS.Toolkit.TkNoiseUberLayerData l)
    {
        var n = l.NoiseData;
        return new TerrainNoiseLayer(new UberNoiseSettings(n.AltitudeErosion, n.AmplifyFeatures, n.Gain, n.Lacunarity,
                n.PerturbFeatures, n.RemapFromMax, n.RemapFromMin, n.RemapToMax, n.RemapToMin, n.RidgeErosion,
                n.SharpToRoundFeatures, n.SlopeBias, n.SlopeErosion, n.SlopeGain),
            (int)n.DebugNoiseType, n.Octaves, l.Height, l.HeightOffset, l.MaximumLOD, (int)l.Offset.OffsetType,
            l.PlateauRegionSize, l.PlateauSharpness, l.PlateauStratas, l.RegionGain, l.RegionRatio, l.RegionScale,
            l.SeedOffset, l.SmoothRadius, l.TileBlendMeters, (int)l.VoxelType.NoiseVoxelType, (int)l.WaterFade, l.Width,
            l.Active, l.Subtract);
    }

    /// <summary>Model radii (half the AABB's ground diagonal, GcBuildingSpawnData.Radius) of the override classes
    /// whose OverrideRadius is -1, as the running game measured them (build 183791, read 2026-10-28): the Shelter's
    /// first model and the Portal. Only prime planets use them (the flatness ring). The game measures the loaded
    /// models; extracting that from the model files is not done yet.</summary>
    private static readonly Dictionary<int, float> MeasuredModelRadius = new() { [2] = 22.641382f, [16] = 37.768147f };

    /// <summary>"BUILDINGOVERRIDES": every class's override rules (Core BuildingOverrideRule) from
    /// planetbuildingtable.mbin - what PlanetOverridePlacement places the portal, shelter and base from.</summary>
    private static BuildingOverrideRule[]? BuildingOverrides(Func<string, NMSTemplate?> decode)
    {
        if (decode(BuildingTableFile) is not GcBuildingDefinitionTable table) return null;
        return table.BuildingPlacement.Select((d, c) => new BuildingOverrideRule(c, d.NumOverridesToGenerate,
            d.NumOverridesToGenerateWaterworlds, (int)d.FlattenType.WaterPlacement, d.OverrideRadius, d.MinHeight,
            d.MaxHeight, d.EnabledWhenPlanetHasNoNPCs, MeasuredModelRadius.GetValueOrDefault(c))).ToArray();
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
                : null,
            Creatures: Creatures(creatures, decode));
    }

    private static CreatureTables? Creatures(GcCreatureGenerationData cgen, Func<string, NMSTemplate?> decode)
    {
        if (decode(CreatureArchetypesFile) is not GcCreatureGenerationArchetypes carch
            || decode(CreatureDataTableFile) is not GcCreatureDataTable cdt
            || decode(CreatureFilenameTableFile) is not GcCreatureFilenameTable cfnt)
            return null;

        var globals = new CreatureGlobals(
            cgen.SandwormPresenceChance.ToArray(),
            cgen.AirGroupsPerKm.ToArray(),
            cgen.CaveGroupsPerKm.ToArray(),
            cgen.DensityModifiers.ToArray(),
            cgen.GroundGroupsPerKm.ToArray(),
            cgen.LifeChance.ToArray(),
            cgen.LifeLevelDensityModifiers.ToArray(),
            cgen.RarityFrequencyModifiers.ToArray(),
            cgen.RoleFrequencyModifiers.ToArray(),
            cgen.WaterGroupsPerKm.ToArray(),
            cgen.HerdCreaturePenalty);

        var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (cfnt.Table is not null)
        {
            foreach (var f in cfnt.Table)
                if (!string.IsNullOrEmpty(f.ID?.Value))
                    validIds.Add(f.ID.Value);
        }

        var creatures = new List<CreatureEntry>();
        if (cdt.Table is not null)
        {
            foreach (var c in cdt.Table)
            {
                string id = c.Id?.Value ?? "";
                if (!validIds.Contains(id)) continue;
                var tags = (c.Tags ?? new()).Select(t => t.Tag?.Value ?? "").Where(t => t.Length > 0).ToArray();
                creatures.Add(new CreatureEntry(
                    id,
                    (int)c.ForceType.CreatureType,
                    c.EcoSystemCreature,
                    c.OnlySpawnWhenIdIsForced,
                    (int)c.HerbivoreProbabilityModifier.CreatureRoleFrequencyModifier,
                    (int)c.PredatorProbabilityModifier.CreatureRoleFrequencyModifier,
                    (int)c.Rarity.CreatureRarity,
                    tags));
            }
        }

        static CreatureDomainWeightedLists MapDomainLists(GcCreatureGenerationWeightedList w) =>
            new(
                (w.Air ?? new()).Select(e => new CreatureWeightedEntry(e.Archetype?.Value ?? "", e.Weight)).ToArray(),
                (w.Cave ?? new()).Select(e => new CreatureWeightedEntry(e.Archetype?.Value ?? "", e.Weight)).ToArray(),
                (w.Ground ?? new()).Select(e => new CreatureWeightedEntry(e.Archetype?.Value ?? "", e.Weight)).ToArray(),
                (w.Water ?? new()).Select(e => new CreatureWeightedEntry(e.Archetype?.Value ?? "", e.Weight)).ToArray());

        static CreatureOptWeightedList MapOptList(GcCreatureGenerationOptionalWeightedList opt) =>
            new(opt.Probability, MapDomainLists(opt.Archetypes));

        var subBiomes = (cgen.SubBiomeSpecific ?? Array.Empty<GcCreatureGenerationOptionalWeightedList>()).Select(MapOptList).ToArray();
        var biomes = (cgen.BiomeSpecific ?? Array.Empty<GcCreatureGenerationOptionalWeightedList>()).Select(MapOptList).ToArray();
        var abandoned = MapOptList(cgen.AbandonedSystemSpecific);
        var empty = MapOptList(cgen.EmptySystemSpecific);
        var purple = MapOptList(cgen.PurpleSystemSpecific);
        var generic = MapDomainLists(cgen.Generic);
        var airEmpty = (cgen.AirArchetypesForEmptyGround ?? new()).Select(e => new CreatureWeightedEntry(e.Archetype?.Value ?? "", e.Weight)).ToArray();

        var archetypeLists = new CreatureArchetypeLists(subBiomes, biomes, abandoned, empty, purple, generic, airEmpty);

        static Dictionary<string, CreatureDomainArchetype> MapDomainArchetypes(List<GcCreatureGenerationDomainTable>? list)
        {
            var dict = new Dictionary<string, CreatureDomainArchetype>(StringComparer.OrdinalIgnoreCase);
            if (list is null) return dict;
            foreach (var dt in list)
            {
                string id = dt.Id?.Value ?? "";
                var tables = (dt.Tables ?? new()).Select(t => new CreatureDomainTableEntry(t.File?.Value ?? "", (int)t.DensityModifier.Density)).ToArray();
                var additional = (dt.AdditionalTables ?? new()).Select(a => new CreatureDomainAdditional(
                    (a.Tables ?? new()).Select(t => new CreatureDomainTableEntry(t.File?.Value ?? "", (int)t.DensityModifier.Density)).ToArray(),
                    a.MaxTablesToAdd,
                    a.ChanceOfHemisphereLimit,
                    a.MaxToHemisphereLimit)).ToArray();
                dict[id] = new CreatureDomainArchetype(dt.ChanceOfHemisphereLimit, dt.MaxToHemisphereLimit, tables, additional);
            }
            return dict;
        }

        var domainArchetypes = new Dictionary<string, Dictionary<string, CreatureDomainArchetype>>(StringComparer.OrdinalIgnoreCase)
        {
            ["air"] = MapDomainArchetypes(carch.AirArchetypes),
            ["cave"] = MapDomainArchetypes(carch.CaveArchetypes),
            ["ground"] = MapDomainArchetypes(carch.GroundArchetypes),
            ["water"] = MapDomainArchetypes(carch.WaterArchetypes)
        };

        var allTableFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in domainArchetypes.Values)
        {
            foreach (var arch in d.Values)
            {
                foreach (var t in arch.Tables)
                    if (!string.IsNullOrEmpty(t.Table)) allTableFiles.Add(t.Table);
                foreach (var a in arch.Additional)
                    foreach (var t in a.Tables)
                        if (!string.IsNullOrEmpty(t.Table)) allTableFiles.Add(t.Table);
            }
        }

        var ecosystemTables = new Dictionary<string, CreatureRoleCandidate[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in allTableFiles)
        {
            if (decode(path) is GcCreatureRoleDescriptionTable rdt && rdt.RoleDescription is not null)
            {
                var roles = rdt.RoleDescription.Select(r => new CreatureRoleCandidate(
                    r.ForceID?.Value ?? "",
                    r.RequireTag?.Value ?? "",
                    (int)r.Density.Density,
                    (int)r.ForceType.CreatureType,
                    r.ProbabilityOfBeingEnabled,
                    (int)r.Role.CreatureRole)).ToArray();
                ecosystemTables[path] = roles;
            }
        }

        return new CreatureTables(globals, creatures.ToArray(), archetypeLists, domainArchetypes, ecosystemTables);
    }

    private static InventoryGenerationTables? InventoryTables(Func<string, NMSTemplate?> decode)
    {
        if (decode(InventoryTableFile) is not GcInventoryTable inv) return null;

        const int sClassIndex = (int)GcInventoryClass.InventoryClassEnum.S;

        var shipStats = (inv.ShipBaseStatsData ?? Array.Empty<GcInventoryGenerationBaseStatData>())
            .Select(d => d?.BaseStatsPerClass != null && sClassIndex < d.BaseStatsPerClass.Length && d.BaseStatsPerClass[sClassIndex]?.BaseStats != null
                ? d.BaseStatsPerClass[sClassIndex].BaseStats
                    .Select(e => new BaseStatRange(e.BaseStatID?.Value ?? "", e.Min, e.Max))
                    .ToArray()
                : Array.Empty<BaseStatRange>())
            .ToArray();

        var weaponStats = (inv.WeaponBaseStatsData ?? Array.Empty<GcInventoryGenerationBaseStatData>())
            .Select(d => d?.BaseStatsPerClass != null && sClassIndex < d.BaseStatsPerClass.Length && d.BaseStatsPerClass[sClassIndex]?.BaseStats != null
                ? d.BaseStatsPerClass[sClassIndex].BaseStats
                    .Select(e => new BaseStatRange(e.BaseStatID?.Value ?? "", e.Min, e.Max))
                    .ToArray()
                : Array.Empty<BaseStatRange>())
            .ToArray();

        var classProbs = (inv.ClassProbabilityData ?? Array.Empty<GcInventoryClassProbabilities>())
            .Select(p => p?.ClassProbabilities?.ToArray() ?? Array.Empty<float>())
            .ToArray();

        var shipMaxCargo = (inv.ShipInventoryMaxUpgradeSize ?? Array.Empty<GcShipInventoryMaxUpgradeCapacity>())
            .Select(c => c?.MaxInventoryCapacity != null && sClassIndex < c.MaxInventoryCapacity.Length ? c.MaxInventoryCapacity[sClassIndex] : 0)
            .ToArray();

        var shipMaxTech = (inv.ShipInventoryMaxUpgradeSize ?? Array.Empty<GcShipInventoryMaxUpgradeCapacity>())
            .Select(c => c?.MaxTechInventoryCapacity != null && sClassIndex < c.MaxTechInventoryCapacity.Length ? c.MaxTechInventoryCapacity[sClassIndex] : 0)
            .ToArray();

        int weaponMaxTech = inv.WeaponInventoryMaxUpgradeSize?.MaxInventoryCapacity != null
            && sClassIndex < inv.WeaponInventoryMaxUpgradeSize.MaxInventoryCapacity.Length
            ? inv.WeaponInventoryMaxUpgradeSize.MaxInventoryCapacity[sClassIndex]
            : 60;

        Dictionary<string, float[]>? buildingDensities = null;
        if (decode(BuildingTableFile) is GcBuildingDefinitionTable bld && bld.BuildingPlacement != null)
        {
            buildingDensities = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            var enumNames = Enum.GetNames<GcBuildingClassification.BuildingClassEnum>();
            for (int i = 0; i < bld.BuildingPlacement.Length && i < enumNames.Length; i++)
            {
                if (bld.BuildingPlacement[i]?.Density is { } density)
                    buildingDensities[enumNames[i]] = density.ToArray();
            }
        }

        var nativePools = (decode("GCSIMULATIONGLOBALS.GLOBAL.MBIN") as GcSimulationGlobals)?.MultitoolPool;
        // An incomplete definition must not be skipped: that would shift the RNG stream.
        var pools = nativePools is not null && nativePools.All(p => p is not null
            && p.File?.Value is not null && p.PoolType is not null)
            ? nativePools.Select(p => new MultiToolPoolDefinition(p.File.Value, p.MinDraw, p.MaxDraw,
                p.PoolProbability, p.PoolType.MultiToolPoolType.ToString())).ToArray()
            : null;
        var weaponClasses = inv.WeaponBaseStatsData?
            .Select(kind => kind?.BaseStatsPerClass?.Select(rating => rating?.BaseStats is { } entries
                && entries.All(e => e?.BaseStatID?.Value is not null)
                    ? entries.Select(e => new BaseStatRange(e.BaseStatID.Value, e.Min, e.Max)).ToArray()
                    : Array.Empty<BaseStatRange>()).ToArray() ?? Array.Empty<BaseStatRange[]>()).ToArray();
        // Native category 3 maps to WeaponMedium at 0x144a60590 in build 180836,
        // independently of the actual tool's size. Bounds are inclusive indices.
        var layoutRows = inv.GenerationData?.GenerationDataPerSizeType;
        int weaponLayoutIndex = (int)GcInventoryLayoutSizeType.SizeTypeEnum.WeaponMedium;
        var weaponLayout = layoutRows is not null && weaponLayoutIndex < layoutRows.Length
            ? layoutRows[weaponLayoutIndex] : null;
        var specialSlots = weaponLayout?.SpecialTechSlotMaxIndex is { } maximumIndex
            ? new SuperchargedSlotRules(maximumIndex.X, maximumIndex.Y, weaponLayout.MaxNumSpecialTechSlots) : null;
        var weaponLayouts = new[] { GcInventoryLayoutSizeType.SizeTypeEnum.WeaponSmall,
                GcInventoryLayoutSizeType.SizeTypeEnum.WeaponMedium, GcInventoryLayoutSizeType.SizeTypeEnum.WeaponLarge }
            .Where(size => layoutRows is not null && (int)size < layoutRows.Length && layoutRows[(int)size]?.TechBounds is not null)
            .Select(size =>
            {
                var entry = layoutRows![(int)size];
                var bounds = entry.TechBounds;
                return new MultiToolLayoutRules(size.ToString(), entry.MinTechSlots, entry.MaxTechSlots,
                    new InventoryLayoutBounds(bounds.MaxWidthSmall, bounds.MaxHeightSmall,
                        bounds.MaxWidthStandard, bounds.MaxHeightStandard, bounds.MaxWidthLarge, bounds.MaxHeightLarge));
            }).ToArray();
        var weaponLayoutNames = weaponLayouts.Select(r => r.SizeType).ToHashSet(StringComparer.Ordinal);
        var layoutByTag = (inv.Table ?? new()).Where(entry => entry.LayoutSizeType is not null
                && weaponLayoutNames.Contains(entry.LayoutSizeType.SizeType.ToString()))
            .ToDictionary(entry => entry.Id.Value, entry => entry.LayoutSizeType.SizeType.ToString(), StringComparer.Ordinal);

        var shipClasses = inv.ShipBaseStatsData?
            .Select(kind => kind?.BaseStatsPerClass?.Select(rating => rating?.BaseStats is { } entries
                && entries.All(e => e?.BaseStatID?.Value is not null)
                    ? entries.Select(e => new BaseStatRange(e.BaseStatID.Value, e.Min, e.Max)).ToArray()
                    : Array.Empty<BaseStatRange>()).ToArray() ?? Array.Empty<BaseStatRange[]>()).ToArray();

        var sizeTypeNames = Enum.GetNames<GcInventoryLayoutSizeType.SizeTypeEnum>();
        var shipLayouts = layoutRows is not null
            ? layoutRows.Select((entry, idx) => entry?.Bounds is not null && entry.TechBounds is not null && idx < sizeTypeNames.Length
                ? new StarshipLayoutRules(
                    sizeTypeNames[idx],
                    idx,
                    entry.MinSlots,
                    entry.MaxSlots,
                    entry.MinTechSlots,
                    entry.MaxTechSlots,
                    new InventoryLayoutBounds(entry.Bounds.MaxWidthSmall, entry.Bounds.MaxHeightSmall,
                        entry.Bounds.MaxWidthStandard, entry.Bounds.MaxHeightStandard, entry.Bounds.MaxWidthLarge, entry.Bounds.MaxHeightLarge),
                    new InventoryLayoutBounds(entry.TechBounds.MaxWidthSmall, entry.TechBounds.MaxHeightSmall,
                        entry.TechBounds.MaxWidthStandard, entry.TechBounds.MaxHeightStandard, entry.TechBounds.MaxWidthLarge, entry.TechBounds.MaxHeightLarge))
                : null).Where(r => r is not null).Select(r => r!).ToArray()
            : null;

        var shipCostData = inv.ShipCostData?.InventoryCostData?
            .Select(c => c?.ClassMultiplier is { Length: >= 4 } mult
                ? new StarshipCostRules(c.MinSlots, c.MinValueInMillions, c.MaxSlots, c.MaxValueInMillions,
                    c.CoolMultiplier, c.TradeInMultiplier, mult.ToArray())
                : new StarshipCostRules(0, 0f, 0, 0f, 0f, 0f, [0f, 0f, 0f, 0f]))
            .ToArray();

        var tableMap = (inv.Table ?? new())
            .Where(e => !string.IsNullOrEmpty(e?.Id?.Value) && e.LayoutSizeType is not null)
            .ToDictionary(e => e.Id.Value, e => (int)e.LayoutSizeType.SizeType, StringComparer.OrdinalIgnoreCase);

        var technologyOverrides = new Dictionary<string, bool?>(StringComparer.Ordinal);
        StarshipInventoryNodeRule[] ExtractShipSceneRules(string scenePath, string typeName)
        {
            var rules = new List<StarshipInventoryNodeRule>();
            bool hasOverrides = false, complete = true;
            void Walk(string path, List<string> activeStack, int depth)
            {
                if (depth > 16 || decode(path) is not libMBIN.NMS.Toolkit.TkSceneNodeData root)
                { complete = false; return; }
                var selectable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string descPath = path.Replace(".SCENE.MBIN", ".DESCRIPTOR.MBIN", StringComparison.OrdinalIgnoreCase);
                if (decode(descPath) is libMBIN.NMS.Toolkit.TkModelDescriptorList dlist)
                {
                    void Collect(libMBIN.NMS.Toolkit.TkModelDescriptorList l)
                    {
                        foreach (var g in l.List ?? new())
                            foreach (var d in g.Descriptors ?? new())
                            {
                                if (!string.IsNullOrEmpty(d.Id?.Value)) selectable.Add(d.Id.Value);
                                foreach (var c in d.Children?.OfType<libMBIN.NMS.Toolkit.TkModelDescriptorList>() ?? Enumerable.Empty<libMBIN.NMS.Toolkit.TkModelDescriptorList>())
                                    Collect(c);
                            }
                    }
                    Collect(dlist);
                }
                void WalkNode(libMBIN.NMS.Toolkit.TkSceneNodeData node, List<string> req)
                {
                    string checkName = node.Name?.Value ?? "";
                    int lodIdx = checkName.IndexOf("LOD", StringComparison.Ordinal);
                    if (lodIdx >= 0 && checkName.Length - lodIdx == 4 && char.IsDigit(checkName[lodIdx + 3]))
                        checkName = checkName[..lodIdx];
                    var nextReq = selectable.Contains(checkName) ? new List<string>(req) { checkName } : req;

                    foreach (var attr in node.Attributes ?? new())
                    {
                        if (attr.Name?.Value == "ATTACHMENT" && !string.IsNullOrEmpty(attr.Value?.Value))
                        {
                            if (decode(attr.Value.Value.Replace('\\', '/').ToUpperInvariant()) is not libMBIN.NMS.Toolkit.TkAttachmentData ent)
                            { complete = false; continue; }
                            foreach (var comp in ent.Components ?? new())
                                if (comp.Template is GcCustomInventoryComponentData invComp)
                                {
                                    hasOverrides |= invComp.DesiredTechs is { Count: > 0 };
                                    if (!string.IsNullOrEmpty(invComp.Size?.Value) && tableMap.TryGetValue(invComp.Size.Value, out int st))
                                        rules.Add(new StarshipInventoryNodeRule(nextReq.ToArray(), st, invComp.Cool));
                                }
                        }
                        if (node.Type?.Value == "REFERENCE" && attr.Name?.Value == "SCENEGRAPH" && !string.IsNullOrEmpty(attr.Value?.Value))
                            Walk(attr.Value.Value.Replace('\\', '/').ToUpperInvariant(), nextReq, depth + 1);
                    }
                    foreach (var child in node.Children ?? new())
                        WalkNode(child, nextReq);
                }
                WalkNode(root, activeStack);
            }
            Walk(scenePath, [], 0);
            technologyOverrides[typeName] = hasOverrides ? true : complete ? false : null;
            return rules.ToArray();
        }

        var shipModelRules = new Dictionary<string, StarshipInventoryNodeRule[]>(StringComparer.Ordinal);
        foreach (var shipType in new[]
        {
            SystemStarshipType.Fighter, SystemStarshipType.Hauler, SystemStarshipType.Explorer,
            SystemStarshipType.Shuttle, SystemStarshipType.Exotic, SystemStarshipType.Solar,
            SystemStarshipType.SentinelInterceptor
        })
        {
            if (StarshipInventoryGeneration.ScenePath(shipType) is { } scenePath)
                shipModelRules[shipType.ToString()] = ExtractShipSceneRules(scenePath, shipType.ToString());
        }

        var aiShipGlobals = decode("GCAISPACESHIPGLOBALS.GLOBAL.MBIN") as GcAISpaceshipGlobals;

        return new InventoryGenerationTables(shipStats, weaponStats, classProbs, shipMaxCargo, shipMaxTech,
            weaponMaxTech, buildingDensities, pools, weaponClasses, specialSlots, weaponLayouts, layoutByTag,
            shipClasses, shipLayouts, shipCostData, shipModelRules,
            aiShipGlobals?.CrashedShipBrokenSlotChance, aiShipGlobals?.CrashedShipMinNonBrokenSlots, technologyOverrides);
    }
}
