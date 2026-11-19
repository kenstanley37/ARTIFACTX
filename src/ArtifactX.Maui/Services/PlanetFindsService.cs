using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.Services;

/// <summary>A wreck or shop near the portal: what it is, its Analysis Visor coordinates and how far from the portal.</summary>
public sealed record PlanetFind(string Title, string Detail, string Coordinates, double DistanceKm, bool IsBest);

/// <summary>A planet's crashed ships and shops around its portal.</summary>
/// <param name="ClassCounts">"S 3 · A 41 · B 160 · C 420" for the whole planet.</param>
public sealed record PlanetFinds(string PortalCoordinates, string ClassCounts, IReadOnlyList<PlanetFind> Wrecks,
    IReadOnlyList<PlanetFind> Shops, string Note);

/// <summary>
/// Crashed ships and shops on one planet, measured from its portal (the place a console player arrives): the whole
/// planet's wrecks by class, the nearest S- and A-class ones, and the nearest shops (whose cabinets sell the load
/// place's list, SystemCabinets). The rules are Core CrashedShipSites and PlanetBuildingSites, the desktop's (14/14
/// wrecks and the shop sites confirmed in game); the tables come from the rules file. A wreck's slots need the ship
/// part trees, which the phone doesn't carry, so they're left out.
/// </summary>
public static class PlanetFindsService
{
    /// <summary>How many of each of the two best classes to list.</summary>
    private const int PerClass = 2;
    private const int ShopCount = 3;

    /// <summary>The finds of <paramref name="planetAddress"/> (planet number in bits 52-55), or null with a reason.</summary>
    public static (PlanetFinds? Finds, string Reason) ForPlanet(ulong planetAddress, GameRules rules)
    {
        if (rules is not { Biomes: { } biomes, Profiles: { } profiles, Stations: { } stations, BuildingOverrides: { } overrides,
                BuildingPlacement: { } placement })
            return (null, "This copy of ArtifactX doesn't have the game rules it needs for crashed ships.");
        int index = (int)((planetAddress >> 52) & 0xF);
        ulong system = planetAddress & ~(0xFUL << 52);
        var portal = PortalPositions.ForSystem(system, biomes, profiles, stations, overrides).GetValueOrDefault(index);
        var centres = PortalPositions.Centres(system, stations.SolarLayout);
        if (portal is null || centres is null || index > centres.Length)
            return (null, "Crashed ships on this body can't be placed yet - gas-giant systems aren't worked out.");
        var context = CrashedShipSites.Prepare(planetAddress, centres[index - 1], biomes, profiles, placement, overrides,
            stations.SolarLayout, rules.Words, rules.Inventory, null);
        if (context is null) return (null, "Crashed ships on this body can't be worked out.");

        var from = CrashedShipSites.Direction(portal.Latitude, portal.Longitude);
        double Km(CrashedShipSite s) => CrashedShipSites.Angle(s, from) * context.Radius / 1000;
        var wrecks = CrashedShipSites.All(context).Where(s => s.Ship is not null && s.ShownClass is not null).ToList();
        string Letter(int cls) => cls switch { 3 => "S", 2 => "A", 1 => "B", _ => "C" };
        string counts = string.Join(" · ", Enumerable.Range(0, 4).Reverse()
            .Select(c => (c, n: wrecks.Count(w => w.ShownClass == c))).Where(x => x.n > 0).Select(x => $"{Letter(x.c)} {x.n:N0}"));

        var listed = new List<PlanetFind>();
        foreach (int cls in new[] { 3, 2 })
            foreach (var w in wrecks.Where(w => w.ShownClass == cls).OrderBy(Km).Take(PerClass))
            {
                double km = Km(w);
                string where = w.Underwater ? w.DepthMetres is double d ? $"Underwater, about {Math.Round(d / 10) * 10:N0} m down" : "Underwater" : "On land";
                listed.Add(new PlanetFind($"{Letter(cls)}-class {w.Ship!.TypeLabel}: {w.Ship.DefaultName}",
                    $"{where} · {km:N1} km from the portal", w.Coordinates, km, cls == 3));
            }

        var shops = context.Uncharted ? [] : PlanetBuildingSites.SitesNear(from, context.Divisions, context.Radius,
                context.SectorSeed, context.Slots, cls => cls == BuildingClass.Shop, ShopCount * 4)
            .Where(context.Builds).Take(ShopCount)
            .Select(site =>
            {
                var (lat, lon) = site.Coordinates;
                double km = Core.NmsModels.PlanetHotspotCluster.GroundDistance(site.Point, from, 1.0) * context.Radius / 1000;
                return new PlanetFind("Shop with a multi-tool cabinet", $"{km:N1} km from the portal",
                    Core.NmsModels.NmsPlanetCoordinates.Format(lat, lon), km, false);
            }).ToArray();

        string note = context.Uncharted
            ? "This system has no race, so the only claimable wreck is one fixed underwater ship, if the planet has one; there are no shops."
            : wrecks.Count == 0 ? "No crashed ships could be placed on this planet." : "";
        return (new PlanetFinds(portal.Coordinates, counts, listed, shops, note), "");
    }
}
