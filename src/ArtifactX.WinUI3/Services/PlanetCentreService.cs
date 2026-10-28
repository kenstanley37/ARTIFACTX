using ArtifactX.Core.Universe;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The exact centre of a planet or moon in its solar system's space, from the game's solar layout
/// (<see cref="SolarLayoutGeneration.ForSystem"/>, catalog solar-layout tables). Works for any system, not just the
/// one you're in. Null for bodies the layout port doesn't cover (gas-giant systems, prime bodies) or before the
/// catalog has the tables - callers then leave coordinates out rather than guess.
/// </summary>
public static class PlanetCentreService
{
    private static readonly ConcurrentDictionary<ulong, SolarLayoutGeneration.SystemLayout?> Layouts = new();

    /// <param name="planetAddress">A planet's packed address (planet number in bits 52-55).</param>
    public static async Task<(double X, double Y, double Z)?> GetAsync(ulong planetAddress)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        if (index < 1) return null;
        ulong system = planetAddress & ~(0xFUL << 52);
        if (!Layouts.TryGetValue(system, out var layout))
        {
            if (await CatalogService.GetStationEquipmentTablesAsync() is not { } tables) return null;
            var (code, galaxy) = UniverseInfo.Generator(system);
            layout = await Task.Run(() => SolarLayoutGeneration.ForSystem(code, galaxy, tables.SolarLayout, true));
            Layouts[system] = layout;
        }
        if (layout is null || index > layout.Centers.Length) return null;
        var c = layout.Centers[index - 1];
        return (c.X, c.Y, c.Z);
    }
}
