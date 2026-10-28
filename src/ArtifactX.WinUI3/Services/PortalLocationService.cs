using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A planet's portal: its site key (the marker's BuildingSeed), ground position in system space and the
/// coordinates the Analysis Visor shows there.</summary>
public sealed record PortalLocation(ulong PlanetAddress, ulong Key, double X, double Y, double Z, double Latitude, double Longitude)
{
    public string Coordinates => NmsPlanetCoordinates.Format(Latitude, Longitude);
}

/// <summary>
/// Where each planet's portal stands, worked out from the address with the game's own placement rules
/// (<see cref="PlanetOverridePlacement.ForSystem"/>, catalog format 48). A whole system at a time, off the UI thread,
/// cached per system. Null for bodies it can't place (gas-giant systems, older game data). Checked 2026-10-28 against
/// the game's records on 12 bodies; markers made from it led to the portal on 5 planets in game.
/// </summary>
public static class PortalLocationService
{
    private const int PortalClass = 16;
    private static readonly ConcurrentDictionary<ulong, Task<IReadOnlyDictionary<int, PortalLocation>?>> Systems = new();

    // A rebuilt catalog may change the rules; results worked out before it are dropped.
    static PortalLocationService() => CatalogService.Reloaded += Systems.Clear;

    /// <param name="planetAddress">A planet's packed address (planet number in bits 52-55).</param>
    public static async Task<PortalLocation?> GetAsync(ulong planetAddress)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        if (index < 1) return null;
        ulong system = planetAddress & ~(0xFUL << 52);
        var task = Systems.GetOrAdd(system, ForSystemAsync);
        try
        {
            // Null = the catalog lacked the tables (not built yet, or older game data): not cached, so the next
            // ask - after a rebuild - works it out again.
            if (await task is not { } portals)
            {
                Systems.TryRemove(new KeyValuePair<ulong, Task<IReadOnlyDictionary<int, PortalLocation>?>>(system, task));
                return null;
            }
            return portals.GetValueOrDefault(index);
        }
        catch (Exception ex)
        {
            Systems.TryRemove(system, out _);
            System.Diagnostics.Debug.WriteLine($"[PortalLocation] {system:X}: {ex.Message}");
            return null;
        }
    }

    private static async Task<IReadOnlyDictionary<int, PortalLocation>?> ForSystemAsync(ulong system)
    {
        var result = new Dictionary<int, PortalLocation>();
        var biomes = await CatalogService.GetBiomeTablesAsync();
        var profiles = await CatalogService.GetPlanetProfileTablesAsync();
        var stations = await CatalogService.GetStationEquipmentTablesAsync();
        var rules = await CatalogService.GetBuildingOverrideRulesAsync();
        if (biomes is null || profiles is null || stations is null || rules is null) return null;
        var (code, galaxy) = UniverseInfo.Generator(system);
        var bodies = await Task.Run(() => PlanetOverridePlacement.ForSystem(code, galaxy, biomes, profiles, stations.SolarLayout, rules));
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] is not { } buildings) continue;
            foreach (var b in buildings)
            {
                if (b.Class != PortalClass || !b.Placed) continue;
                ulong address = system | ((ulong)(i + 1) << 52);
                if (await PlanetCentreService.GetAsync(address) is not { } centre
                    || NmsPlanetCoordinates.From((b.Position.X, b.Position.Y, b.Position.Z), centre) is not { } at) break;
                result[i + 1] = new PortalLocation(address, b.Key, b.Position.X, b.Position.Y, b.Position.Z, at.Latitude, at.Longitude);
                break;
            }
        }
        return result;
    }
}
