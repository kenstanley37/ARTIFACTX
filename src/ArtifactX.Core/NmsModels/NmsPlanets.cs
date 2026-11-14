using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using ArtifactX.Core.Universe;

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
    public string Classification(string? descriptor, string ringSuffix = " with rings", bool? isMoon = null) =>
        NmsPlanets.FormatClassification(BiomeIndex, descriptor, HasRings, ringSuffix, isMoon);

    /// <summary>Where this planet was read from, e.g. "Slot 3" - set by
    /// callers that merge several saves; empty for the loaded save.</summary>
    public string Source { get; init; } = "";

    /// <summary>Not in the save: a body nobody has discovered here yet, its type worked
    /// out by the game's own generator (PlanetGenerator).</summary>
    public bool Predicted { get; init; }
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
///            order); bit 16 is Extreme Hazard / Extreme Weather.
///            (Planetary rings are NOT saved in flags; the solar layout picks
///            one primary planet per system - see PlanetRings).
///   ksu      discoverer name (V?:), owner id (f5Q) and time (3I1).
/// The discovery list also holds other players' discoveries downloaded by the
/// game; those have an empty f5Q, which is how "discovered on this save" is
/// told apart (checked 2026-10-11: every non-empty f5Q equals the record's own
/// K7E, under all of the player's names and accounts).
///
/// Biome: checked against a real 6-planet system (3 green, 2 brown, 1 red in
/// the game's Discoveries screen).
/// Ring determination: PlanetRings (one primary planet per system, drawn on the
/// solar layout stream; moons never have rings). Confirmed 2026-11-08 on
/// Apolok-Sats, where the older child-seed-22 / 30% test ringed the wrong three planets.
/// Bit 16 in bEr[1] was previously assumed to be rings, but cross-save analysis
/// of 2,069 records proved it is Extreme Weather/Hazard (only occurs on hazard biomes).
/// </summary>
public static class NmsPlanets
{
    /// <summary>Display names in GcBiomeType order (libMBIN). Weird is the
    /// game's "exotic" world; Red/Green/Blue are anomalies (see BiomeGroup);
    /// Test never appears in real saves.</summary>
    private static readonly string[] TypeNames =
    {
        "Lush", "Toxic", "Scorched", "Radioactive", "Frozen", "Barren", "Dead", "Exotic",
        "Red Anomaly", "Green Anomaly", "Blue Anomaly", "Unknown", "Swamp", "Volcanic", "Water World", "Gas Giant",
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

    /// <summary>Broad biome classification group. Red, Green and Blue are only the game's
    /// internal names: it shows these worlds as anomalies - Aukel (Green, grey and blue
    /// to look at) is a "Planetary Anomaly" in the game's Discoveries (2026-10-21) - so
    /// they're grouped as Anomaly rather than by a colour the player never sees.</summary>
    /// <summary>The worlds the game's fishing text calls "haunted homeworlds" (UI_FISH_BIOME_ODD_DESC, the ODD fish's
    /// GcFishTable biomes 7-10): Exotic (Weird) and the Red, Green and Blue anomalies. Not a planet setting of its own.</summary>
    public static bool IsHaunted(int biome) => biome is 7 or 8 or 9 or 10;

    /// <summary>GcBiomeSubType.Infested (24): the infested variant of a planet type, whose biome files place Whispering
    /// Eggs and the small ground worms (objects/rare/infestation.mbin, build 185034). <see cref="PlanetType.SubType"/>
    /// holds it for a generated body. Checked 2026-11-13 on the 2,796 bodies of the Slot 8 region (galaxy 246): the 70
    /// of subtype 24 carry only the game's infested descriptors ("Infested" 23, "Boiling Doom", "Caustic Nightmare",
    /// "Mutated", "Worm-ridden", "The Nest"...), none of which any other body has; "Corrupted" (4) also occurs elsewhere.</summary>
    public const int InfestedSubType = 24;

    /// <summary>What players search the haunted worlds by.</summary>
    public const string HauntedName = "Exotic or anomaly (haunted)";

    /// <summary>A list of biomes in words ("Lush, Swamp"); the four haunted biomes together read as
    /// <see cref="HauntedName"/>.</summary>
    public static string TypeList(IEnumerable<int> biomes, string separator = ", ")
    {
        var list = biomes.ToList();
        if (list.Count > 1 && list.All(IsHaunted) && Enumerable.Range(7, 4).All(list.Contains)) return HauntedName;
        return string.Join(separator, list.Select(TypeName));
    }

    public static string BiomeGroup(int biome) => biome switch
    {
        8 or 9 or 10 => "Anomaly",
        _ => TypeName(biome),
    };

    /// <summary>Dual classification format combining specific planetary descriptor with broad biome group
    /// (e.g. "Tropical Moon (Lush)" or "Paradise Planet (Lush)"). If no descriptor is recorded,
    /// cleanly falls back to the broad biome group, indicating moon status if known.</summary>
    public static string FormatClassification(int biome, string? descriptor, bool hasRings = false, string ringSuffix = " with rings", bool? isMoon = null)
    {
        string group = BiomeGroup(biome);
        string typeName = TypeName(biome);
        string baseText;

        if (string.IsNullOrWhiteSpace(descriptor))
        {
            if (isMoon == true)
                baseText = $"{group} Moon";
            else if (isMoon == false)
                baseText = $"{group} Planet";
            else
                baseText = group;
        }
        else
        {
            string desc = descriptor.Trim();

            // If descriptor doesn't specify Moon or Planet, and isMoon is known, align it
            if (isMoon == true && !desc.EndsWith("Moon", StringComparison.OrdinalIgnoreCase) && !desc.EndsWith("Anomaly", StringComparison.OrdinalIgnoreCase))
            {
                if (desc.EndsWith("Planet", StringComparison.OrdinalIgnoreCase))
                    desc = desc[..^6].TrimEnd() + " Moon";
                else
                    desc = $"{desc} Moon";
            }
            else if (isMoon == false && !desc.EndsWith("Planet", StringComparison.OrdinalIgnoreCase) && !desc.EndsWith("Moon", StringComparison.OrdinalIgnoreCase) && !desc.EndsWith("Anomaly", StringComparison.OrdinalIgnoreCase))
            {
                desc = $"{desc} Planet";
            }

            // Check if desc already mentions the group/type
            bool alreadyHasGroup = string.Equals(desc, group, StringComparison.OrdinalIgnoreCase)
                || string.Equals(desc, typeName, StringComparison.OrdinalIgnoreCase)
                || desc.StartsWith(group + " ", StringComparison.OrdinalIgnoreCase)
                || desc.StartsWith(typeName + " ", StringComparison.OrdinalIgnoreCase)
                || desc.EndsWith($"({group})", StringComparison.OrdinalIgnoreCase)
                || desc.EndsWith($" {group}", StringComparison.OrdinalIgnoreCase);

            if (alreadyHasGroup)
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
    /// on every slot, bEr's flags only ever use the biome bits, bit 16 = extreme
    /// hazard and bit 17 on 3 Frozen/Scorched worlds), and the system generator doesn't
    /// cover terrain, so for every other type water is unknown.</summary>
    public static bool AlwaysHasWater(int biome) => biome is 12 or 14;

    /// <summary>Every distinct planet in the discovery list, newest record wins
    /// when a planet appears twice.</summary>
    public static List<KnownPlanet> ReadAll(JArray? discoveries)
    {
        var byAddress = new Dictionary<ulong, KnownPlanet>();
        if (discoveries is null) return new();

        var systemBodiesCache = new Dictionary<ulong, SystemBodies>();
        SystemBodies GetBodies(ulong addr)
        {
            ulong sysAddr = NmsSpaceStationPaths.ToSystemAddress(addr);
            if (!systemBodiesCache.TryGetValue(sysAddr, out var bodies))
            {
                ulong portal = (((addr >> 40) & 0xFFF) << 32) | (addr & 0xFFFFFFFF);
                int gal = (int)((addr >> 32) & 0xFF);
                bodies = SystemGenerator.Bodies(portal, gal);
                systemBodiesCache[sysAddr] = bodies;
            }
            return bodies;
        }

        foreach (var record in discoveries.OfType<JObject>())
        {
            if (record["8P3"] is not JObject key || key["<Dn"]?.Value<string>() != "Planet") continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(key["5L6"]) is not ulong address) continue;
            if (key["bEr"] is not JArray seedAndFlags || seedAndFlags.Count < 2) continue;

            long flags = seedAndFlags[1].Type == JTokenType.Integer ? seedAndFlags[1].Value<long>() : 0;
            var owner = record["ksu"] as JObject;

            int planetIndex = (int)((address >> 52) & 0xF);
            var bodies = GetBodies(address);
            bool isMoon = bodies.IsMoon(planetIndex);

            bool hasRings = !isMoon && PlanetRings.HasRings(
                (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF), (int)((address >> 32) & 0xFF), planetIndex);

            var planet = new KnownPlanet(
                address,
                NmsSpaceStationPaths.GalaxyNumber(address),
                planetIndex,
                (int)(flags & 0xFFFF),
                hasRings,
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

    /// <summary>One planet's numeric ^PLANET_STATS values (stat groups ":rc", the planet in
    /// "2Ak"; each stat in "gUR" is an id "b2n" with an int ">MX.>vs" or a float ">MX.eoL").
    /// ^TIME / ^DIST_WALKED are seconds / metres (Cholton: 199 and 85 after a few minutes
    /// on foot).</summary>
    public static Dictionary<string, double> PlanetStats(JArray? statGroups, ulong planetAddress)
    {
        var result = new Dictionary<string, double>();
        var group = statGroups?.OfType<JObject>().FirstOrDefault(g =>
            g[":rc"]?.Value<string>() == "^PLANET_STATS" && NmsSpaceStationPaths.ParsePackedAddress(g["2Ak"]) == planetAddress);
        if (group?["gUR"] is not JArray stats) return result;
        foreach (var stat in stats.OfType<JObject>())
        {
            string? id = stat["b2n"]?.Value<string>();
            var value = stat[">MX"]?[">vs"] ?? stat[">MX"]?["eoL"];
            if (id is not null && value is not null && value.Type is JTokenType.Integer or JTokenType.Float)
                result[id] = value.Value<double>();
        }
        return result;
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
