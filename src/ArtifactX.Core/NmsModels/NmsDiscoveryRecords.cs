using System.Globalization;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Writing discovery records the way the game writes a discovery the player hasn't uploaded yet (#43). From the
/// owner's controlled Slot 8 diff of 2026-11-18 (save15.hg -> save16.hg: one planet, creature, plant and mineral
/// discovered on Egha, not uploaded), each discovery adds:
///   - a record to fDu/ETO/OsQ/?fB: {8P3: {5L6 planet address, &lt;Dn kind, bEr key}, ksu: {3I1 unix time, D6b "",
///     K7E player id, V?: player name, f5Q player id}, q9a: {}} - an uploaded record has D6b "ST", =wD and a server
///     signature B2h instead, so a local one simply lacks them;
///   - an entry to the upload queue fDu/ETO/brV: {8P3 the same key, kVv unix time};
///   - fDu/ETO/fgt = the record count;
///   - +1 to vLc/6f=/gUR ^GLOBAL_STATS DISC_PLANETS / DISC_CREATURES / DISC_FLORA / DISC_MINERALS and to the same stat
///     of the planet's ^PLANET_STATS group (created on its first discovery).
/// The game also raised per-kind stats (DISC_CRE_AIR, DIS_FLORA_BARR, DISC_P_DUST) and paid scan units; neither is
/// written here (the per-kind mapping is unknown; the owner chose no units, 2026-11-19).
/// Numbers above 2^53 are written as "0x..." hex strings, smaller ones as plain numbers, as the game does.
/// </summary>
public static class NmsDiscoveryRecords
{
    public static readonly string[] RecordsPath = { "fDu", "ETO", "OsQ", "?fB" };
    public static readonly string[] UploadQueuePath = { "fDu", "ETO", "brV" };
    public static readonly string[] CountPath = { "fDu", "ETO", "fgt" };
    public static readonly string[] PlayerIdPath = { "<h0", "F=J", "0", "K7E" };

    private const string PlanetStatsGroupId = "^PLANET_STATS";
    private const string GlobalStatsGroupId = "^GLOBAL_STATS";

    /// <summary>A ^PLANET_STATS group's stats in the order the game created Egha's on its first discovery
    /// (2026-11-18 diff).</summary>
    private static readonly string[] PlanetStatIdsInGameOrder =
    [
        "^ALL_CREATURES", "^DEATHS", "^DISC_MINERALS", "^DISC_PLANETS", "^DIST_SWAM", "^DISC_CREATURES", "^TIME",
        "^DRONES_KILLED", "^SENTINEL_KILLS", "^DISC_WAYPOINTS", "^QUADS_KILLED", "^RES_EXTRACTED", "^PREDS_KILLED",
        "^PB_CHALL_WINS", "^PARTS_PLACED", "^CREATURES_KILL", "^ALIENS_MET", "^DIST_WALKED", "^WORDS_LEARNT",
        "^S12_RESEARCH", "^DISC_FLORA", "^WALKERS_KILLED",
    ];

    /// <summary>The stat each kind raises.</summary>
    public static string? StatFor(string kind) => kind switch
    {
        "Planet" => "^DISC_PLANETS",
        "Animal" => "^DISC_CREATURES",
        "Flora" => "^DISC_FLORA",
        "Mineral" => "^DISC_MINERALS",
        _ => null,
    };

    /// <summary>A number as the save writes it.</summary>
    public static JToken Number(ulong value) => value < (1UL << 53) ? new JValue(value) : new JValue("0x" + value.ToString("X"));

    /// <summary>A number read from the save (a plain number or a "0x..." string).</summary>
    public static ulong? ReadNumber(JToken? token)
    {
        if (token is null) return null;
        if (token.Type == JTokenType.Integer) return unchecked((ulong)token.Value<long>());
        string? text = token.Type == JTokenType.String ? token.Value<string>() : null;
        if (text is not null && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex))
            return hex;
        return null;
    }

    /// <summary>What makes two records the same discovery: kind, planet and key. A creature is known by its seed alone
    /// (the game writes its key with or without a fifth part, never twice for one creature).</summary>
    public static string Identity(string kind, ulong address, IReadOnlyList<ulong> ber) =>
        kind == "Animal" && ber.Count > 0 ? $"Animal|{address:X}|{ber[0]:X}" : $"{kind}|{address:X}|{string.Join(",", ber.Select(b => b.ToString("X")))}";

    /// <summary>A save record's <see cref="Identity(string, ulong, IReadOnlyList{ulong})"/>, or null when it isn't readable.</summary>
    public static string? Identity(JObject record)
    {
        if (record["8P3"] is not JObject key || key["<Dn"]?.Value<string>() is not { } kind || ReadNumber(key["5L6"]) is not ulong address
            || key["bEr"] is not JArray ber)
            return null;
        var values = new List<ulong>();
        foreach (var token in ber)
        {
            if (ReadNumber(token) is not ulong v) return null;
            values.Add(v);
        }
        return Identity(kind, address, values);
    }

    public static JObject Key(DiscoveryRecordKey key) => new()
    {
        ["5L6"] = Number(key.Address),
        ["<Dn"] = key.Kind,
        ["bEr"] = new JArray(key.Ber.Select(Number)),
    };

    public static JObject CreateRecord(DiscoveryRecordKey key, long unixTime, string playerId, string playerName) => new()
    {
        ["8P3"] = Key(key),
        ["ksu"] = new JObject { ["3I1"] = unixTime, ["D6b"] = "", ["K7E"] = playerId, ["V?:"] = playerName, ["f5Q"] = playerId },
        ["q9a"] = new JObject(),
    };

    public static JObject CreateUploadEntry(DiscoveryRecordKey key, long unixTime) => new() { ["8P3"] = Key(key), ["kVv"] = unixTime };

    /// <summary>The keys the save doesn't hold yet (by <see cref="Identity(JObject)"/>), each once, in order.</summary>
    public static List<DiscoveryRecordKey> Missing(JArray? records, IEnumerable<DiscoveryRecordKey> keys)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records?.OfType<JObject>() ?? [])
            if (Identity(record) is { } id) known.Add(id);
        return keys.Where(k => known.Add(Identity(k.Kind, k.Address, k.Ber))).ToList();
    }

    /// <summary>The stat groups with <paramref name="added"/> counted (a copy; the input is untouched): +1 per record to
    /// its kind's stat in ^GLOBAL_STATS and in its planet's ^PLANET_STATS group, created when missing.</summary>
    public static JArray CountInStats(JArray groups, IReadOnlyList<DiscoveryRecordKey> added)
    {
        var result = (JArray)groups.DeepClone();
        var global = result.OfType<JObject>().FirstOrDefault(g => g[":rc"]?.Value<string>() == GlobalStatsGroupId);
        foreach (var byPlanet in added.GroupBy(k => k.Address))
        {
            var planet = result.OfType<JObject>().FirstOrDefault(g => g[":rc"]?.Value<string>() == PlanetStatsGroupId
                && ReadNumber(g["2Ak"]) == byPlanet.Key);
            if (planet is null)
            {
                planet = new JObject
                {
                    [":rc"] = PlanetStatsGroupId,
                    ["2Ak"] = Number(byPlanet.Key),
                    ["gUR"] = new JArray(PlanetStatIdsInGameOrder.Select(id => new JObject { ["b2n"] = id, [">MX"] = new JObject() })),
                };
                result.Add(planet);
            }
            foreach (var byStat in byPlanet.GroupBy(k => StatFor(k.Kind)))
            {
                if (byStat.Key is not { } stat) continue;
                int count = byStat.Count();
                NmsSpaceStationPaths.WriteStat(planet, stat, NmsSpaceStationPaths.ReadStat(planet, stat) + count);
                if (global is not null) NmsSpaceStationPaths.WriteStat(global, stat, NmsSpaceStationPaths.ReadStat(global, stat) + count);
            }
        }
        return result;
    }
}
