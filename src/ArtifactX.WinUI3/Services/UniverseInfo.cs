using ArtifactX.Core.Universe;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>What a star system is like, from the game's own generator
/// (ArtifactX.Core.Universe, ported from nms_namegen) - the same answers the
/// game shows in its system info, for any address, visited or not.</summary>
public sealed record SystemFacts(
    string GeneratedName,
    string Region,
    StarColour Star,
    SystemEconomy Economy,
    int Wealth,
    int Conflict,
    SystemRace Race,
    bool Uncharted,
    bool Abandoned,
    bool Pirate,
    int PlanetCount,
    int MoonCount)
{
    public string StarLabel => Star.ToString();

    public string RaceLabel => Uncharted ? "None (uncharted)" : Abandoned ? "Abandoned" : Race switch
    {
        SystemRace.Gek => "Gek",
        SystemRace.Korvax => "Korvax",
        SystemRace.Vykeen => "Vy'keen",
        _ => "None",
    };

    /// <summary>Economy in the same words the Planets notes use for its categories.</summary>
    public string EconomyLabel => Uncharted || Abandoned ? "None" : Economy switch
    {
        SystemEconomy.Trading => "Trading",
        SystemEconomy.AdvancedMaterials => "Advanced Materials",
        SystemEconomy.Scientific => "Scientific",
        SystemEconomy.Mining => "Mining",
        SystemEconomy.Manufacturing => "Manufacturing",
        SystemEconomy.Technology => "Technology",
        SystemEconomy.PowerGeneration => "Power Generation",
        _ => "None",
    };

    /// <summary>Same labels as SystemNotesService.TierName ("High ★★★"), so notes and facts read alike.</summary>
    public string WealthLabel => Uncharted ? "None" : Wealth switch { 3 => "High ★★★", 2 => "Medium ★★", _ => "Low ★" };

    public string ConflictLabel => Uncharted ? "None" : Conflict switch { 3 => "High", 2 => "Medium", _ => "Low" };

    public string BodiesLabel =>
        MoonCount > 0 ? $"{PlanetCount} planet{(PlanetCount == 1 ? "" : "s")} and {MoonCount} moon{(MoonCount == 1 ? "" : "s")}"
                      : $"{PlanetCount} planet{(PlanetCount == 1 ? "" : "s")}";
}

/// <summary>
/// One place for system and planet names and system details. The player's
/// own data wins: a name the save recorded (a player may have renamed the
/// system) or typed on the Planets page, and their notes. Otherwise the
/// generator's answer. Addresses are the save's packed form
/// ([planet:4][system:12][galaxy:8][Y:8][Z:12][X:12]); results are cached.
/// </summary>
public static class UniverseInfo
{
    private static readonly ConcurrentDictionary<ulong, SystemFacts> Facts = new();
    private static readonly ConcurrentDictionary<ulong, string[]> PlanetNamesCache = new();

    private static ulong SystemKey(ulong address) => address & ~(0xFUL << 52);

    /// <summary>Portal code (planet digit 0) and 0-based galaxy for the generator.</summary>
    public static (ulong Code, int Galaxy) Generator(ulong address, int planet = 0) =>
        (((ulong)(planet & 0xF) << 44) | (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF), (int)((address >> 32) & 0xFF));

    /// <summary>Portal code numeric value for an address.</summary>
    public static ulong PortalCode(ulong address, int planet = 0) => Generator(address, planet).Code;

    /// <summary>Generated facts about the system an address is in.</summary>
    public static SystemFacts For(ulong address) => Facts.GetOrAdd(SystemKey(address), key =>
    {
        var (code, galaxy) = Generator(key);
        var a = SystemGenerator.Attributes(code, galaxy);
        var bodies = SystemGenerator.Bodies(code, galaxy);
        return new SystemFacts(SystemGenerator.SystemName(code, galaxy), SystemGenerator.RegionName(code, galaxy), a.Star,
            a.Economy, a.Wealth, a.Conflict, a.Race, a.Uncharted, a.Abandoned, a.Pirate, bodies.PlanetCount, bodies.MoonCount);
    });

    private static readonly ConcurrentDictionary<ulong, ArtifactX.Core.Universe.SystemBodies> BodiesCache = new();

    /// <summary>The generator's bodies for the system an address is in (which are moons, and of what).</summary>
    public static ArtifactX.Core.Universe.SystemBodies Bodies(ulong address) => BodiesCache.GetOrAdd(SystemKey(address), key =>
    {
        var (code, galaxy) = Generator(key);
        return SystemGenerator.Bodies(code, galaxy);
    });

    /// <summary>The system's name: the one the save recorded or the player typed, else the generated one.</summary>
    public static string SystemName(ulong address) =>
        SystemNotesService.Get(SystemKey(address))?.SystemName is { Length: > 0 } recorded ? recorded : For(address).GeneratedName;

    /// <summary>The region of space this system is in (e.g. "Ocopad Conflux").</summary>
    public static string Region(ulong address) => For(address).Region;

    /// <summary>Formatted location line with System, Region, and Galaxy.</summary>
    public static string LocationWithRegion(ulong address, int galaxy, string? system = null)
    {
        string sys = system ?? SystemName(address);
        string reg = Region(address);
        string gal = ArtifactX.Core.NmsModels.NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
        return reg.Length > 0 ? $"{sys} · {reg} · {gal}" : $"{sys} · {gal}";
    }

    /// <summary>A planet's name: recorded by the save, else generated (planet index 1-based).</summary>
    public static string PlanetName(ulong address, int planetIndex)
    {
        ulong key = SystemKey(address);
        if (SystemNotesService.Get(key)?.PlanetNames.TryGetValue(planetIndex.ToString(CultureInfo.InvariantCulture), out var recorded) == true
            && recorded.Length > 0)
            return recorded;
        return GeneratedPlanetName(address, planetIndex);
    }

    /// <summary>The planet's original name, from its seed - what it's called
    /// until its discoverer renames it (the new name lives on the game's
    /// servers, not in the save).</summary>
    public static string GeneratedPlanetName(ulong address, int planetIndex)
    {
        ulong key = SystemKey(address);
        var names = PlanetNamesCache.GetOrAdd(key, k =>
        {
            var (code, galaxy) = Generator(k);
            var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
            return Array.ConvertAll(seeds, SystemGenerator.PlanetName);
        });
        return planetIndex >= 1 && planetIndex <= names.Length ? names[planetIndex - 1] : $"Planet {planetIndex}";
    }

    /// <summary>Wealth label: the player's note if they took one, else generated.</summary>
    public static string Wealth(ulong address) =>
        SystemNotesService.Get(SystemKey(address))?.Wealth is { Length: > 0 } w ? SystemNotesService.CategoryOf("Wealth", w) : For(address).WealthLabel;

    public static string Economy(ulong address) =>
        SystemNotesService.Get(SystemKey(address))?.Economy is { Length: > 0 } e ? SystemNotesService.CategoryOf("Economy", e) : For(address).EconomyLabel;

    public static string Conflict(ulong address) =>
        SystemNotesService.Get(SystemKey(address))?.Conflict is { Length: > 0 } c ? SystemNotesService.CategoryOf("Conflict", c) : For(address).ConflictLabel;

    /// <summary>Stars of wealth (1-3, 0 when none), note first.</summary>
    public static int WealthStars(ulong address)
    {
        var note = SystemNotesService.Get(SystemKey(address));
        if (note?.Wealth is { Length: > 0 } w) return SystemNotesService.CategoryOf("Wealth", w).Count(ch => ch == '★');
        var facts = For(address);
        return facts.Uncharted ? 0 : facts.Wealth;
    }

    /// <summary>One short line for cards, wealth first since that's what people scan
    /// for: "★★★ · Advanced Materials · Gek · Medium conflict · Yellow star".</summary>
    public static string Compact(ulong address)
    {
        var f = For(address);
        if (f.Uncharted) return $"Uncharted (no station) \u00B7 {f.StarLabel} star";
        int stars = WealthStars(address);
        var parts = new System.Collections.Generic.List<string>
        {
            stars > 0 ? new string('\u2605', stars) : "\u2606",
            Economy(address),
            f.Abandoned ? "Abandoned" : RaceLabel(address),
            $"{Conflict(address)} conflict",
            $"{(SystemNotesService.Get(SystemKey(address))?.StarColour is { Length: > 0 } c ? c : f.StarLabel)} star",
        };
        if (f.Pirate) parts.Add("Pirate");
        return string.Join(" \u00B7 ", parts);
    }

    public static string RaceLabel(ulong address) =>
        SystemNotesService.Get(SystemKey(address))?.Race is { Length: > 0 } r ? r : For(address).RaceLabel;

    /// <summary>"Yellow star · Gek · Advanced Materials · High ★★★ wealth · Medium conflict".</summary>
    public static string Summary(ulong address)
    {
        var f = For(address);
        if (f.Uncharted) return $"{f.StarLabel} star · Uncharted - no race, economy or station";
        // Pirate systems' own words read as-is ("Black Market", "Pirate Controlled"), not "... wealth/conflict".
        string wealth = Wealth(address), conflict = Conflict(address);
        bool pirateConflict = conflict.Contains("Pirate", System.StringComparison.OrdinalIgnoreCase);
        var parts = new System.Collections.Generic.List<string> { $"{f.StarLabel} star", f.RaceLabel, Economy(address),
            wealth == "Black Market" ? wealth : $"{wealth} wealth", pirateConflict ? conflict : $"{conflict} conflict" };
        if (f.Pirate && !pirateConflict) parts.Add("Pirate-controlled");
        return string.Join(" · ", parts);
    }
}
