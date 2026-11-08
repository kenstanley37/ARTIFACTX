using System.Linq;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Helpers.Location;

/// <summary>
/// Where the player is, in a line for the title bar (#33): the most specific place
/// the save says (planet, space station, freighter, the Anomaly...) and its
/// system, with region, galaxy and portal address for the tooltip.
///
/// The place comes from the player state word rnc.jk4 (NmsPlanetCoordinates.State)
/// and the planet index in the universe address (oZw.jsv, 0 = in space). Values
/// seen in the owner's saves 2026-11-04: OnFoot (Slots 3, 5, on planet 1),
/// InStation (6, 8), AboardFleet (2, 4, 7), InNexus (1, the Space Anomaly).
/// InShip / InShipLanded / AbandonedFreighter / OnFootInCorvette are the other
/// states NmsPlanetCoordinates knows; anything else reads as the plain system.
/// </summary>
public static class PlayerLocationText
{
    /// <summary>Null when the snapshot has no universe address.</summary>
    public static (string Line, string Detail)? Describe(PlanetSnapshot snapshot, string? freighterName)
    {
        if (S.ReadSystemAddress(snapshot.Universe) is not ulong system) return null;
        int planet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        string state = NmsPlanetCoordinates.State(snapshot.Spawn);
        string systemName = UniverseInfo.SystemName(system);
        string? planetName = planet > 0 ? UniverseInfo.PlanetName(system, planet) : null;

        string place = state switch
        {
            // The Anomaly is its own place; the address still names the system it sits in.
            "InNexus" => "Space Anomaly",
            "InStation" => SystemNotesService.Get(system)?.StationName is { Length: > 0 } station ? station : $"{systemName} Station",
            "AboardFleet" => freighterName is { Length: > 0 } name ? $"Aboard {name}" : "Aboard your freighter",
            "AbandonedFreighter" => "Derelict freighter",
            "OnFootInCorvette" => "Aboard your corvette",
            "InShip" when planetName is not null => $"Flying over {planetName}",
            _ when planetName is not null => $"On {planetName}",
            "InShip" => "In your ship",
            _ => "In space",
        };
        string line = state == "InNexus" ? place : $"{place} · {systemName}";

        string typeDetail = "";
        if (planet > 0)
        {
            var pDetails = PlanetDetailsService.Get(system, planet);
            var kp = NmsPlanets.ReadAll(snapshot.Discoveries).FirstOrDefault(p => p.PlanetIndex == planet);
            bool isMoon = BodyKindsService.IsMoon(system, planet);
            string type = kp is not null
                ? kp.Classification(pDetails?.Descriptor, isMoon: isMoon)
                : pDetails?.Descriptor is { Length: > 0 } desc ? desc : "";
            if (type.Length > 0) typeDetail = $" · {type}";
        }

        int galaxy = S.GalaxyNumber(system);
        string detail = $"{place}{typeDetail}\n{systemName} · {UniverseInfo.Region(system)} · {PlanetText.GalaxyLabel(galaxy)}\n"
            + $"Portal address {S.PortalAddress(system, planet > 0 ? planet : 1)}";
        return (line, detail);
    }
}
