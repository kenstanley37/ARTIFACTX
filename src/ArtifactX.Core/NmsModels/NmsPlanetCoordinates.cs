using System.Globalization;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// The planetary coordinates the game shows on the ship's dashboard
/// ("+8.92, +29.91" - latitude, longitude in degrees), worked out from the save.
///
/// The save has no latitude/longitude, but it has both halves of the sum:
///   rnc.mEH  the player's position in the solar system (GcPlayerSpawnStateData
///            PlayerPositionInSystem; rnc sits next to 6f=, so the expedition
///            remap applies to it too). rnc.jk4 is where the player is
///            (OnFoot, InShip, InShipLanded, InStation, AboardFleet...).
///   6f=.aHM  every planet's centre in the current system (GcPlayerStateData
///            PlanetPositions, 16 slots, rounded to 1000; unused slots are 0,0,0).
///            Slot = planet index - 1 (the planet index is the address's jsv).
/// Direction from the centre d = normalise(position - centre):
///   latitude  = asin(d.y)          (+y is north)
///   longitude = atan2(d.x, d.z)
///
/// Checked 2026-10-14 on Ippianes III (DELETEME, planet 1, radius ~64,640):
/// two dashboard readings taken while flying in to land, vs the save made on
/// landing - +8.92/+29.91 vs +8.93/+29.74 and +0.53/+53.18 vs +0.52/+53.51;
/// latitude within 0.01, longitude off along the flight path (the reading
/// was taken before touching down). The centre also matched, to ~50 units, the
/// one implied by the player standing at their base (bases' wMC is the base's
/// position measured from the planet centre).
/// </summary>
public static class NmsPlanetCoordinates
{
    public static readonly string[] SpawnStatePath = { "vLc", "rnc" };
    public static readonly string[] PlanetPositionsPath = { "vLc", "6f=", "aHM" };

    /// <summary>Where the player's on-planet coordinates make no sense
    /// (indoors somewhere that isn't on the planet's surface).</summary>
    private static readonly HashSet<string> OffPlanetStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "InStation", "AboardFleet", "InNexus", "AbandonedFreighter", "OnFootInCorvette",
    };

    /// <summary>The player's coordinates on planet <paramref name="planetIndex"/>
    /// (1-6), or null when they're not on or near it.</summary>
    public static (double Latitude, double Longitude)? Read(JToken? spawn, JToken? planetPositions, int planetIndex)
    {
        if (planetIndex <= 0 || spawn is null) return null;
        if (spawn["jk4"]?.Value<string>() is string state && OffPlanetStates.Contains(state)) return null;
        if (Vector(spawn["mEH"]) is not { } player) return null;
        if (Vector((planetPositions as JArray)?.ElementAtOrDefault(planetIndex - 1)) is not { } centre) return null;
        if (centre.X == 0 && centre.Y == 0 && centre.Z == 0) return null;
        return From(player, centre);
    }

    /// <summary>
    /// Every planet and moon centre in the current system: (planet index, x, y, z) for each used slot.
    /// Moons sit 226,000 units (rounded) from the planet they orbit; that matched the system generator's
    /// moon hosts in all nine save systems checked 2026-10-27 (three with moons), and no other two bodies
    /// were closer than about 680,000.
    /// </summary>
    public static List<(int Index, double X, double Y, double Z)> Centres(JToken? planetPositions)
    {
        var centres = new List<(int, double, double, double)>();
        if (planetPositions is not JArray slots) return centres;
        for (int i = 0; i < slots.Count; i++)
            if (Vector(slots[i]) is { } c && (c.X != 0 || c.Y != 0 || c.Z != 0)) centres.Add((i + 1, c.X, c.Y, c.Z));
        return centres;
    }

    /// <summary>The player's position in the solar system (rnc.mEH), the same space as the planet centres.</summary>
    public static (double X, double Y, double Z)? Player(JToken? spawn) => Vector(spawn?["mEH"]);

    /// <summary>Latitude/longitude of a system-space position on the planet
    /// whose centre is given.</summary>
    public static (double Latitude, double Longitude)? From((double X, double Y, double Z) position, (double X, double Y, double Z) centre)
    {
        double x = position.X - centre.X, y = position.Y - centre.Y, z = position.Z - centre.Z;
        double r = Math.Sqrt(x * x + y * y + z * z);
        if (r < 1) return null;
        return (Math.Asin(Math.Clamp(y / r, -1, 1)) * 180 / Math.PI, Math.Atan2(x, z) * 180 / Math.PI);
    }

    /// <summary>Coordinates of a planet base (F?0 entry, or a teleporter):
    /// its wMC is already measured from the planet's centre. Ippianes III's
    /// base [46176, 350, 45226] gives +0.31, +45.60, where the player stood
    /// beside it (2026-10-14).</summary>
    public static (double Latitude, double Longitude)? OfBase(JToken? baseEntry) =>
        Vector(baseEntry?["wMC"]) is { } at ? From(at, (0, 0, 0)) : null;

    /// <summary>Coordinates of an active planetary scan event (A1f entry, GcScanEventSave):
    /// its TWn (BuildingLocation) is measured from the planet's centre (2026-10-23).</summary>
    public static (double Latitude, double Longitude)? OfScanEvent(JToken? eventEntry) =>
        Vector(eventEntry?["TWn"]) is { } at ? From(at, (0, 0, 0)) : null;

    /// <summary>As the game writes them: "+8.92, -29.91".</summary>
    public static string Format(double latitude, double longitude) =>
        $"{Signed(latitude)}, {Signed(longitude)}";

    private static string Signed(double value) =>
        (value >= 0 ? "+" : "") + value.ToString("0.00", CultureInfo.InvariantCulture);

    private static (double X, double Y, double Z)? Vector(JToken? token) =>
        token is JArray { Count: >= 3 } a
        && a[0].Type is JTokenType.Float or JTokenType.Integer
            ? (a[0].Value<double>(), a[1].Value<double>(), a[2].Value<double>())
            : null;
}
