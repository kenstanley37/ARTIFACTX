using ArtifactX.Core.NmsModels;
using System;
using System.Collections.Generic;

namespace ArtifactX.Core.Universe;

/// <summary>A planet's portal: its site key (the marker's BuildingSeed), ground position in system space and the
/// coordinates the Analysis Visor shows there.</summary>
public sealed record PortalPosition(int Planet, ulong Key, double X, double Y, double Z, double Latitude, double Longitude)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);
}

/// <summary>
/// Where planets sit and where each one's portal stands, from the address and the game's own rules - no save needed.
/// Shared by the desktop (PlanetCentreService, PortalLocationService) and the mobile app.
/// </summary>
public static class PortalPositions
{
    /// <summary>A system's address split into the generator's portal code and galaxy (0-255).</summary>
    public static (ulong Code, int Galaxy) Generator(ulong address, int planet = 0) =>
        (((ulong)(planet & 0xF) << 44) | (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF), (int)((address >> 32) & 0xFF));

    /// <summary>
    /// Each body's centre in system space, as the game places it: the solar layout's centre
    /// (<see cref="SolarLayoutGeneration.ForSystem"/>) on the 1,000-unit grid below it - the value the save stores in
    /// <c>6f=.aHM</c>. Checked 2026-10-28 on Peak XV: the Analysis Visor read -37.21, +17.61 where the player stood;
    /// the floored centre gives exactly that, the raw generated one -37.51, +17.28. Null for systems the layout port
    /// doesn't cover (gas-giant systems, prime bodies).
    /// </summary>
    public static (double X, double Y, double Z)[]? Centres(ulong systemAddress, SolarLayoutTables tables)
    {
        var (code, galaxy) = Generator(systemAddress);
        if (SolarLayoutGeneration.ForSystem(code, galaxy, tables, true) is not { } layout) return null;
        var centres = new (double, double, double)[layout.Centers.Length];
        for (int i = 0; i < centres.Length; i++)
            centres[i] = (Grid(layout.Centers[i].X), Grid(layout.Centers[i].Y), Grid(layout.Centers[i].Z));
        return centres;
    }

    private static double Grid(float value) => Math.Floor(value / 1000.0) * 1000.0;

    /// <summary>
    /// Each planet's portal (by planet number, 1-6), worked out with the game's own placement rules
    /// (<see cref="PlanetOverridePlacement.ForSystem"/>, catalog format 48). Checked 2026-10-28 against the game's
    /// records on 12 bodies; markers made from it led to the portal on 5 planets in game. Planets it can't place are
    /// left out.
    /// </summary>
    public static Dictionary<int, PortalPosition> ForSystem(ulong systemAddress, BiomeTables biomes, PlanetProfileTables profiles,
        StationEquipmentTables stations, BuildingOverrideRule[] rules)
    {
        var result = new Dictionary<int, PortalPosition>();
        var (code, galaxy) = Generator(systemAddress);
        var bodies = PlanetOverridePlacement.ForSystem(code, galaxy, biomes, profiles, stations.SolarLayout, rules);
        var centres = Centres(systemAddress, stations.SolarLayout);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] is not { } buildings || centres is null || i >= centres.Length) continue;
            foreach (var b in buildings)
            {
                if (b.Class != BuildingClass.Portal || !b.Placed) continue;
                if (NmsPlanetCoordinates.From((b.Position.X, b.Position.Y, b.Position.Z), centres[i]) is not { } at) break;
                result[i + 1] = new PortalPosition(i + 1, b.Key, b.Position.X, b.Position.Y, b.Position.Z, at.Latitude, at.Longitude);
                break;
            }
        }
        return result;
    }
}
