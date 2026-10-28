using System.Numerics;
using System.Text.Json;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;

/// <summary>"sites table.mbin planet_buildings.json ua:seed:level:corrupt:relic:scrap:sites.json ...": checks
/// ArtifactX.Core's PlanetBuildingSites against the running game's data - Voronoi seeds and divisions, the slot table
/// (dump_planet_buildings.py), and every genuine live site record (buildingcheck_live.py with SITES_OUT): key, point
/// and built class - plus 200,000 random cells' class against the live slot table.</summary>
static class Sites
{
    public static void Check(string tableFile, string planetsFile, IEnumerable<string> planets)
    {
        var mbin = new MBINFile(File.OpenRead(tableFile));
        mbin.Load();
        var table = ((GcBuildingDefinitionTable)mbin.GetData()).BuildingPlacement.Select(d => (BuildingClassPlacement?)new BuildingClassPlacement(
            d.Density, d.PlanetRestrictions.RequiresCorruptSentinels, d.PlanetRestrictions.RequiresRelicWorld,
            d.PlanetRestrictions.RequiresScrapWorld, d.PlanetRestrictions.RequiresWater, d.EnabledWhenPlanetHasNoNPCs)).ToList();
        var live = JsonDocument.Parse(File.ReadAllText(planetsFile)).RootElement.EnumerateArray()
            .ToDictionary(p => p.GetProperty("ua").GetString()!);
        foreach (string arg in planets)
        {
            var f = arg.Split(':', 7);
            var planet = live[f[0]];
            ulong seed = Convert.ToUInt64(f[1], 16);
            var facts = new BuildingPlanetFacts(int.Parse(f[2]), f[3] == "1", f[4] == "1", f[5] == "1", false);
            var sites = JsonDocument.Parse(File.ReadAllText(f[6])).RootElement;
            float radius = sites.GetProperty("radius").GetSingle();
            var (point, sector) = PlanetBuildingSites.VoronoiSeeds(seed);
            int divisions = PlanetBuildingSites.Divisions(radius);
            Console.WriteLine($"{f[0]}: seeds {point}/{sector} live {planet.GetProperty("point_seed").GetInt32()}/{planet.GetProperty("sector_seed").GetInt32()}, " +
                $"divisions {divisions} live {sites.GetProperty("divisions").GetInt32()}");

            var types = planet.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("classification").GetInt32()).ToArray();
            var liveSlots = planet.GetProperty("slots").EnumerateArray().Select(s =>
            {
                int index = s[0].GetInt32(); float p = s[1].GetSingle(); bool has = s[2].GetBoolean();
                return has && index >= 0 && index < types.Length ? (types[index], p) : (-1, 1f);
            }).ToArray();
            var ours = PlanetBuildingSites.Slots(table, facts);
            int sameSlots = ours.Zip(liveSlots).Count(z => z.First.Class == z.Second.Item1 && (z.First.Class < 0 || z.First.Probability == z.Second.Item2));

            int records = 0, keys = 0, built = 0, classes = 0;
            float worst = 0;
            foreach (var r in sites.GetProperty("records").EnumerateArray())
            {
                records++;
                int face = r.GetProperty("face").GetInt32();
                var cell = r.GetProperty("cell").EnumerateArray().Select(c => c.GetInt32()).ToArray();
                var (u, v) = face switch { 0 or 1 => (cell[0], cell[1]), 2 or 3 => (cell[1], cell[2]), _ => (cell[0], cell[2]) };
                var site = PlanetBuildingSites.Site(face, u, v, divisions, radius, sector, ours);
                var p = r.GetProperty("point").EnumerateArray().Select(c => c.GetSingle()).ToArray();
                worst = Math.Max(worst, Vector3.Distance(site.Point, new Vector3(p[0], p[1], p[2])));
                if (site.Key == Convert.ToUInt64(r.GetProperty("key").GetString(), 16)) keys++;
                if (site.Class >= 0) built++;
                if (site.Class == PlanetBuildingSites.ClassAt(site.Key, liveSlots)) classes++;
            }
            var rnd = new Random(1);
            int agree = 0, n = 200000;
            for (int i = 0; i < n; i++)
            {
                int half = divisions / 2;
                var site = PlanetBuildingSites.Site(rnd.Next(6), rnd.Next(-half, half + 1), rnd.Next(-half, half + 1), divisions, radius, sector, ours);
                if (site.Class == PlanetBuildingSites.ClassAt(site.Key, liveSlots)) agree++;
            }
            Console.WriteLine($"   slots {sameSlots}/1000; records {records}: keys {keys}, predicted built {built}, class as live {classes}, " +
                $"worst point error {worst:0.000} m; random cells class as live {agree}/{n}");
        }
    }
}
