using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>One planet the save knows about, read from its discovery record.</summary>
public sealed record KnownPlanet(
    ulong Address,
    int Galaxy,
    int PlanetIndex,
    int BiomeIndex,
    bool HasRings,
    string Discoverer,
    bool DiscoveredHere,
    long DiscoveredUnixSeconds)
{
    public ulong SystemAddress => NmsSpaceStationPaths.ToSystemAddress(Address);
    public string PortalCode => NmsSpaceStationPaths.PortalAddress(Address, PlanetIndex);
    public string TypeName => NmsPlanets.TypeName(BiomeIndex);
    public string BiomeGroup => NmsPlanets.BiomeGroup(BiomeIndex);
    public string Classification(string? descriptor, string ringSuffix = " with rings") =>
        NmsPlanets.FormatClassification(BiomeIndex, descriptor, HasRings, ringSuffix);

    /// <summary>Where this planet was read from, e.g. "Slot 3" - set by
    /// callers that merge several saves; empty for the loaded save.</summary>
    public string Source { get; init; } = "";
}

/// <summary>One star system the save knows about: every discovery record
/// (planet, creature, plant, mineral, the system itself...) carries its
/// system's full address, so a system is known from any one of them.</summary>
public sealed record KnownSystem(ulong SystemAddress, int Galaxy, int PlanetCount, bool DiscoveredHere, long LatestUnixSeconds)
{
    public double DistanceToCore => NmsPlanets.DistanceToCoreLightYears(SystemAddress);
    public string Source { get; init; } = "";
}

/// <summary>Per-planet discovery tallies, counted from the discovery records
/// (the game itself counts records, not the ^PLANET_STATS counters - confirmed
/// 2026-10-11: setting Cholton's ^DISC_CREATURES to 6 changed nothing in-game).
/// Waypoints are the save's "Sector" records - one per waypoint the game lists
/// (Ippianes III: 6 records, game shows "Waypoints 6 Discovered", 2026-10-13).
/// Fauna/Flora/Minerals count only the records YOU made (ksu.f5Q set - the
/// player's own online id; empty on other players' records, checked across
/// ~20k records). The save also caches other players' discoveries and the game
/// adds only some of those to its counts - Ippianes III, 2026-10-14: records
/// 12 / 22 / 19, yours 3 / 12 / 6, game 11 / 12 / 8 - and which ones can't be
/// told from the save, so ArtifactX shows yours.</summary>
public sealed record PlanetDiscoveryCounts(int Fauna, int Flora, int Minerals, int Waypoints = 0);

/// <summary>
/// Planets as the save records them. The save stores very little about a
/// planet: everything shown in-game (name, weather, sentinels, resources,
/// species totals, star colour, economy, conflict) is regenerated from the
/// address seed on every visit and is NOT in the file. What IS stored, on
/// each "Planet" record in the discovery list (fDu/ETO/OsQ/?fB):
///   8P3.5L6  packed address - its top nibble is the planet's index (1-6)
///   8P3.bEr  [seed, flags]: flags' low 16 bits are the biome (GcBiomeType
///            order); bit 16 set on ringed planets.
///   ksu      discoverer name (V?:), owner id (f5Q) and time (3I1).
/// The discovery list also holds other players' discoveries downloaded by the
/// game; those have an empty f5Q, which is how "discovered on this save" is
/// told apart (checked 2026-10-11: every non-empty f5Q equals the record's own
/// K7E, under all of the player's names and accounts).
///
/// Biome: checked against a real 6-planet system (3 green, 2 brown, 1 red in
/// the game's Discoveries screen) and planet 1 = Cholton, a ringed Paradise
/// world. Ring bit: one confirmed ringed planet so far, ~10% of all planets.
/// </summary>
public static class NmsPlanets
{
    /// <summary>Display names in GcBiomeType order (libMBIN). Weird is the
    /// game's "exotic" world; Red/Green/Blue are the colour-shifted worlds;
    /// Test never appears in real saves.</summary>
    private static readonly string[] TypeNames =
    {
        "Lush", "Toxic", "Scorched", "Radioactive", "Frozen", "Barren", "Dead", "Exotic",
        "Red", "Green", "Blue", "Unknown", "Swamp", "Volcanic", "Water World", "Gas Giant",
    };

    /// <summary>Swatch colour per type (#RRGGBB) for the planet drawings.</summary>
    private static readonly string[] TypeColours =
    {
        "#5DBB63", "#A9C33B", "#E07A3A", "#D6CF3C", "#9CD4F2", "#B49B74", "#8C8C8C", "#C25DDD",
        "#D9474F", "#39C46A", "#3D8BE0", "#777777", "#6E8F4E", "#FF5A1F", "#2FA8D8", "#D9B26F",
    };

    public static int TypeCount => TypeNames.Length;

    public static string TypeName(int biome) =>
        biome >= 0 && biome < TypeNames.Length ? TypeNames[biome] : "Unknown";

    public static string TypeColour(int biome) =>
        biome >= 0 && biome < TypeColours.Length ? TypeColours[biome] : "#777777";

    /// <summary>Broad biome classification group (e.g. Red / Exotic, Green / Exotic, Blue / Exotic for chromatic worlds).</summary>
    public static string BiomeGroup(int biome) => biome switch
    {
        8 => "Red / Exotic",
        9 => "Green / Exotic",
        10 => "Blue / Exotic",
        _ => TypeName(biome),
    };

    /// <summary>Dual classification format combining specific planetary descriptor with broad biome group
    /// (e.g. "Crimson Planet (Red / Exotic)" or "Paradise Planet (Lush)"). If no descriptor is recorded,
    /// cleanly falls back to the broad biome group.</summary>
    public static string FormatClassification(int biome, string? descriptor, bool hasRings = false, string ringSuffix = " with rings")
    {
        string group = BiomeGroup(biome);
        string baseText;
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            baseText = group;
        }
        else
        {
            string desc = descriptor.Trim();
            if (string.Equals(desc, group, StringComparison.OrdinalIgnoreCase)
                || string.Equals(desc, TypeName(biome), StringComparison.OrdinalIgnoreCase)
                || desc.EndsWith($"({group})", StringComparison.OrdinalIgnoreCase))
            {
                baseText = desc;
            }
            else
            {
                baseText = $"{desc} ({group})";
            }
        }

        return hasRings ? $"{baseText}{ringSuffix}" : baseText;
    }

    /// <summary>Real planet types that can actually occur (excludes Test).</summary>
    public static IEnumerable<int> SelectableTypes => Enumerable.Range(0, TypeNames.Length).Where(i => i != 11);

    /// <summary>Types that always have water: Swamp and Water World. The save has
    /// no water flag for other planets (checked 2026-10-12: across 2,612 planets
    /// on every slot, bEr's flags only ever use the biome bits, bit 16 = rings
    /// and bit 17 on 3 Frozen/Scorched worlds), and the system generator doesn't
    /// cover terrain, so for every other type water is unknown.</summary>
    public static bool AlwaysHasWater(int biome) => biome is 12 or 14;

    /// <summary>Every distinct planet in the discovery list, newest record wins
    /// when a planet appears twice.</summary>
    public static List<KnownPlanet> ReadAll(JArray? discoveries)
    {
        var byAddress = new Dictionary<ulong, KnownPlanet>();
        if (discoveries is null) return new();

        foreach (var record in discoveries.OfType<JObject>())
        {
            if (record["8P3"] is not JObject key || key["<Dn"]?.Value<string>() != "Planet") continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(key["5L6"]) is not ulong address) continue;
            if (key["bEr"] is not JArray seedAndFlags || seedAndFlags.Count < 2) continue;

            long flags = seedAndFlags[1].Type == JTokenType.Integer ? seedAndFlags[1].Value<long>() : 0;
            var owner = record["ksu"] as JObject;

            var planet = new KnownPlanet(
                address,
                NmsSpaceStationPaths.GalaxyNumber(address),
                (int)((address >> 52) & 0xF),
                (int)(flags & 0xFFFF),
                ((flags >> 16) & 1) == 1,
                owner?["V?:"]?.Value<string>() ?? "",
                !string.IsNullOrEmpty(owner?["f5Q"]?.Value<string>()),
                owner?["3I1"]?.Value<long>() ?? 0);

            if (!byAddress.TryGetValue(address, out var existing) || planet.DiscoveredUnixSeconds > existing.DiscoveredUnixSeconds)
                byAddress[address] = planet;
        }

        return byAddress.Values.ToList();
    }

    /// <summary>Approximate distance in light-years between two systems, or
    /// null when they're in different galaxies. Uses the community rule of
    /// 400 ly per region step along each axis (the address's X/Z are signed
    /// 12-bit, Y signed 8-bit); systems in the same region read as 0.</summary>
    public static double? DistanceLightYears(ulong from, ulong to)
    {
        if (NmsSpaceStationPaths.GalaxyNumber(from) != NmsSpaceStationPaths.GalaxyNumber(to)) return null;

        static int Signed(ulong value, int bits) => (int)((long)(value << (64 - bits)) >> (64 - bits));
        static (int X, int Y, int Z) Region(ulong a) =>
            (Signed(a & 0xFFF, 12), Signed((a >> 24) & 0xFF, 8), Signed((a >> 12) & 0xFFF, 12));

        var (x1, y1, z1) = Region(from);
        var (x2, y2, z2) = Region(to);
        double dx = x2 - x1, dy = y2 - y1, dz = z2 - z1;
        return System.Math.Sqrt(dx * dx + dy * dy + dz * dz) * 400;
    }

    /// <summary>Approximate distance in light-years from a system to its
    /// galaxy's centre (region 0,0,0), using the same 400 ly-per-region rule.
    /// Checked 2026-10-11: 737,852 ly computed vs 737,935 ly shown on the
    /// Galaxy Map for the same system (0.01% off).</summary>
    public static double DistanceToCoreLightYears(ulong address)
    {
        static int Signed(ulong value, int bits) => (int)((long)(value << (64 - bits)) >> (64 - bits));
        double x = Signed(address & 0xFFF, 12), y = Signed((address >> 24) & 0xFF, 8), z = Signed((address >> 12) & 0xFFF, 12);
        return System.Math.Sqrt(x * x + y * y + z * z) * 400;
    }

    /// <summary>Every distinct system in the discovery list. Records with an
    /// all-zero address (no coordinates, no system index) are skipped - two
    /// such placeholder records exist in real saves.</summary>
    public static List<KnownSystem> ReadSystems(JArray? discoveries)
    {
        var bySystem = new Dictionary<ulong, (HashSet<int> Planets, bool Mine, long Latest)>();
        if (discoveries is null) return new();

        foreach (var record in discoveries.OfType<JObject>())
        {
            if (NmsSpaceStationPaths.ParsePackedAddress(record["8P3"]?["5L6"]) is not ulong address) continue;
            ulong system = NmsSpaceStationPaths.ToSystemAddress(address);
            if ((system & 0xFFF_0000_FFFF_FFFFUL) == 0) continue;

            bySystem.TryGetValue(system, out var entry);
            entry.Planets ??= new HashSet<int>();
            if (record["8P3"]?["<Dn"]?.Value<string>() == "Planet") entry.Planets.Add((int)((address >> 52) & 0xF));
            entry.Mine |= !string.IsNullOrEmpty(record["ksu"]?["f5Q"]?.Value<string>());
            entry.Latest = System.Math.Max(entry.Latest, record["ksu"]?["3I1"]?.Value<long>() ?? 0);
            bySystem[system] = entry;
        }

        return bySystem.Select(kv => new KnownSystem(kv.Key, NmsSpaceStationPaths.GalaxyNumber(kv.Key),
            kv.Value.Planets.Count, kv.Value.Mine, kv.Value.Latest)).ToList();
    }

    /// <summary>Names players gave planets and systems, as the game shows them: a discovery
    /// record's q9a/q5u ("Waterworld" for planet 6 of 04275B6DA013, 2026-10-18). The save
    /// holds the player's own renames and those of other players' discoveries it has cached.
    /// Planet = 0 for a system's own name.</summary>
    public static List<(ulong System, int Planet, string Name)> ReadCustomNames(JArray? discoveries)
    {
        var names = new List<(ulong, int, string)>();
        if (discoveries is null) return names;
        foreach (var record in discoveries.OfType<JObject>())
        {
            string? type = record["8P3"]?["<Dn"]?.Value<string>();
            if (type is not ("Planet" or "SolarSystem")) continue;
            if (record["q9a"]?["q5u"]?.Value<string>()?.Trim() is not { Length: > 0 } name) continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(record["8P3"]?["5L6"]) is not ulong address) continue;
            ulong system = NmsSpaceStationPaths.ToSystemAddress(address);
            if ((system & 0xFFF_0000_FFFF_FFFFUL) == 0) continue;
            names.Add((system, type == "Planet" ? (int)((address >> 52) & 0xF) : 0, name));
        }
        return names;
    }

    /// <summary>Fauna/flora/mineral record counts per planet address.</summary>
    public static Dictionary<ulong, PlanetDiscoveryCounts> CountDiscoveries(JArray? discoveries)
    {
        var tallies = new Dictionary<ulong, (int Fauna, int Flora, int Minerals, int Waypoints)>();
        if (discoveries is null) return new();

        foreach (var record in discoveries.OfType<JObject>())
        {
            string? type = record["8P3"]?["<Dn"]?.Value<string>();
            if (type is not ("Animal" or "Flora" or "Mineral" or "Sector")) continue;
            if (type != "Sector" && string.IsNullOrEmpty(record["ksu"]?["f5Q"]?.Value<string>())) continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(record["8P3"]?["5L6"]) is not ulong address) continue;

            tallies.TryGetValue(address, out var t);
            tallies[address] = type switch
            {
                "Animal" => (t.Fauna + 1, t.Flora, t.Minerals, t.Waypoints),
                "Flora" => (t.Fauna, t.Flora + 1, t.Minerals, t.Waypoints),
                "Sector" => (t.Fauna, t.Flora, t.Minerals, t.Waypoints + 1),
                _ => (t.Fauna, t.Flora, t.Minerals + 1, t.Waypoints),
            };
        }

        return tallies.ToDictionary(kv => kv.Key, kv => new PlanetDiscoveryCounts(kv.Value.Fauna, kv.Value.Flora, kv.Value.Minerals, kv.Value.Waypoints));
    }
}
