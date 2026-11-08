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
    // Canonical gateway system coordinates ~5,571 light-years from the galactic core:
    public const int CoreGatewayX = 7;
    public const int CoreGatewayY = -8;
    public const int CoreGatewayZ = -9;
    public const int CoreGatewaySystemIndex = 407;
    public const int CoreGatewayPlanet = 0;
    public const double DistanceToCoreLightYears = 5571.36;

    /// <summary>12-glyph portal address for the gateway core system (P SSS YY ZZZ XXX).</summary>
    public const string CoreGatewayPortalAddress = "1197777F8808";

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

    /// <summary>Calculates the 64-bit packed system address for the core gateway in the given galaxy (0..255).</summary>
    public static ulong GetGatewaySystemAddress(int realityIndex) =>
        NmsSpaceStationPaths.PackAddress(realityIndex, CoreGatewayX, CoreGatewayY, CoreGatewayZ, CoreGatewaySystemIndex, CoreGatewayPlanet);
}

