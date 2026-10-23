using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Source of a multi-tool pool in a star system.
/// </summary>
public enum MultiToolPoolSource
{
    SpaceStation,
    Planet,
}

/// <summary>
/// A native multi-tool in a system's or planet's procedural pool.
/// </summary>
/// <param name="Index">1-based index within its source pool.</param>
/// <param name="Seed">The 64-bit seed determining model parts, accessories, colors and kind.</param>
/// <param name="Source">The pool source (Space Station or Planet).</param>
/// <param name="SourceLabel">Label of the source (e.g. "Space Station" or "Planet 1 (Irskya)").</param>
/// <param name="DefaultName">The procedural name the game assigns to this tool.</param>
/// <param name="ModelPath">The scene MBIN model file.</param>
public sealed record SystemMultiTool(
    int Index,
    ulong Seed,
    MultiToolPoolSource Source,
    string SourceLabel,
    string DefaultName,
    string ModelPath)
{
    public string SeedHex => $"0x{Seed:X16}";
    public bool IsStationPool => Source == MultiToolPoolSource.SpaceStation;
    public bool IsRoyal => ModelPath.Contains("ROYAL", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Generates the native multi-tool pools for a star system (Space Station pool and per-planet pools).
///
/// In No Man's Sky multi-tool mechanics (reverse-engineered from NMS.exe build 180836, 0x1406f9330 and 0x1404d44d0):
/// - Every star system has a Space Station pool and one pool per planet/moon.
/// - Each pool contains 2 to 4 native multi-tools (Standard rifles/pistols/alien/experimental), plus rare exotic rolls.
/// - When a player saves and reloads on a planet, that planet's pool becomes active across all cabinets in the system.
/// - When a player reloads on the Space Station, the station's pool is active.
/// - Seeds are derived from the station portal seed or the planet's seed using the MWC PRNG and 64-bit mixer (Mix64).
/// - Names are generated via MultiToolNames from the player's game files.
/// </summary>
public static class SystemMultiToolGenerator
{
    public const string MainModel = "MODELS/COMMON/WEAPONS/MULTITOOL/MULTITOOL.SCENE.MBIN";
    public const string RoyalModel = "MODELS/COMMON/WEAPONS/MULTITOOL/ROYALMULTITOOL.SCENE.MBIN";

    private const ulong ConstA = 0x64DD81482CBD31D7UL;
    private const ulong ConstB = 0xE36AA5C613612997UL;

    private static ulong Mix64(ulong register)
    {
        register = ((register >> 33) ^ register) * ConstA;
        register = ((register >> 33) ^ register) * ConstB;
        return (register >> 33) ^ register;
    }

    /// <summary>
    /// Generates multi-tools for a single pool (Space Station or a specific planet).
    /// </summary>
    public static IReadOnlyList<SystemMultiTool> GeneratePool(
        ulong poolSeed,
        MultiToolPoolSource source,
        string sourceLabel,
        Func<string, IReadOnlyList<string>?>? words = null,
        int count = 3)
    {
        uint lo = (uint)poolSeed;
        uint hi = (uint)(poolSeed >> 32);
        uint s0 = lo == 0 ? 1 : lo;
        uint s1 = ((lo >> 16) | (lo << 16)) ^ hi ^ lo;

        var rng = new UniverseRandom(((ulong)s1 << 32) | s0);

        var list = new List<SystemMultiTool>(count + 1);

        for (int i = 1; i <= count; i++)
        {
            uint low = rng.NextWord();
            uint high = rng.NextWord();
            ulong seed = Mix64(((ulong)high << 32) | low);
            string name = words != null ? MultiToolNames.Generate(seed, isStaff: false, words) ?? $"Multi-Tool {seed:X16}" : $"Multi-Tool {seed:X16}";
            list.Add(new SystemMultiTool(i, seed, source, sourceLabel, name, MainModel));
        }

        // Royal / Exotic multi-tool (1 draw from Royal pool)
        {
            uint low = rng.NextWord();
            uint high = rng.NextWord();
            ulong seed = Mix64(((ulong)high << 32) | low);
            string name = words != null ? MultiToolNames.Generate(seed, isStaff: false, words) ?? $"Royal Tool {seed:X16}" : $"Royal Tool {seed:X16}";
            list.Add(new SystemMultiTool(count + 1, seed, source, sourceLabel, name, RoyalModel));
        }

        return list;
    }

    /// <summary>
    /// Generates all native multi-tool pools for a star system: the Space Station pool and each planet's pool.
    /// </summary>
    public static IReadOnlyList<SystemMultiTool> GenerateSystem(
        ulong portalCode,
        IReadOnlyList<(ulong PlanetSeed, int PlanetIndex, string Name)> planets,
        Func<string, IReadOnlyList<string>?>? words = null)
    {
        var allTools = new List<SystemMultiTool>();

        // 1. Space Station Pool
        allTools.AddRange(GeneratePool(portalCode, MultiToolPoolSource.SpaceStation, "Space Station", words));

        // 2. Planet Pools
        foreach (var p in planets)
        {
            string label = string.IsNullOrEmpty(p.Name) ? $"Planet {p.PlanetIndex}" : p.Name;
            allTools.AddRange(GeneratePool(p.PlanetSeed, MultiToolPoolSource.Planet, label, words));
        }

        return allTools;
    }
}

