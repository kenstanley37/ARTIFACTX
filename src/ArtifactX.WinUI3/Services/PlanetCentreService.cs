using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The centre of a planet or moon in its solar system's space, as the game places it (PortalPositions.Centres: the solar
/// layout's centre on the 1,000-unit grid). Works for any system, not just the one you're in. Null for bodies the layout
/// port doesn't cover (gas-giant systems, prime bodies) or before the catalog has the tables - callers then leave
/// coordinates out rather than guess.
/// </summary>
public static class PlanetCentreService
{
    private static readonly ConcurrentDictionary<ulong, (double X, double Y, double Z)[]?> Layouts = new();

    /// <param name="planetAddress">A planet's packed address (planet number in bits 52-55).</param>
    public static async Task<(double X, double Y, double Z)?> GetAsync(ulong planetAddress)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        if (index < 1) return null;
        ulong system = planetAddress & ~(0xFUL << 52);
        if (!Layouts.TryGetValue(system, out var centres))
        {
            if (await CatalogService.GetStationEquipmentTablesAsync() is not { } tables) return null;
            centres = await Task.Run(() => PortalPositions.Centres(system, tables.SolarLayout));
            Layouts[system] = centres;
        }
        return centres is null || index > centres.Length ? null : centres[index - 1];
    }
}
