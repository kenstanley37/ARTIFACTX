using ArtifactX.Core.NmsModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>How much of a body ArtifactX has from the game screen: nothing, its Discoveries
/// hover (type, resources, sentinel level), or its own page as well (weather, sentinels,
/// flora, fauna).</summary>
public enum CaptureLevel { None, Hover, Page }

public sealed record BodyCapture(KnownPlanet? Planet, int Index, string Name, bool IsMoon, CaptureLevel Level, bool FaunaTotalKnown)
{
    /// <summary>What's still to capture, in the game's words: "open its page", "hover it in Discoveries".</summary>
    public string Missing => Level switch
    {
        CaptureLevel.None => "Hover it in the Discoveries list, then open its page",
        CaptureLevel.Hover => "Open its page (weather, sentinels, flora, fauna)",
        _ => FaunaTotalKnown ? "" : "Its page didn't show a fauna count",
    };
}

public sealed record SystemCapture(ulong System, string Name, bool SystemData, bool Mine, long LatestUnixSeconds, IReadOnlyList<BodyCapture> Bodies)
{
    public int Complete => Bodies.Count(b => b.Level == CaptureLevel.Page);
    public bool IsComplete => SystemData && Complete == Bodies.Count;
}

/// <summary>A region you've found systems in, keyed by its galactic coordinates.</summary>
public sealed record RegionSummary(ulong Key, string Name, int Galaxy, int Systems, int SystemsMine);

/// <summary>
/// Regions and how completely their systems are mapped, for the Regions page. A region isn't
/// stored in the save: it's the galactic coordinates of a system's address (every system of a
/// region shares them; only the system index differs), named by the game's generator - checked
/// 2026-10-20: Ehilusti, Otgravenu, Ulvermon VI and Azeley-Gadia all "Noturbo Spur", as in game.
/// The game doesn't say how many systems a region has, so this counts the ones in the save.
/// </summary>
public static class RegionMapService
{
    /// <summary>The address's galaxy and galactic coordinates ([galaxy:8][Y:8][Z:12][X:12]) - its region.</summary>
    public static ulong RegionKey(ulong address) => address & 0xFF_FFFF_FFFFUL;

    public static List<RegionSummary> Regions(PlanetSnapshot snapshot) =>
        NmsPlanets.ReadSystems(snapshot.Discoveries)
            .GroupBy(s => RegionKey(s.SystemAddress))
            .Select(g => new RegionSummary(g.Key, UniverseInfo.Region(g.First().SystemAddress), g.First().Galaxy, g.Count(), g.Count(s => s.DiscoveredHere)))
            .OrderByDescending(r => r.SystemsMine).ThenByDescending(r => r.Systems).ThenBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>Every system of a region in the save, with what's captured of it and its bodies.</summary>
    public static List<SystemCapture> Systems(PlanetSnapshot snapshot, ulong regionKey)
    {
        var planets = NmsPlanets.ReadAll(snapshot.Discoveries).Where(p => RegionKey(p.SystemAddress) == regionKey)
            .GroupBy(p => (p.SystemAddress, p.PlanetIndex)).Select(g => g.First())
            .ToDictionary(p => (p.SystemAddress, p.PlanetIndex));
        return NmsPlanets.ReadSystems(snapshot.Discoveries).Where(s => RegionKey(s.SystemAddress) == regionKey)
            .Select(s =>
            {
                var info = UniverseInfo.For(s.SystemAddress);
                var note = SystemNotesService.Get(s.SystemAddress);
                var bodies = Enumerable.Range(1, info.PlanetCount + info.MoonCount).Select(i =>
                {
                    var d = PlanetDetailsService.Get(s.SystemAddress, i);
                    var level = d is null ? CaptureLevel.None
                        : d.Weather.Length > 0 || d.Flora.Length > 0 || d.Fauna.Length > 0 ? CaptureLevel.Page
                        : d.Descriptor.Length > 0 || d.Resources.Count > 0 ? CaptureLevel.Hover
                        : CaptureLevel.None;
                    planets.TryGetValue((s.SystemAddress, i), out var planet);
                    return new BodyCapture(planet, i, UniverseInfo.PlanetName(s.SystemAddress, i), BodyKindsService.IsMoon(s.SystemAddress, i),
                        level, d?.FaunaTotal is not null);
                }).ToList();
                bool systemData = note is not null && (note.Economy.Length > 0 || note.Race.Length > 0);
                return new SystemCapture(s.SystemAddress, UniverseInfo.SystemName(s.SystemAddress), systemData, s.DiscoveredHere, s.LatestUnixSeconds, bodies);
            })
            .ToList();
    }
}
