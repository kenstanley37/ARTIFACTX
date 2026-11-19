using System.Collections.Concurrent;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.Services;

/// <summary>A crashed ship the finder found: the ship, where it lies and how far from the planet's portal.</summary>
public sealed record ShipFind(ulong SystemAddress, string SystemName, string PlanetName, int ClassIndex, string TypeLabel,
    string ShipName, string Coordinates, double? PortalKm, bool Underwater, double SystemLy);

/// <summary>A finished crashed-ship search: the finds and how many systems were searched.</summary>
public sealed record ShipSearch(IReadOnlyList<ShipFind> Found, int Systems);

/// <summary>
/// The phone's crashed-ship finder: claimable wrecks of a class (and ship type) on the planets of the systems around a
/// start, nearest systems first, stopping once enough are found. The rules are the desktop's (Core CrashedShipSites,
/// 14/14 wrecks confirmed in game); CrashedShipSites.OfClass is the same wrecks as the full pass
/// (GameData.Checks --crashed-ship-class-check). A planet's whole grid is walked (~0.1 s on a PC), so the search goes
/// system by system rather than over a whole region, and a system whose ship list has no ship of the wanted type is
/// skipped without walking its planets.
/// </summary>
public static class ShipFinderService
{
    /// <summary>Finds to collect before stopping; systems to try at most.</summary>
    public const int Wanted = 12, MaxSystems = 40;

    /// <summary>Wrecks of <paramref name="minClass"/> or better (C=0 .. S=3; Exotics are S) and type
    /// <paramref name="type"/> ("" any), nearest systems to <paramref name="start"/> first. Each system's finds are
    /// reported as they come; null without the rules file.</summary>
    public static async Task<ShipSearch?> FindAsync(ulong start, int minClass, string type,
        IProgress<(int Systems, IReadOnlyList<ShipFind> Found)>? progress, CancellationToken cancel)
    {
        if (await GameRulesService.GetAsync() is not { GalaxySettings: { } settings, Biomes: { } biomes, Profiles: { } profiles,
                Stations: { } stations, BuildingOverrides: { } overrides, BuildingPlacement: { } placement } rules)
            return null;
        return await Task.Run(() =>
        {
            ulong startSystem = start & ~(0xFUL << 52);
            var stars = rules.GalaxyPlacement is { } galaxyPlacement ? RegionStarPositions.ForSystem(startSystem, settings, galaxyPlacement) : null;
            int startIndex = (int)((startSystem >> 40) & 0xFFF);
            var here = stars?.FirstOrDefault(s => s.System == startIndex);
            double Ly(ulong system)
            {
                int index = (int)((system >> 40) & 0xFFF);
                return system == startSystem ? 0
                    : here is { System: > 0 } h && stars?.FirstOrDefault(s => s.System == index) is { System: > 0 } star
                        ? RegionStarPositions.LightYears(star, h) : double.MaxValue;
            }
            // Nearest first: the start, then the region's systems by galaxy-map distance.
            var systems = NearbySystems.Around(startSystem, 0, settings).Select(s => s.SystemAddress & ~(0xFUL << 52))
                .Distinct().OrderBy(Ly).Take(MaxSystems).ToList();
            var found = new List<ShipFind>();
            int done = 0;
            foreach (ulong system in systems)
            {
                cancel.ThrowIfCancellationRequested();
                try { found.AddRange(InSystem(system, minClass, type, Ly(system), biomes, profiles, placement, overrides, stations, rules, cancel)); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One system the generators can't work out mustn't stop the search; it is skipped.
                    System.Diagnostics.Debug.WriteLine($"[ShipFinder] {system:X} skipped: {ex}");
                }
                done++;
                progress?.Report((done, found.ToArray()));
                if (found.Count >= Wanted) break;
            }
            return new ShipSearch(found, done);
        }, cancel);
    }

    private static IEnumerable<ShipFind> InSystem(ulong system, int minClass, string type, double ly, BiomeTables biomes,
        PlanetProfileTables profiles, BuildingClassPlacement[] placement, BuildingOverrideRule[] overrides,
        StationEquipmentTables stations, GameRules rules, CancellationToken cancel)
    {
        var (portal, galaxy) = SystemProfiles.PortalOf(system);
        var centres = PortalPositions.Centres(system, stations.SolarLayout);
        if (centres is null) return [];
        var portals = PortalPositions.ForSystem(system, biomes, profiles, stations, overrides);
        string systemName = SystemGenerator.SystemName(portal, galaxy);
        var finds = new ConcurrentBag<ShipFind>();
        Parallel.For(1, centres.Length + 1, new ParallelOptions { CancellationToken = cancel }, index =>
        {
            var context = CrashedShipSites.Prepare(system | ((ulong)index << 52), centres[index - 1], biomes, profiles, placement,
                overrides, stations.SolarLayout, rules.Words, rules.Inventory, null);
            if (context?.Pool is not { } pool) return;
            // No ship of the wanted type in this system's list: none can lie on its planets.
            if (type.Length > 0 && !pool.Any(s => s.TypeLabel == type)) return;
            var from = portals.GetValueOrDefault(index) is { } p ? CrashedShipSites.Direction(p.Latitude, p.Longitude) : (System.Numerics.Vector3?)null;
            string planetName = SystemGenerator.PlanetName(portal | ((ulong)index << 44), galaxy);
            foreach (var wreck in CrashedShipSites.OfClass(context, minClass, cancel))
            {
                if (wreck.Ship is not { } ship || wreck.ShownClass is not int cls || (type.Length > 0 && ship.TypeLabel != type)) continue;
                double? km = from is { } dir ? CrashedShipSites.Angle(wreck, dir) * context.Radius / 1000 : null;
                finds.Add(new ShipFind(system, systemName, planetName, cls, ship.TypeLabel, ship.DefaultName, wreck.Coordinates, km,
                    wreck.Underwater, ly));
            }
        });
        // Per planet, the wrecks nearest its portal first; a few per planet so one planet doesn't fill the list.
        return finds.GroupBy(f => f.PlanetName)
            .SelectMany(g => g.OrderByDescending(f => f.ClassIndex).ThenBy(f => f.PortalKm ?? double.MaxValue).Take(3))
            .OrderByDescending(f => f.ClassIndex).ThenBy(f => f.PortalKm ?? double.MaxValue).ToList();
    }
}
