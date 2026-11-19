using System.Diagnostics;
using ArtifactX.Core.Universe;
using ArtifactX.GameData.Services;

/// <summary>
/// CrashedShipSites.OfClass (the phone's fast search) against the full pass CrashedShipSites.All filtered by class:
/// the same wrecks on every planet of a spread of systems, and how much faster it is. Tables from the phone's rules
/// file (MobileRulesExportService), so this also covers the phone's data.
/// </summary>
static class CrashedShipClassChecks
{
    public static void Run(string catalogPath)
    {
        var rules = MobileRulesExportService.Read(catalogPath);
        var random = new Random(185034);
        var systems = new List<ulong> { 0x3EFDAFBFFB004 };
        for (int i = 0; i < 12; i++)
        {
            ulong galaxy = (ulong)(i % 3 == 2 ? 218 : i % 2);
            systems.Add((galaxy << 32) | ((ulong)(uint)random.Next(0, 0x1000) << 40) | ((ulong)(uint)random.Next(0, 0x100) << 24)
                | ((ulong)(uint)random.Next(0, 0x1000) << 12) | (uint)random.Next(1, 0x300));
        }
        int planets = 0, failures = 0, sClass = 0, aClass = 0;
        var full = new Stopwatch();
        var fast = new Stopwatch();
        var prepare = new Stopwatch();
        foreach (ulong system in systems)
        {
            var centres = PortalPositions.Centres(system, rules.Stations!.SolarLayout);
            if (centres is null) continue;
            for (int index = 1; index <= centres.Length; index++)
            {
                ulong planet = system | ((ulong)index << 52);
                prepare.Start();
                var context = CrashedShipSites.Prepare(planet, centres[index - 1], rules.Biomes!, rules.Profiles!, rules.BuildingPlacement!,
                    rules.BuildingOverrides, rules.Stations.SolarLayout, rules.Words, rules.Inventory, null);
                prepare.Stop();
                if (context is null) continue;
                planets++;
                full.Start();
                var all = CrashedShipSites.All(context);
                full.Stop();
                foreach (int min in new[] { 3, 2 })
                {
                    fast.Start();
                    var quick = CrashedShipSites.OfClass(context, min);
                    fast.Stop();
                    var expected = all.Where(s => s.ShownClass >= min).Select(s => s.Key).Order().ToArray();
                    if (!quick.Select(s => s.Key).Order().SequenceEqual(expected))
                    {
                        failures++;
                        Console.WriteLine($"FAIL: {planet:X} class {min}+: {quick.Count} vs {expected.Length}");
                    }
                    if (min == 3) sClass += quick.Count; else aClass += quick.Count;
                }
            }
        }
        Console.WriteLine($"{planets} planets: {sClass} S-class, {aClass} A-or-better wrecks. Prepare {prepare.ElapsedMilliseconds / Math.Max(planets, 1)} ms, "
            + $"full pass {full.ElapsedMilliseconds / Math.Max(planets, 1)} ms, class search {fast.ElapsedMilliseconds / Math.Max(planets * 2, 1)} ms per planet.");
        Console.WriteLine(failures == 0 ? "PASS: the class search finds exactly the full pass's wrecks." : $"{failures} failures.");
        if (failures > 0) Environment.ExitCode = 1;
    }

    /// <summary>Every planet of every system in the region of a portal code (galaxy 1-based) through the phone finder's
    /// steps (PortalPositions, CrashedShipSites.Prepare and OfClass), reporting any that throws.</summary>
    public static void Region(string catalogPath, string portalCode, int galaxy)
    {
        var rules = MobileRulesExportService.Read(catalogPath);
        ulong start = ArtifactX.Core.NmsModels.NmsPortalCode.ToAddress(portalCode, galaxy) & ~(0xFUL << 52);
        var systems = NearbySystems.Around(start, 0, rules.GalaxySettings!).Select(s => s.SystemAddress & ~(0xFUL << 52)).Distinct().ToList();
        int failures = 0, planets = 0;
        foreach (ulong system in systems)
        {
            try
            {
                var centres = PortalPositions.Centres(system, rules.Stations!.SolarLayout);
                if (centres is null) continue;
                var portals = PortalPositions.ForSystem(system, rules.Biomes!, rules.Profiles!, rules.Stations, rules.BuildingOverrides!);
                for (int index = 1; index <= centres.Length; index++)
                {
                    var context = CrashedShipSites.Prepare(system | ((ulong)index << 52), centres[index - 1], rules.Biomes!, rules.Profiles!,
                        rules.BuildingPlacement!, rules.BuildingOverrides, rules.Stations.SolarLayout, rules.Words, rules.Inventory, null);
                    if (context is null) continue;
                    planets++;
                    CrashedShipSites.OfClass(context, 3);
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL: {system:X}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}");
            }
        }
        Console.WriteLine(failures == 0 ? $"PASS: {systems.Count} systems, {planets} planets, no errors." : $"{failures} of {systems.Count} systems failed.");
        if (failures > 0) Environment.ExitCode = 1;
    }
}
