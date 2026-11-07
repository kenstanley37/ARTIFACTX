using System.Text.Json;
using ArtifactX.Core.Universe;

/// <summary>"places address biometables.json planetprofile.json stationequipment.json out.json": everything the
/// override placement (placeport.py) needs for each planet of a system, from Core alone - planet seed, type, terrain
/// choice (setting, draws before the layers, sea, biome file chances), radius, Voronoi seeds and the solar centre - so
/// it can run with the game closed. Tables are the catalog's PlanetGeneration rows exported as JSON.</summary>
static class Places
{
    public static void Write(string address, string biomeFile, string profileFile, string stationFile, string outFile)
    {
        ulong ua = Convert.ToUInt64(address.Replace("0x", ""), 16);
        int galaxy = (int)((ua >> 32) & 0xFF);
        ulong portal = (((ua >> 40) & 0xFFF) << 32) | (ua & 0xFFFFFFFF);
        var biomes = JsonSerializer.Deserialize<BiomeTables>(File.ReadAllText(biomeFile))!;
        var profiles = JsonSerializer.Deserialize<PlanetProfileTables>(File.ReadAllText(profileFile))!;
        var stations = JsonSerializer.Deserialize<StationEquipmentTables>(File.ReadAllText(stationFile))!;
        var types = PlanetGenerator.Generate(portal, galaxy, biomes);
        var attributes = SystemGenerator.Attributes(portal, galaxy);
        var seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
        var layout = SolarLayoutGeneration.ForSystem(portal, galaxy, stations.SolarLayout, true);
        var planets = new List<object>();
        for (int i = 0; i < Math.Min(types.Length, seeds.Length); i++)
        {
            var body = types[i];
            var terrain = PlanetProfileGenerator.TerrainChoiceFor(seeds[i], body, (int)attributes.Star, profiles);
            float radius = PlanetBuildingSites.Radius(body.BodyClass) ?? 0f;
            var (pointSeed, sectorSeed) = PlanetBuildingSites.VoronoiSeeds(seeds[i]);
            var centre = layout is not null && i < layout.Centers.Length ? layout.Centers[i] : (System.Numerics.Vector3?)null;
            planets.Add(new
            {
                ua = $"{((ulong)(i + 1) << 52) | (ua & ~(0xFUL << 52)):X16}", seed = $"{seeds[i]:X16}", biome = body.Biome, subType = body.SubType,
                planetClass = body.Class, size = body.BodyClass, prime = body.Prime, deadToWeird = body.DeadToWeird,
                setting = terrain?.SettingIndex, drawsBeforeLayers = terrain?.DrawsBeforeLayers, sea = terrain?.Sea,
                noiseChances = terrain?.BiomeFile.NoiseLayers, rock = terrain?.BiomeFile.RockTile, substance = terrain?.BiomeFile.SubstanceTile,
                seaStandard = profiles.Objects?.Terrain.SeaStandard,
                radius, pointSeed, sectorSeed, divisions = PlanetBuildingSites.Divisions(radius),
                centre = centre is { } c ? new[] { c.X, c.Y, c.Z } : null,
            });
        }
        File.WriteAllText(outFile, JsonSerializer.Serialize(planets, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{planets.Count} planets written to {outFile}");
    }
}
