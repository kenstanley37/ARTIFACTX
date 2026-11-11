using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.Core.NmsModels;

public enum GalaxyClassification
{
    Normal,
    Lush,
    Harsh,
    Empty
}

public sealed record GalaxyInfo(
    int Number,
    int RealityIndex,
    string Name,
    GalaxyClassification Classification)
{
    public string ClassificationName => Classification switch
    {
        GalaxyClassification.Lush => "Lush (Halcyon)",
        GalaxyClassification.Harsh => "Harsh (Raging)",
        GalaxyClassification.Empty => "Empty (Ancestral)",
        _ => "Normal (Improved)"
    };

    public string Description => Classification switch
    {
        GalaxyClassification.Lush => "Significantly increased probability of tropical, overgrown, verdant, and temperate paradise worlds with pleasant weather in yellow star systems.",
        GalaxyClassification.Harsh => "Dramatically increased frequency of extreme weather, hazardous atmospheric conditions, and aggressive sentinel presence.",
        GalaxyClassification.Empty => "Higher frequency of airless, dead, barren, and exotic/anomalous boundary failure worlds.",
        _ => "Standard baseline procedural generation with a balanced distribution of all planetary biomes and weather types."
    };
}

/// <summary>
/// Galaxy classification, metadata and core gateway destination definitions for all 256 galaxies.
/// </summary>
public static class NmsGalaxyInfo
{
    /// <summary>
    /// Gateway region for galaxy transfers: (5, -5, -6), ~3,709 light-years from the galactic core (portal glyphs
    /// ?SSSFBFFA005). Owner, 2026-11-10: land "close to the core, somewhere within 5,000 ly". The earlier region
    /// (7, -8, -9) was 5,571 ly out. Regions closer than 8 voxels (~3,200 ly, GcGalaxyGlobals GoalGapVoxelDist in the
    /// catalog) are the empty goal gap with no systems; this region holds 230-531 systems in every galaxy
    /// (RegionStarGeneration over all 256 galaxies, 2026-11-10).
    /// </summary>
    public const int CoreGatewayX = 5;
    public const int CoreGatewayY = -5;
    public const int CoreGatewayZ = -6;
    public static double DistanceToCoreLightYears => Math.Sqrt(CoreGatewayX * CoreGatewayX + CoreGatewayY * CoreGatewayY + CoreGatewayZ * CoreGatewayZ) * 400;

    private static readonly HashSet<int> LushGalaxies = BuildSequence(10, 250, 9, 11);
    private static readonly HashSet<int> HarshGalaxies = BuildSequence(3, 255, 12, 8);
    private static readonly HashSet<int> EmptyGalaxies = BuildSequence(7, 252, 5, 15);

    private static HashSet<int> BuildSequence(int start, int end, int stepA, int stepB)
    {
        var set = new HashSet<int>();
        int current = start;
        bool toggle = true;
        while (current <= end)
        {
            set.Add(current);
            current += toggle ? stepA : stepB;
            toggle = !toggle;
        }
        return set;
    }

    public static GalaxyClassification GetClassification(int galaxyNumber)
    {
        if (LushGalaxies.Contains(galaxyNumber)) return GalaxyClassification.Lush;
        if (HarshGalaxies.Contains(galaxyNumber)) return GalaxyClassification.Harsh;
        if (EmptyGalaxies.Contains(galaxyNumber)) return GalaxyClassification.Empty;
        return GalaxyClassification.Normal;
    }

    private static readonly Lazy<List<GalaxyInfo>> _allGalaxies = new(() =>
    {
        var list = new List<GalaxyInfo>(256);
        for (int i = 1; i <= 256; i++)
        {
            string name = NmsGalaxyNames.ForNumber(i) ?? $"Galaxy {i}";
            list.Add(new GalaxyInfo(i, i - 1, name, GetClassification(i)));
        }
        return list;
    });

    public static IReadOnlyList<GalaxyInfo> AllGalaxies => _allGalaxies.Value;

    public static GalaxyInfo? Get(int galaxyNumber) =>
        galaxyNumber >= 1 && galaxyNumber <= 256 ? AllGalaxies[galaxyNumber - 1] : null;

    /// <summary>Any address in the gateway region of a galaxy (0..255); use it to ask how many systems the region holds.</summary>
    public static ulong GatewayRegionAddress(int realityIndex) =>
        NmsSpaceStationPaths.PackAddress(realityIndex, CoreGatewayX, CoreGatewayY, CoreGatewayZ, 1);

    /// <summary>
    /// The galaxy transfer destination in galaxy <paramref name="realityIndex"/> (0..255): a star system in the gateway
    /// region that exists and has a working space station. The transfer spawns the player "InStation"; owner, 2026-11-10:
    /// that worked everywhere except one galaxy whose destination had no station, where the game put them on a planet.
    /// The earlier fixed system 407 could not work everywhere: the region generator (RegionStarGeneration, exact 225/225)
    /// gave the old region only 231 systems in 42 of the 256 galaxies, Euclid included, and an Uncharted system has no
    /// station at all. This picks the first system (1..N) that is not Uncharted, Abandoned (no trade or services) or
    /// an outlaw system, so the player arrives at an ordinary station. Near the core most systems are Uncharted: in
    /// Euclid systems 1-8 and 11 are Uncharted, 9-10 Abandoned, and 12 (Gek, Scientific) is the destination.
    /// <paramref name="ordinarySystems"/> is <see cref="Universe.RegionStarGeneration"/>'s count for the region (systems 1..N).
    /// Null when no system qualifies.
    /// </summary>
    public static ulong? FindGatewaySystemAddress(int realityIndex, int ordinarySystems)
    {
        bool HasStation(int systemIndex)
        {
            if (systemIndex < 1 || systemIndex > ordinarySystems) return false;
            ulong address = NmsSpaceStationPaths.PackAddress(realityIndex, CoreGatewayX, CoreGatewayY, CoreGatewayZ, systemIndex);
            ulong code = ulong.Parse(NmsSpaceStationPaths.PortalAddress(address, 0), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture);
            var attributes = Universe.SystemGenerator.Attributes(code, realityIndex);
            return !attributes.Uncharted && !attributes.Abandoned && !attributes.Pirate;
        }

        for (int system = 1; system <= ordinarySystems; system++)
            if (HasStation(system))
                return NmsSpaceStationPaths.PackAddress(realityIndex, CoreGatewayX, CoreGatewayY, CoreGatewayZ, system);
        return null;
    }
}

