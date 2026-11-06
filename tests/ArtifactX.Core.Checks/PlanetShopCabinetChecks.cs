using ArtifactX.Core.Universe;

internal static class PlanetShopCabinetChecks
{
    public static void Check()
    {
        const ulong kaimSeed = 0x07E3F9990696DCC9UL;
        var recordSeeds = PlanetBuildingSites.RecordSeeds(kaimSeed, 60);
        if (recordSeeds[0] != 0x1DD2F3ACC9DFF362UL || recordSeeds[4] != 0x8D9B9D8A2623562FUL || recordSeeds[8] != 0xC9BC0BFCE72F16EFUL)
            throw new InvalidOperationException($"Kaim 30/I9 record seeds differ: [0]=0x{recordSeeds[0]:X16}, [4]=0x{recordSeeds[4]:X16}, [8]=0x{recordSeeds[8]:X16}");

        // SHELTER_WAYPOIN cluster fixture (from GcBuildingDefinitionTable and GcBuildingGlobals).
        const int shelter = 2, shop = 5, waypoint = 7;
        int[] types = [shelter, shelter, shelter, shelter, -1, -1, -1, -1, shop, shop, shop, -1, waypoint];
        BuildingClusterEntry[] shelterWaypoint = [new(waypoint, 1, 1, 1f), new(shelter, 1, 1, 1f)];
        const float chanceOfAddingShelter = 0.75f;
        var radiusByRecord = new Dictionary<int, double>
        {
            [0] = 21.379,
            [1] = 23.329,
            [2] = 21.255,
            [3] = 21.379,
            [12] = 10.0,
        };

        var liveClusters = new (ulong Key, (double X, double Y, double Z) Site, int[] WantMembers, (double X, double Y, double Z)[] WantPlaced)[]
        {
            (0x8924A2E5C39F854DUL, (116591.492, -15370.817, 55521.562), [12],
                [(116587.938, -15430.443, 55512.520)]),
            (0x66386EC5762B09FBUL, (114312.875, -17583.473, 59463.180), [12, 2],
                [(114308.336, -17643.055, 59454.285), (114323.273, -17643.504, 59425.438)]),
            (0x627968CDF4A27660UL, (117627.625, -9044.394, 54721.324), [12, 3],
                [(117627.250, -9104.119, 54712.254), (117641.062, -9103.396, 54682.695)]),
        };

        foreach (var (key, site, wantMembers, wantPlaced) in liveClusters)
        {
            int[] chosen = BuildingClusterPlacement.ChooseMembers(key, shelterWaypoint, types, chanceOfAddingShelter);
            if (!chosen.SequenceEqual(wantMembers))
                throw new InvalidOperationException($"Cluster members for 0x{key:X16} differ: [{string.Join(", ", chosen)}]");
            double[] radii = [50.412, .. chosen.Select(idx => radiusByRecord[idx])];
            var placed = BuildingClusterPlacement.PlaceMembers(site, radii);
            for (int i = 0; i < wantPlaced.Length; i++)
            {
                double err = Math.Sqrt(
                    Math.Pow(placed[i].X - wantPlaced[i].X, 2) +
                    Math.Pow(placed[i].Y - wantPlaced[i].Y, 2) +
                    Math.Pow(placed[i].Z - wantPlaced[i].Z, 2));
                if (err > 0.005)
                    throw new InvalidOperationException($"Cluster member {i} position error {err:F4} m for 0x{key:X16}.");
            }
        }

        var site1 = liveClusters[0].Site;
        double[] radii1 = [50.412, 10.0];
        var placed1 = BuildingClusterPlacement.PlaceMembers(site1, radii1);
        var recentred1 = BuildingClusterPlacement.Recentre(site1, [site1, .. placed1], radii1)[0];
        if (Math.Abs(recentred1.X - site1.X - 0.59) > 0.02
            || Math.Abs(recentred1.Y - site1.Y - 9.87) > 0.02
            || Math.Abs(recentred1.Z - site1.Z - 1.50) > 0.02)
            throw new InvalidOperationException("Shop 1 re-centring shift differs from live.");

        foreach (var (key, pos, wantFwd) in new (ulong, (double, double, double), (double X, double Y, double Z))[]
        {
            (0x8924A2E5C39F854DUL, (116591.49, -15370.817, 55521.562), (0.180735, 0.977482, -0.108921)),
            (0x66386EC5762B09FBUL, (114312.875, -17583.473, 59463.184), (-0.466306, -0.443959, 0.765153)),
            (0x627968CDF4A27660UL, (117627.62, -9044.393, 54721.32), (0.136328, 0.981998, -0.130742)),
        })
        {
            var (_, fwd, _) = BuildingClusterPlacement.Facing(pos, key);
            double err = Math.Sqrt(Math.Pow(fwd.X - wantFwd.X, 2) + Math.Pow(fwd.Y - wantFwd.Y, 2) + Math.Pow(fwd.Z - wantFwd.Z, 2));
            if (err >= 1e-4) throw new InvalidOperationException($"Facing error {err:E2} for key 0x{key:X16}.");
        }
        Console.WriteLine("PASS: planet building record seeds, cluster member selection/placement/re-centring and radian facing match live Kaim 30/I9 data.");
    }
}
