using System.Globalization;
using System.Text.Json;
using ArtifactX.Core.Universe;
using ArtifactX.GameData;
using ArtifactX.GameData.Data;

/// <summary>
/// BuildingLayoutGenerator against L-system samples the running game stored (scripts/diagnostics/read_lsystem_samples.py,
/// read-only), through a scratch catalog built from the installed game: node models in order, positions and rotations
/// in the root's frame, radius (half the live bounds diagonal, at least MinRadius) and the re-centring root offset.
/// The samples file is the player's own local read and stays out of the repository.
/// </summary>
internal static class BuildingLayoutChecks
{
    public static void Live(string pcbanks, string scratchCatalog, string samplesPath)
    {
        string output = Path.GetFullPath(scratchCatalog);
        if (File.Exists(output)) throw new InvalidOperationException("The check requires a new scratch catalog.");
        using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
        CatalogSteps.AddPlanetGeneration(output, pcbanks);
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
        connection.Open();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='BUILDINGLAYOUTS'";
        string json = (string?)query.ExecuteScalar() ?? throw new InvalidOperationException("No BUILDINGLAYOUTS row.");
        var tables = JsonSerializer.Deserialize<BuildingLayoutTables>(json)!;
        Console.WriteLine($"BUILDINGLAYOUTS: {tables.LSystems.Count} L-systems, {tables.Scenes.Count} scenes, {json.Length / 1024} KB");

        using var samples = JsonDocument.Parse(File.ReadAllText(samplesPath));
        int total = 0, layouts = 0, radii = 0;
        foreach (var ls in samples.RootElement.EnumerateArray())
        {
            string path = ls.GetProperty("path").GetString()!;
            var diag = ls.GetProperty("diag").EnumerateArray().Select(d => d.GetDouble()).ToArray();
            int k = 0;
            foreach (var s in ls.GetProperty("samples").EnumerateArray())
            {
                total++;
                ulong seed = ulong.Parse(s.GetProperty("seed").GetString()!, NumberStyles.HexNumber);
                var layout = BuildingLayoutGenerator.Generate(tables, path, seed)
                    ?? throw new InvalidOperationException($"{path}: no layout");
                var want = s.GetProperty("nodes").EnumerateArray().ToArray();
                bool names = layout.Nodes.Length == want.Length && layout.Nodes.Zip(want).All(p =>
                    p.First.Model == p.Second.GetProperty("model").GetString()!.Replace('\\', '/').ToUpperInvariant());
                double pos = 0, rot = 0;
                foreach (var (node, live) in layout.Nodes.Zip(want).Skip(1))
                {
                    var q = live.GetProperty("m").EnumerateArray().Select(v => v.GetDouble()).ToArray();   // rows: X, Y, Z axes, position
                    for (int i = 0; i < 3; i++)
                    {
                        pos = Math.Max(pos, Math.Abs(node.World[9 + i] - q[12 + i]));
                        for (int j = 0; j < 3; j++) rot = Math.Max(rot, Math.Abs(node.World[i * 3 + j] - q[j * 4 + i]));
                    }
                }
                double liveRadius = Math.Max(diag[k] / 2, tables.MinRadius);
                var root = want[0].GetProperty("m").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                double offset = Math.Max(Math.Abs(layout.RootOffset.X - root[12]), Math.Abs(layout.RootOffset.Z - root[14]));
                bool ok = names && pos < 1e-3 && rot < 1e-4;
                layouts += ok ? 1 : 0;
                radii += Math.Abs(layout.Radius - liveRadius) < 5e-4 ? 1 : 0;
                Console.WriteLine($"{path.Split('/')[^1],-44} {k}: layout {(ok ? "OK  " : "DIFF")} {layout.Nodes.Length}/{want.Length} nodes " +
                    $"(pos {pos:F4}, rot {rot:E1}); radius {layout.Radius,8:F3} live {liveRadius,8:F3}; root offset error {offset:F3}");
                k++;
            }
        }
        Console.WriteLine($"{layouts}/{total} layouts, {radii}/{total} radii exact");
        if (layouts != total) throw new InvalidOperationException("Layouts differ from the live samples.");

        CheckShopCabinetPipeline(connection, tables, samples.RootElement);
        Console.WriteLine("PASS");
    }

    private static void CheckShopCabinetPipeline(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        BuildingLayoutTables tables,
        JsonElement liveSamples)
    {
        // Step 2: Record seeds against Kaim 30/I9's 8 live Vy'keen L-system records.
        const ulong kaimSeed = 0x07E3F9990696DCC9UL;
        var recordSeeds = PlanetBuildingSites.RecordSeeds(kaimSeed, 60);
        int[] liveRecordIndices = [0, 4, 8, 14, 17, 21, 24, 59];
        int lsIndex = 0;
        foreach (var ls in liveSamples.EnumerateArray())
        {
            ulong recSeed = recordSeeds[liveRecordIndices[lsIndex++]];
            int sampleIdx = 0;
            foreach (var s in ls.GetProperty("samples").EnumerateArray())
            {
                ulong wantSeed = ulong.Parse(s.GetProperty("seed").GetString()!, NumberStyles.HexNumber);
                ulong gotSeed = BuildingLayoutGenerator.SampleSeed(recSeed, sampleIdx++);
                if (gotSeed != wantSeed)
                    throw new InvalidOperationException($"Record seed mismatch at L-system {lsIndex - 1} sample {sampleIdx - 1}: got 0x{gotSeed:X16}, want 0x{wantSeed:X16}");
            }
        }
        Console.WriteLine($"Record seeds: 8/8 Vy'keen L-systems (24/24 sample seeds) match live.");

        // Step 3 & 4: Cluster layout from catalog, radii from Step 1, placement, re-centring, facing, and Step 5 crate point.
        if (Math.Abs(tables.ChanceOfAddingShelter - 0.75f) > 1e-6f)
            throw new InvalidOperationException($"ChanceOfAddingShelter {tables.ChanceOfAddingShelter} != 0.75");
        var shopCluster = BuildingClusterPlacement.ClusterForClass(tables, 5)
            ?? throw new InvalidOperationException("Shop cluster layout missing from BUILDINGLAYOUTS.");
        string shopPath = BuildingLayoutGenerator.LSystemPath(tables, race: 1, buildingClass: 5)
            ?? throw new InvalidOperationException("Vy'keen Shop L-system path missing from BUILDINGLAYOUTS.");
        string shelterPath = BuildingLayoutGenerator.LSystemPath(tables, race: 1, buildingClass: 2)
            ?? throw new InvalidOperationException("Vy'keen Shelter L-system path missing from BUILDINGLAYOUTS.");

        var shopLayout = BuildingLayoutGenerator.Generate(tables, shopPath, BuildingLayoutGenerator.SampleSeed(recordSeeds[8], 0))
            ?? throw new InvalidOperationException("Shop sample 0 layout failed.");
        if (shopLayout.WeaponCrateRoot is not var (crx, cry, crz)
            || Math.Abs(crx - 3.974) > 2e-3 || Math.Abs(cry - 3.734) > 2e-3 || Math.Abs(crz - (-5.000)) > 2e-3)
            throw new InvalidOperationException($"Shop WeaponCrateRoot differs: {shopLayout.WeaponCrateRoot}");
        if (shopLayout.WeaponCrate is not var (cx, cy, cz)
            || Math.Abs(cx - (-2.374)) > 2e-3 || Math.Abs(cy - 3.734) > 2e-3 || Math.Abs(cz - 8.389) > 2e-3)
            throw new InvalidOperationException($"Shop WeaponCrate differs: {shopLayout.WeaponCrate}");

        const int shelter = 2, shop = 5, waypoint = 7;
        int[] recordClasses = [shelter, shelter, shelter, shelter, -1, -1, -1, -1, shop, shop, shop, -1, waypoint];
        var recordRadii = new Dictionary<int, double> { [12] = tables.MinRadius };
        for (int n = 0; n < 4; n++)
            recordRadii[n] = BuildingLayoutGenerator.Generate(tables, shelterPath, BuildingLayoutGenerator.SampleSeed(recordSeeds[0], n))!.Radius;

        (double X, double Y, double Z) site1 = (116591.492, -15370.817, 55521.562);
        var chosen1 = BuildingClusterPlacement.ChooseMembers(0x8924A2E5C39F854DUL, shopCluster, recordClasses, tables.ChanceOfAddingShelter);
        double[] radii1 = [shopLayout.Radius, .. chosen1.Select(idx => recordRadii[idx])];
        var placed1 = BuildingClusterPlacement.PlaceMembers(site1, radii1);
        var recentred1 = BuildingClusterPlacement.Recentre(site1, [site1, .. placed1], radii1)[0];
        if (Math.Abs(recentred1.X - site1.X - 0.59) > 0.02
            || Math.Abs(recentred1.Y - site1.Y - 9.87) > 0.02
            || Math.Abs(recentred1.Z - site1.Z - 1.50) > 0.02)
            throw new InvalidOperationException($"Shop 1 re-centred shift differs from live: {recentred1}");

        // Verify Trader (race 0), Warrior (race 1) and Explorer (race 2) Shop L-systems all resolve a WeaponCrate.
        foreach (int race in new[] { 0, 1, 2 })
        {
            string raceShopPath = BuildingLayoutGenerator.LSystemPath(tables, race, buildingClass: 5)
                ?? throw new InvalidOperationException($"Race {race} Shop L-system path missing from BUILDINGLAYOUTS.");
            var raceShopLayout = BuildingLayoutGenerator.Generate(tables, raceShopPath, BuildingLayoutGenerator.SampleSeed(recordSeeds[8], 0))
                ?? throw new InvalidOperationException($"Race {race} Shop layout failed.");
            if (raceShopLayout.WeaponCrate is null)
                throw new InvalidOperationException($"Race {race} ({raceShopPath}) Shop layout did not resolve a WeaponCrate.");
        }

        // Step 5 & 6: PreparePlanet + ForShopSite on Kaim 30/I9 (cube face +X).
        T ReadRow<T>(string id)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            cmd.Parameters.AddWithValue("$id", id);
            return JsonSerializer.Deserialize<T>((string)cmd.ExecuteScalar()!)!;
        }
        var biomes = ReadRow<BiomeTables>("BIOMETABLES");
        var profiles = ReadRow<PlanetProfileTables>("PLANETPROFILE");
        var placement = ReadRow<BuildingClassPlacement[]>("BUILDINGPLACEMENT");
        var station = ReadRow<StationEquipmentTables>("STATIONEQUIPMENT");
        const ulong address = 0x1FA00016452DBUL;
        const ulong code = (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF);
        const int galaxy = (int)((address >> 32) & 0xFF);
        const int planetIndex = 0;

        var context = PlanetShopCabinetGenerator.PreparePlanet(
            code, galaxy, planetIndex, biomes, profiles, placement, tables, station.SolarLayout!)
            ?? throw new InvalidOperationException("Failed to prepare Kaim 30/I9 shop cabinet context.");
        int[] expectedFirstRecords = [2, 3, 5, 9, 10, 12, 13, 31];
        int[] actualFirstIndices = expectedFirstRecords.Select(c => Array.IndexOf(context.RecordClasses.ToArray(), c)).ToArray();
        if (!actualFirstIndices.SequenceEqual(liveRecordIndices))
            throw new InvalidOperationException($"Generated first-record indices [{string.Join(", ", actualFirstIndices)}] != live [{string.Join(", ", liveRecordIndices)}]");
        if (context.PlanetCentre != (-224000.0, 0.0, -134000.0))
            throw new InvalidOperationException($"Generated planet centre {context.PlanetCentre} != (-224000, 0, -134000)");
        Console.WriteLine($"RecordClasses: {context.RecordClasses.Count} records on Kaim 30/I9; first-record indices [{string.Join(", ", actualFirstIndices)}] match all 8 live L-systems.");

        var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
        var types = PlanetGenerator.Generate(code, galaxy, biomes);
        var facts = PlanetBuildingSites.FactsFor(code, galaxy, planetIndex, biomes, profiles)!;
        var (_, sectorSeed) = PlanetBuildingSites.VoronoiSeeds(seeds[planetIndex]);
        int divisions = PlanetBuildingSites.Divisions(context.PlanetRadius);
        var slots = PlanetBuildingSites.Slots(placement, facts);
        double latRad = 39.73 * Math.PI / 180.0, lonRad = 92.10 * Math.PI / 180.0;
        var dir = new System.Numerics.Vector3(
            (float)(Math.Cos(latRad) * Math.Sin(lonRad)),
            (float)Math.Sin(latRad),
            (float)(Math.Cos(latRad) * Math.Cos(lonRad)));
        var portalSites = PlanetBuildingSites.SitesNear(dir, divisions, context.PlanetRadius, sectorSeed, slots, cls => cls == 5, 20)
            .Where(s => PlanetBuildingSites.GroundAllows(context.Terrain, s.Point, context.PlanetRadius, types[planetIndex].Prime, 40f, false, types[planetIndex].Biome == 14))
            .ToDictionary(s => s.Key, s => ((double)s.Point.X, (double)s.Point.Y, (double)s.Point.Z));

        var shops = new (ulong Key, (double X, double Y, double Z) Pos, double? LiveSpawnHeight, (int X, int Y, int Z) WantCabinet, bool NearEdge)[]
        {
            (0x404DA563E9C5EC88UL, (99954.344, 83115.625, -3661.159), 269.828, (-123846, 83303, -137661), false),
            (0x8924A2E5C39F854DUL, (116591.49, -15370.817, 55521.562), null, (-107157, -15386, -78362), true),
            (0x66386EC5762B09FBUL, (114312.875, -17583.473, 59463.184), null, (-109470, -17596, -74402), false),
            (0x627968CDF4A27660UL, (117627.62, -9044.393, 54721.32), null, (-106129, -9033, -79157), false),
            (0x741BF5DCCD41B78AUL, portalSites[0x741BF5DCCD41B78AUL], null, (-124034, 83512, -138752), false),
            (0x45B9A979D3989991UL, portalSites[0x45B9A979D3989991UL], null, (-121547, 80353, -139241), false),
            (0x31CCA8AE9D607F54UL, portalSites[0x31CCA8AE9D607F54UL], null, (-120898, 79330, -140789), false),
            (0xD2D981D73443CF98UL, portalSites[0xD2D981D73443CF98UL], null, (-123713, 83045, -131786), false),
        };

        int exactCabinets = 0;
        foreach (var s in shops)
        {
            ulong wantSeed = MultiToolSiteGeneration.SeedForPosition(new System.Numerics.Vector3(s.WantCabinet.X, s.WantCabinet.Y, s.WantCabinet.Z));
            var cab = PlanetShopCabinetGenerator.ForShopSite(context, s.Key, s.Pos)
                ?? throw new InvalidOperationException($"No cabinet for shop 0x{s.Key:X16}");
            if (s.LiveSpawnHeight is { } liveSpawn && Math.Abs(cab.SpawnHeight - liveSpawn) > 0.03)
                throw new InvalidOperationException($"Shop 0x{s.Key:X16} spawn height error {Math.Abs(cab.SpawnHeight - liveSpawn):F4} m > 0.03 m (got {cab.SpawnHeight:F3}, live {liveSpawn:F3})");

            bool exact = cab.CabinetPoint == s.WantCabinet && cab.CabinetSeed == wantSeed;
            if (exact) exactCabinets++;
            else if (!s.NearEdge || cab.AlternateNearEdge is not { } alt || alt.Point != s.WantCabinet || alt.Seed != wantSeed)
                throw new InvalidOperationException($"Shop 0x{s.Key:X16} cabinet {cab.CabinetPoint} (0x{cab.CabinetSeed:X16}) != {s.WantCabinet} (0x{wantSeed:X16})");

            Console.WriteLine($"Shop 0x{s.Key:X16}: H(A) {cab.SiteHeight:F3} spawn {cab.SpawnHeight:F3}; cabinet {cab.CabinetPoint} seed 0x{cab.CabinetSeed:X16}" +
                (cab.AlternateNearEdge is { } a ? $" (near-edge alt {a.Point} -> 0x{a.Seed:X16})" : ""));
        }
        if (exactCabinets != 7)
            throw new InvalidOperationException($"Expected 7/8 exact cabinets and 1 near-edge, got {exactCabinets}/8 exact.");

        // Eolul Sigma (Moon 2 of Ihweyf, planetIndex = 1, cube face -Y): verified in game 2026-11-06 at (-66.19, -164.07)
        // -> cabinet (-369690, 98921, -7965) seed 0xF846F1CB23520AA9 (Improvised Molecule Focuser, A, 333.3 dmg / 452.4 scan).
        var moonContext = PlanetShopCabinetGenerator.PreparePlanet(
            code, galaxy, 1, biomes, profiles, placement, tables, station.SolarLayout!)
            ?? throw new InvalidOperationException("Failed to prepare Eolul Sigma shop cabinet context.");
        var moonFacts = PlanetBuildingSites.FactsFor(code, galaxy, 1, biomes, profiles)!;
        var (_, moonSectorSeed) = PlanetBuildingSites.VoronoiSeeds(seeds[1]);
        int moonDivisions = PlanetBuildingSites.Divisions(moonContext.PlanetRadius);
        var moonSlots = PlanetBuildingSites.Slots(placement, moonFacts);
        (ulong Key, double Lat, double Lon, (int X, int Y, int Z) WantCabinet)[] moonShops =
        [
            (0x2CA7C9B233C0867DUL, -66.24, -164.17, (-369690, 98921, -7965)),
            (0x30521C5EE7F897F4UL, -52.72, -123.73, (-375751, 100759, -7172)),
        ];
        foreach (var (moonKey, moonLat, moonLon, wantMoonCab) in moonShops)
        {
            double moonLatRad = moonLat * Math.PI / 180.0, moonLonRad = moonLon * Math.PI / 180.0;
            var moonDir = new System.Numerics.Vector3(
                (float)(Math.Cos(moonLatRad) * Math.Sin(moonLonRad)),
                (float)Math.Sin(moonLatRad),
                (float)(Math.Cos(moonLatRad) * Math.Cos(moonLonRad)));
            var moonShopSite = PlanetBuildingSites.SitesNear(moonDir, moonDivisions, moonContext.PlanetRadius, moonSectorSeed, moonSlots, cls => cls == 5, 5)
                .First(s => s.Key == moonKey);
            var moonCab = PlanetShopCabinetGenerator.ForShopSite(moonContext, moonShopSite.Key, (moonShopSite.Point.X, moonShopSite.Point.Y, moonShopSite.Point.Z))
                ?? throw new InvalidOperationException($"No cabinet for Eolul Sigma shop 0x{moonKey:X16}.");
            ulong wantMoonSeed = MultiToolSiteGeneration.SeedForPosition(new System.Numerics.Vector3(wantMoonCab.X, wantMoonCab.Y, wantMoonCab.Z));
            if (moonCab.CabinetPoint != wantMoonCab || moonCab.CabinetSeed != wantMoonSeed)
                throw new InvalidOperationException($"Eolul Sigma shop 0x{moonKey:X16} cabinet {moonCab.CabinetPoint} (0x{moonCab.CabinetSeed:X16}) != {wantMoonCab} (0x{wantMoonSeed:X16})");
            Console.WriteLine($"Moon shop 0x{moonShopSite.Key:X16} (Eolul Sigma, face -Y): H(A) {moonCab.SiteHeight:F3} spawn {moonCab.SpawnHeight:F3}; cabinet {moonCab.CabinetPoint} seed 0x{moonCab.CabinetSeed:X16}");
        }
        Console.WriteLine("Cabinets: 9/10 exact across Kaim 30/I9 (+X) and Eolul Sigma (-Y moon), 10th (0x8924A2E5C39F854D) ~2 cm across rounding edge in X with exact AlternateNearEdge.");
    }
}
