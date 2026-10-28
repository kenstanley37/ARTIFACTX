using System.Numerics;
using System.Text.Json;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

/// <summary>"overrides address biometables.json planetprofile.json stationequipment.json voxelgeneratorsettings.mbin
/// planetbuildingtable.mbin expected.json": runs Core's PlanetOverridePlacement for every planet of a system from its
/// address alone (terrain layers and override rules read from the game files here, the rest from the catalog's
/// PlanetGeneration rows) and compares with the game's records read live (expected.json, keyed by planet address:
/// class, key, position). Model radii (Shelter, Portal) are the measured values until the catalog extracts them.</summary>
static class Overrides
{
    public static void Check(string address, string biomeFile, string profileFile, string stationFile, string voxelFile,
        string tableFile, string expectedFile)
    {
        ulong ua = Convert.ToUInt64(address.Replace("0x", ""), 16);
        int galaxy = (int)((ua >> 32) & 0xFF);
        ulong portal = (((ua >> 40) & 0xFFF) << 32) | (ua & 0xFFFFFFFF);
        var biomes = JsonSerializer.Deserialize<BiomeTables>(File.ReadAllText(biomeFile))!;
        var profiles = JsonSerializer.Deserialize<PlanetProfileTables>(File.ReadAllText(profileFile))!;
        var stations = JsonSerializer.Deserialize<StationEquipmentTables>(File.ReadAllText(stationFile))!;
        // "-" for the voxel file and a .json rules file: everything from the catalog rows, as the app reads it.
        var voxel = voxelFile == "-" ? null : Load<TkVoxelGeneratorSettingsArray>(voxelFile);
        var expected = JsonDocument.Parse(File.ReadAllText(expectedFile)).RootElement;
        var radii = new Dictionary<int, float> { [2] = 22.641382217407227f, [16] = 37.76814651489258f, [28] = 10f };
        var rules = tableFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Deserialize<List<BuildingOverrideRule>>(File.ReadAllText(tableFile))!
            : Load<GcBuildingDefinitionTable>(tableFile).BuildingPlacement.Select((d, c) => new BuildingOverrideRule(c, d.NumOverridesToGenerate,
                d.NumOverridesToGenerateWaterworlds, (int)d.FlattenType.WaterPlacement, d.OverrideRadius, d.MinHeight, d.MaxHeight,
                d.EnabledWhenPlanetHasNoNPCs, radii.GetValueOrDefault(c))).ToList();

        var types = PlanetGenerator.Generate(portal, galaxy, biomes);
        var attributes = SystemGenerator.Attributes(portal, galaxy);
        var seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
        var layout = SolarLayoutGeneration.ForSystem(portal, galaxy, stations.SolarLayout, true)!;
        var terrainTables = profiles.Objects!.Terrain;
        int total = 0, keys = 0;
        float worst = 0;
        for (int i = 0; i < Math.Min(types.Length, seeds.Length); i++)
        {
            string planetUa = $"{((ulong)(i + 1) << 52) | (ua & ~(0xFUL << 52)):X16}";
            if (!expected.TryGetProperty(planetUa, out var want)) continue;
            var body = types[i];
            var choice = PlanetProfileGenerator.TerrainChoiceFor(seeds[i], body, (int)attributes.Star, profiles)!;
            var setting = terrainTables.Settings[choice.SettingIndex];
            if (voxel is not null)
            {
                var element = voxel.TerrainSettings[choice.SettingIndex];
                setting = setting with
                {
                    MinLayers = element.Min.NoiseLayers.Select(Layer).ToArray(),
                    MaxLayers = element.Max.NoiseLayers.Select(Layer).ToArray(),
                    NoSeaBaseLevel = element.Min.NoSeaBaseLevel,
                };
            }
            var terrain = PlanetTerrain.Generate(seeds[i], setting, choice, terrainTables.SeaStandard, body.BodyClass == 3, body.DeadToWeird)!;
            float radius = PlanetBuildingSites.Radius(body.BodyClass)!.Value;
            var c = layout.Centers[i];
            var centre = new Vector3(MathF.Floor(c.X / 1000f) * 1000f, MathF.Floor(c.Y / 1000f) * 1000f, MathF.Floor(c.Z / 1000f) * 1000f);
            var planet = new OverridePlanet(seeds[i], radius, PlanetBuildingSites.Divisions(radius), PlanetBuildingSites.VoronoiSeeds(seeds[i]).SectorSeed,
                body.Prime, body.Biome, choice.Sea, centre, terrain, terrainTables.SeaMoon, terrainTables.SeaGasGiant);
            var placed = PlanetOverridePlacement.Place(planet, rules);
            var records = want.EnumerateArray().ToList();
            int ok = 0;
            float planetWorst = 0;
            for (int r = 0; r < records.Count; r++)
            {
                total++;
                if (r >= placed.Length) continue;
                var w = records[r];
                if (placed[r].Class != w.GetProperty("cls").GetInt32()) continue;
                if (placed[r].Key == Convert.ToUInt64(w.GetProperty("key").GetString(), 16)) { ok++; keys++; }
                var p = w.GetProperty("pos");
                float d = Vector3.Distance(placed[r].Position, new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()));
                planetWorst = MathF.Max(planetWorst, d);
            }
            worst = MathF.Max(worst, planetWorst);
            Console.WriteLine($"{planetUa}: records {placed.Length}/{records.Count}, keys {ok}/{records.Count}, worst {planetWorst:F3} m");
        }
        Console.WriteLine($"total keys {keys}/{total}, worst position {worst:F3} m");
    }

    private static T Load<T>(string file) where T : class
    {
        var mbin = new MBINFile(File.OpenRead(file));
        mbin.Load();
        return (T)(object)mbin.GetData();
    }

    private static TerrainNoiseLayer Layer(TkNoiseUberLayerData l)
    {
        var n = l.NoiseData;
        return new TerrainNoiseLayer(new UberNoiseSettings(n.AltitudeErosion, n.AmplifyFeatures, n.Gain, n.Lacunarity, n.PerturbFeatures,
                n.RemapFromMax, n.RemapFromMin, n.RemapToMax, n.RemapToMin, n.RidgeErosion, n.SharpToRoundFeatures, n.SlopeBias,
                n.SlopeErosion, n.SlopeGain),
            (int)n.DebugNoiseType, n.Octaves, l.Height, l.HeightOffset, l.MaximumLOD, (int)l.Offset.OffsetType, l.PlateauRegionSize,
            l.PlateauSharpness, l.PlateauStratas, l.RegionGain, l.RegionRatio, l.RegionScale, l.SeedOffset, l.SmoothRadius,
            l.TileBlendMeters, (int)l.VoxelType.NoiseVoxelType, (int)l.WaterFade, l.Width, l.Active, l.Subtract);
    }
}
