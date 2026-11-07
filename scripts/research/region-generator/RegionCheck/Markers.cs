using System.Text.Json;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;

/// <summary>"markers table.mbin markers.json biometables.json planetprofile.json": checks PlanetBuildingSites against
/// the scan markers saved in real saves (A1f: planet address oZw, BuildingSeed 5bU, class iqv - exported by a short
/// script into markers.json; the two catalog rows exported from nms_catalog.sqlite). For each marker it generates
/// the planet (type, profile, density level, restriction flags), finds the cell whose site key is the marker's
/// seed by searching every cell of every face, and compares the predicted class with the saved one.</summary>
static class Markers
{
    public static void Check(string tableFile, string markersFile, string biomeFile, string profileFile)
    {
        var mbin = new MBINFile(File.OpenRead(tableFile));
        mbin.Load();
        var table = ((GcBuildingDefinitionTable)mbin.GetData()).BuildingPlacement.Select(d => (BuildingClassPlacement?)new BuildingClassPlacement(
            d.Density, d.PlanetRestrictions.RequiresCorruptSentinels, d.PlanetRestrictions.RequiresRelicWorld,
            d.PlanetRestrictions.RequiresScrapWorld, d.PlanetRestrictions.RequiresWater, d.EnabledWhenPlanetHasNoNPCs)).ToList();
        var biomes = JsonSerializer.Deserialize<BiomeTables>(File.ReadAllText(biomeFile))!;
        var profiles = JsonSerializer.Deserialize<PlanetProfileTables>(File.ReadAllText(profileFile))!;
        var names = Enum.GetNames<GcBuildingClassification.BuildingClassEnum>();
        int found = 0, same = 0, total = 0;
        foreach (var m in JsonDocument.Parse(File.ReadAllText(markersFile)).RootElement.EnumerateArray())
        {
            total++;
            ulong ua = m.GetProperty("ua").GetUInt64(), key = m.GetProperty("key").GetUInt64();
            string savedClass = m.GetProperty("cls").GetString()!;
            int planet = (int)((ua >> 52) & 0xF) - 1, galaxy = (int)((ua >> 32) & 0xFF);
            ulong portal = (((ua >> 40) & 0xFFF) << 32) | (ua & 0xFFFFFFFF);
            var types = PlanetGenerator.Generate(portal, galaxy, biomes);
            var attributes = SystemGenerator.Attributes(portal, galaxy);
            var seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
            if (planet < 0 || planet >= types.Length || planet >= seeds.Length) { Console.WriteLine($"{ua:X}: planet {planet + 1} not generated"); continue; }
            var body = types[planet];
            var profile = PlanetProfileGenerator.Generate(seeds[planet], planet, body, (int)attributes.Star,
                PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
            int level = PlanetBuildingSites.DensityLevel(body.Biome, body.SubType, body.Class);
            var facts = new BuildingPlanetFacts(level, profile.Sentinels[Math.Clamp(profiles.Sentinels.CheckDifficulty, 0, 3)] == 3,
                PlanetBuildingSites.IsRelicWorld(body.Biome, body.SubType), profile.ScrapWorld, profile.Water, !attributes.Abandoned);
            float radius = PlanetBuildingSites.Radius(body.BodyClass) ?? 130048f;
            var slots = PlanetBuildingSites.Slots(table, facts);
            var (_, sector) = PlanetBuildingSites.VoronoiSeeds(seeds[planet]);
            int divisions = PlanetBuildingSites.Divisions(radius), half = divisions / 2;
            BuildingSite? hit = null;
            for (int face = 0; face < 6 && hit is null; face++)
                for (int u = -half; u <= half && hit is null; u++)
                    for (int v = -half; v <= half; v++)
                    {
                        var site = PlanetBuildingSites.Site(face, u, v, divisions, radius, sector, slots);
                        if (site.Key == key) { hit = site; break; }
                    }
            string predicted = hit is { } h ? (h.Class >= 0 ? names[h.Class] : "none") : "-";
            if (hit is not null) found++;
            if (predicted == savedClass) same++;
            Console.WriteLine($"{ua:X14} planet {planet + 1} biome {body.Biome} sub {body.SubType} class {body.Class} size {body.BodyClass} " +
                $"level {level}: {m.GetProperty("event").GetString(),-22} saved {savedClass,-16} predicted {predicted,-16}" +
                (hit is { } s ? $" face {s.Face} ({s.U}, {s.V}) {s.Coordinates.Latitude:+0.00;-0.00}, {s.Coordinates.Longitude:+0.00;-0.00}" : " key not found"));
        }
        Console.WriteLine($"{total} markers: key found {found}, class as saved {same}");
    }

    /// <summary>"suggest table.mbin biometables.json planetprofile.json planetUA classes lat lon cx cy cz": the
    /// predicted sites of the given classes (comma-separated names) nearest a latitude/longitude, with the marker
    /// values a save needs (key, and TWn at the base radius around the planet centre cx/cy/cz from the save's aHM).</summary>
    public static void Suggest(string tableFile, string biomeFile, string profileFile, string uaText, string classes,
        double lat, double lon, double cx, double cy, double cz)
    {
        var mbin = new MBINFile(File.OpenRead(tableFile));
        mbin.Load();
        var table = ((GcBuildingDefinitionTable)mbin.GetData()).BuildingPlacement.Select(d => (BuildingClassPlacement?)new BuildingClassPlacement(
            d.Density, d.PlanetRestrictions.RequiresCorruptSentinels, d.PlanetRestrictions.RequiresRelicWorld,
            d.PlanetRestrictions.RequiresScrapWorld, d.PlanetRestrictions.RequiresWater, d.EnabledWhenPlanetHasNoNPCs)).ToList();
        var biomes = JsonSerializer.Deserialize<BiomeTables>(File.ReadAllText(biomeFile))!;
        var profiles = JsonSerializer.Deserialize<PlanetProfileTables>(File.ReadAllText(profileFile))!;
        var names = Enum.GetNames<GcBuildingClassification.BuildingClassEnum>();
        var wanted = classes.Split(',').Select(c => Array.IndexOf(names, c)).ToHashSet();
        ulong ua = Convert.ToUInt64(uaText, 16);
        int planet = (int)((ua >> 52) & 0xF) - 1, galaxy = (int)((ua >> 32) & 0xFF);
        ulong portal = (((ua >> 40) & 0xFFF) << 32) | (ua & 0xFFFFFFFF);
        var body = PlanetGenerator.Generate(portal, galaxy, biomes)[planet];
        var attributes = SystemGenerator.Attributes(portal, galaxy);
        ulong seed = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds[planet];
        var profile = PlanetProfileGenerator.Generate(seed, planet, body, (int)attributes.Star,
            PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
        int level = PlanetBuildingSites.DensityLevel(body.Biome, body.SubType, body.Class);
        var facts = new BuildingPlanetFacts(level, profile.Sentinels[Math.Clamp(profiles.Sentinels.CheckDifficulty, 0, 3)] == 3,
            PlanetBuildingSites.IsRelicWorld(body.Biome, body.SubType), profile.ScrapWorld, profile.Water, !attributes.Abandoned);
        float radius = PlanetBuildingSites.Radius(body.BodyClass) ?? 130048f;
        var slots = PlanetBuildingSites.Slots(table, facts);
        var (_, sector) = PlanetBuildingSites.VoronoiSeeds(seed);
        int divisions = PlanetBuildingSites.Divisions(radius);
        Console.WriteLine($"planet {planet + 1} biome {body.Biome} sub {body.SubType} class {body.Class} size {body.BodyClass} radius {radius} level {level} {facts}");
        double la = lat * Math.PI / 180, lo = lon * Math.PI / 180;
        var target = new System.Numerics.Vector3((float)(Math.Cos(la) * Math.Sin(lo)), (float)Math.Sin(la), (float)(Math.Cos(la) * Math.Cos(lo)));
        var found = Enumerable.Range(0, 6).SelectMany(f => PlanetBuildingSites.Sites(f, divisions, radius, sector, slots, wanted.Contains))
            .OrderBy(s => System.Numerics.Vector3.Distance(System.Numerics.Vector3.Normalize(s.Point), target)).Take(8);
        foreach (var s in found)
            Console.WriteLine($"  {names[s.Class],-14} {s.Coordinates.Latitude:+0.00;-0.00}, {s.Coordinates.Longitude:+0.00;-0.00}  key 0x{s.Key:X16}" +
                $"  TWn [{cx + s.Point.X:0.###}, {cy + s.Point.Y:0.###}, {cz + s.Point.Z:0.###}]");
    }
}
