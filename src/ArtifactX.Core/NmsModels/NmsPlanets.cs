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

    /// <summary>Where this planet was read from, e.g. "Slot 3" - set by
    /// callers that merge several saves; empty for the loaded save.</summary>
    public string Source { get; init; } = "";
}

/// <summary>Per-planet discovery tallies, counted from the discovery records
/// (the game itself counts records, not the ^PLANET_STATS counters - confirmed
/// 2026-10-11: setting Cholton's ^DISC_CREATURES to 6 changed nothing in-game).</summary>
public sealed record PlanetDiscoveryCounts(int Fauna, int Flora, int Minerals);

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

    /// <summary>Real planet types that can actually occur (excludes Test).</summary>
    public static IEnumerable<int> SelectableTypes => Enumerable.Range(0, TypeNames.Length).Where(i => i != 11);

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

    /// <summary>Fauna/flora/mineral record counts per planet address.</summary>
    public static Dictionary<ulong, PlanetDiscoveryCounts> CountDiscoveries(JArray? discoveries)
    {
        var tallies = new Dictionary<ulong, (int Fauna, int Flora, int Minerals)>();
        if (discoveries is null) return new();

        foreach (var record in discoveries.OfType<JObject>())
        {
            string? type = record["8P3"]?["<Dn"]?.Value<string>();
            if (type is not ("Animal" or "Flora" or "Mineral")) continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(record["8P3"]?["5L6"]) is not ulong address) continue;

            tallies.TryGetValue(address, out var t);
            tallies[address] = type switch
            {
                "Animal" => (t.Fauna + 1, t.Flora, t.Minerals),
                "Flora" => (t.Fauna, t.Flora + 1, t.Minerals),
                _ => (t.Fauna, t.Flora, t.Minerals + 1),
            };
        }

        return tallies.ToDictionary(kv => kv.Key, kv => new PlanetDiscoveryCounts(kv.Value.Fauna, kv.Value.Flora, kv.Value.Minerals));
    }
}
