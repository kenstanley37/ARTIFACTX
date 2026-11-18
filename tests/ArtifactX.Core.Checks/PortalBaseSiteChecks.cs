using System.Numerics;
using ArtifactX.Core.NmsModels;

/// <summary>Base spots that reach the portal and hotspots (PortalBaseSites, #63). Synthetic planet and hotspots, not game data:
/// a 100 km planet, portal on the +Z axis, hotspots placed by their ground distance east (+X) or west (-X) of it.</summary>
internal static class PortalBaseSiteChecks
{
    public static void Run()
    {
        const float planet = 100000f, reach = 300f;
        var portal = Vector3.UnitZ;
        PlanetHotspot Spot(string kind, int grade, float metresEast, ulong key)
        {
            float angle = metresEast / planet;
            var position = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * planet;
            return new PlanetHotspot(1, kind, kind, kind, "C", grade, 0f, 40, 0f, 0, 0, "", 0, "", null, null, key, position);
        }
        var power = Spot("Power", 1, 200f, 1);      // 200 m east
        var mineral = Spot("Mineral", 3, -250f, 2); // 250 m west: with power and the portal, the circle centre sits ~25 m west
        var gas = Spot("Gas", 2, 900f, 3);          // never in reach of a base that also holds the portal
        var sites = PortalBaseSites.Find([gas, mineral, power], portal, reach, planet);

        if (sites.Any(s => s.Hotspots.Contains(gas)))
            throw new InvalidOperationException("A hotspot 900 m from the portal was offered with it.");
        if (sites.Count != 3 || !sites.All(s => s.IncludesPortal))
            throw new InvalidOperationException($"Expected power, mineral and power+mineral spots, got {sites.Count}.");
        // Power first; with power, two hotspots beat one.
        if (!(sites[0].Hotspots.Count == 2 && sites[1].Hotspots.SequenceEqual([power]) && sites[2].Hotspots.SequenceEqual([mineral])))
            throw new InvalidOperationException("Ranking is not power first, then more hotspots.");
        foreach (var site in sites)
        {
            var dir = Vector3.Normalize(site.BaseLocalPosition);
            foreach (var target in site.Hotspots.Select(h => h.LocalPosition).Append(portal * planet))
                if (Math.Atan2(Vector3.Cross(dir, Vector3.Normalize(target)).Length(), Vector3.Dot(dir, Vector3.Normalize(target))) * planet > reach + 0.5f)
                    throw new InvalidOperationException($"{site.Title}: something is outside the base's reach.");
        }
        // The pair's best spot is the midpoint of power and mineral: 225 m from each, 25 m from the portal.
        if (Math.Abs(sites[0].MaxRadius - 225) > 1 || Math.Abs(sites[0].PortalDistance!.Value - 25) > 1)
            throw new InvalidOperationException($"Pair placement: reach {sites[0].MaxRadius:F1}, from portal {sites[0].PortalDistance:F1}.");
        var lone = PortalBaseSites.NearestPower([gas, Spot("Power", 0, -1400f, 4)], portal, planet);
        if (lone is not { } nearest || Math.Abs(nearest.Distance - 1400) > 1)
            throw new InvalidOperationException("Nearest power hotspot to the portal not found.");
        Console.WriteLine("PASS: base spots near the portal - reach checked along the ground, power first, far hotspots left out.");
    }
}
