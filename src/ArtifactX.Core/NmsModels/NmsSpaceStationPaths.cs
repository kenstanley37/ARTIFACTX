using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Save paths and rules for space station directorship (claiming stations).
///
/// Mapping CONFIRMED 2026-10-09 against the user's real Slot 1 save (two
/// owned stations + one claim in progress) and the game's own Station Core
/// "Directorship Requirements" screen:
/// - Owned stations are F?0 base records with peI.DPp == "PlayerSpaceStationBase"
///   (NKm = station name, oZw = packed universe address).
///
/// Takeover CONFIRMED via a before/after diff of a real in-game claim
/// (Asbesla Station Alpha, 2026-10-09). The game wrote exactly:
///   1. a new F?0 record holding a fixed 260-object layout - 250/251 of its
///      objects sit at identical local positions in the two older owned
///      stations too, so it's a template, not per-station data. That fresh
///      record is embedded as PlayerSpaceStationTemplate.json (owner, address,
///      name and timestamps blanked; filled in at claim time);
///   2. ^GLOBAL_STATS ^STATIONS_OWNED + 1;
///   3. Units - 1,000,000,000 (the "donation") - NOT replicated by ArtifactX
///      (user decision: claiming shouldn't cost Units);
///   4. the station's nlG marker 6H2.8?J set to a 4 x uint32 value that is
///      identical on every owned station's marker (zeros when unowned) -
///      a per-player ownership stamp, copied from an existing owned marker.
/// Everything else in the diff was routine (session counters, an event log).
/// - Claim progress is per star system: a gUR group with :rc == ^SYSTEM_STATS
///   whose 2Ak is the system's packed address (planet nibble 0). The game
///   creates the group lazily (observed: Asbesla's appeared the moment Gek
///   standing first rose there) with all 7 stats below, unset ones as ">MX": {}.
/// - Station names come from nlG markers with iAF == "Spacestation"; their
///   address is a yhJ object (Iis = galaxy, oZw = coordinates), not a 2Ak.
///
/// Requirements (loc9 UI_CLAIM_STATION_MSG1-4 + in-game screen): %RACENAME%
/// standing 30, %FACTIONNAME% guild standing 15, salvage contracts 5, and a
/// 1,000,000,000 Units "donation". Which race and guild a system uses isn't
/// stored in the save (CONFIRMED to vary: Asbesla = Gek + Mercenaries, Higashi
/// = Vy'keen + Explorers, Africa = Vy'keen + Merchants), so claiming raises
/// all three races and guilds.
/// </summary>
public static class NmsSpaceStationPaths
{
    public static readonly string[] LocationDescriptionPath = { "vLc", "6f=", "n:R" };
    public static readonly string[] CurrentUniverseAddressPath = { "vLc", "6f=", "yhJ" };
    public static readonly string[] StatGroupsPath = { "vLc", "6f=", "gUR" };
    public static readonly string[] MarkersPath = { "vLc", "6f=", "nlG" };
    public static readonly string[] BasesPath = { "vLc", "6f=", "F?0" };

    public const string GlobalStatsGroupId = "^GLOBAL_STATS";
    public const string StationsOwnedStatId = "^STATIONS_OWNED";

    public const string PlayerStationBaseType = "PlayerSpaceStationBase";
    public const string StationMarkerType = "Spacestation";
    public const string SystemStatsGroupId = "^SYSTEM_STATS";

    public const string GekStandingStatId = "^TRA_STANDING";
    public const string KorvaxStandingStatId = "^EXP_STANDING";
    public const string VykeenStandingStatId = "^WAR_STANDING";
    public const string MerchantsGuildStatId = "^TGUILD_STAND";
    public const string ExplorersGuildStatId = "^EGUILD_STAND";
    public const string MercenariesGuildStatId = "^WGUILD_STAND";
    public const string SalvageContractsStatId = "^SP_POI_MISSIONS";

    public const int RaceStandingRequired = 30;
    public const int GuildStandingRequired = 15;
    public const int SalvageContractsRequired = 5;
    public const long UnitsDonationRequired = 1_000_000_000;

    public static readonly (string StatId, string Name)[] RaceStats =
    {
        (GekStandingStatId, "Gek"),
        (KorvaxStandingStatId, "Korvax"),
        (VykeenStandingStatId, "Vy'keen"),
    };

    public static readonly (string StatId, string Name)[] GuildStats =
    {
        (MerchantsGuildStatId, "Merchants Guild"),
        (ExplorersGuildStatId, "Explorers Guild"),
        (MercenariesGuildStatId, "Mercenaries Guild"),
    };

    /// <summary>Same order the game itself writes a fresh group in.</summary>
    private static readonly string[] SystemStatIdsInGameOrder =
    {
        VykeenStandingStatId, SalvageContractsStatId, MercenariesGuildStatId,
        ExplorersGuildStatId, GekStandingStatId, KorvaxStandingStatId, MerchantsGuildStatId,
    };

    public static string[] StatGroupPath(int groupIndex) =>
        new[] { "vLc", "6f=", "gUR", groupIndex.ToString(CultureInfo.InvariantCulture) };

    public static string[] BasePath(int baseIndex) =>
        new[] { "vLc", "6f=", "F?0", baseIndex.ToString(CultureInfo.InvariantCulture) };

    public static string[] MarkerOwnershipStampPath(int markerIndex) =>
        new[] { "vLc", "6f=", "nlG", markerIndex.ToString(CultureInfo.InvariantCulture), "6H2", "8?J" };

    private static readonly Lazy<string> _stationTemplateJson = new(() =>
    {
        using var stream = typeof(NmsSpaceStationPaths).Assembly
            .GetManifestResourceStream("ArtifactX.Core.PlayerSpaceStationTemplate.json")
            ?? throw new InvalidOperationException("PlayerSpaceStationTemplate.json resource missing.");
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>The furniture every claim comes with: the template's parts minus the station's
    /// own structure. Identical on every station - checked 2026-10-21 on the 25 untouched
    /// claims of a real save (Gek, Korvax and Vy'keen systems): the same 137 pieces at the same
    /// positions (station-local), matching this template exactly.</summary>
    public static IReadOnlyList<(string Id, double[] Position)> DefaultFurniture => _defaultFurniture.Value;

    private static readonly string[] StructureIds = { "^STACONNECT", "^BRIDGECONNECTOR", "^STATION_CORE", "^U_PARAGON" };

    private static readonly Lazy<List<(string Id, double[] Position)>> _defaultFurniture = new(() =>
        (JObject.Parse(_stationTemplateJson.Value)["@ZJ"] as JArray ?? new JArray()).OfType<JObject>()
            .Select(o => (Id: o["r<7"]?.Value<string>() ?? "", Position: o["wMC"]?.ToObject<double[]>() ?? Array.Empty<double>()))
            .Where(p => p.Id.Length > 0 && p.Position.Length == 3 && !StructureIds.Contains(p.Id))
            .ToList());

    /// <summary>The parts of a station that are its untouched default furniture - same kind at the
    /// same position (within 5 cm) as the claim template's, each default matched once. Moved
    /// pieces and anything the player built aren't included.</summary>
    public static HashSet<JObject> MatchDefaultFurniture(JArray parts)
    {
        var unused = DefaultFurniture.ToList();
        var matched = new HashSet<JObject>(ReferenceEqualityComparer.Instance);
        foreach (var part in parts.OfType<JObject>())
        {
            string id = part["r<7"]?.Value<string>() ?? "";
            if (part["wMC"] is not JArray { Count: 3 } at) continue;
            int i = unused.FindIndex(d => d.Id == id
                && Math.Abs(d.Position[0] - at[0]!.Value<double>()) < 0.05
                && Math.Abs(d.Position[1] - at[1]!.Value<double>()) < 0.05
                && Math.Abs(d.Position[2] - at[2]!.Value<double>()) < 0.05);
            if (i < 0) continue;
            matched.Add(part);
            unused.RemoveAt(i);
        }
        return matched;
    }

    /// <summary>Builds the F?0 record the game writes on takeover, from the
    /// embedded template. <paramref name="owner"/> is a 3?K owner block copied
    /// from one of the player's existing bases (Steam id, username, platform).</summary>
    public static JObject CreateClaimedStationRecord(ulong systemAddress, string stationName, JObject owner, long unixNow)
    {
        var record = JObject.Parse(_stationTemplateJson.Value);
        record["oZw"] = systemAddress;
        record["NKm"] = stationName;
        record["wx7"] = unixNow;

        var ownerCopy = (JObject)owner.DeepClone();
        ownerCopy["3I1"] = unixNow;
        record["3?K"] = ownerCopy;

        if (record["@ZJ"] is JArray objects)
        {
            foreach (var obj in objects.OfType<JObject>())
                obj["b1:"] = unixNow;
        }

        return record;
    }

    /// <summary>Standard NMS universe address layout:
    /// [planet:4][system:12][galaxy:8][Y:8][Z:12][X:12]. Verified against
    /// F?0 oZw and ^SYSTEM_STATS 2Ak values (e.g. Oskrem-Tat = 0x1D09E8F80732).</summary>
    public static ulong PackAddress(int galaxy, int x, int y, int z, int systemIndex, int planet = 0) =>
        ((ulong)(uint)(planet & 0xF) << 52)
        | ((ulong)(uint)(systemIndex & 0xFFF) << 40)
        | ((ulong)(uint)(galaxy & 0xFF) << 32)
        | ((ulong)(uint)(y & 0xFF) << 24)
        | ((ulong)(uint)(z & 0xFFF) << 12)
        | (ulong)(uint)(x & 0xFFF);

    /// <summary>Discovery records (fDu/ETO/OsQ/?fB): each entry's 8P3.5L6 is a
    /// packed address, 8P3.&lt;Dn its type (SolarSystem/Planet/Animal/...),
    /// ksu.3I1 the discovery time and ksu.V?: the discoverer's name.</summary>
    public static readonly string[] DiscoveriesPath = { "fDu", "ETO", "OsQ", "?fB" };

    /// <summary>1-based galaxy number, as the game counts them (Euclid = 1) -
    /// see NmsGalaxyNames for the matching name.</summary>
    public static int GalaxyNumber(ulong address) => (int)((address >> 32) & 0xFF) + 1;

    /// <summary>12-glyph portal address as hex digits: P SSS YY ZZZ XXX. The
    /// coordinate digits are the address's own stored bits (portal X equals
    /// galactic X + 0x801, which wraps back to the raw 12-bit value).</summary>
    public static string PortalAddress(ulong address, int planet = 1) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{planet & 0xF:X1}{(address >> 40) & 0xFFF:X3}{(address >> 24) & 0xFF:X2}{(address >> 12) & 0xFFF:X3}{address & 0xFFF:X3}");

    /// <summary>Drops the planet nibble so any address in a system compares
    /// equal to that system's ^SYSTEM_STATS key.</summary>
    public static ulong ToSystemAddress(ulong address) => address & ~(0xFUL << 52);

    /// <summary>Reads a yhJ-shaped object ({ Iis, oZw: { dZj, IyE, uXE, vby, jsv } })
    /// as a system address, or null if it's missing.</summary>
    public static ulong? ReadSystemAddress(JToken? universeAddress)
    {
        if (universeAddress?["oZw"] is not JObject coords) return null;

        return PackAddress(
            galaxy: universeAddress["Iis"]?.Value<int>() ?? 0,
            x: coords["dZj"]?.Value<int>() ?? 0,
            y: coords["IyE"]?.Value<int>() ?? 0,
            z: coords["uXE"]?.Value<int>() ?? 0,
            systemIndex: coords["vby"]?.Value<int>() ?? 0);
    }

    /// <summary>Parses a packed address field. The game writes values above
    /// 2^53 as "0x..." hex strings (common in ^PLANET_STATS) and smaller ones
    /// as plain integers, so both forms must be accepted.</summary>
    public static ulong? ParsePackedAddress(JToken? token)
    {
        if (token is null) return null;
        if (token.Type == JTokenType.Integer) return token.Value<ulong>();

        string? text = token.Type == JTokenType.String ? token.Value<string>() : null;
        if (text is not null && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex))
            return hex;

        return null;
    }

    public static JObject CreateSystemStatsGroup(ulong systemAddress)
    {
        var stats = new JArray();
        foreach (string id in SystemStatIdsInGameOrder)
            stats.Add(new JObject { ["b2n"] = id, [">MX"] = new JObject() });

        return new JObject
        {
            [":rc"] = SystemStatsGroupId,
            ["2Ak"] = systemAddress,
            ["gUR"] = stats,
        };
    }

    public static int ReadStat(JObject? group, string statId)
    {
        if (group?["gUR"] is not JArray stats) return 0;

        foreach (var token in stats)
        {
            if (token is JObject stat && string.Equals(stat["b2n"]?.Value<string>(), statId, StringComparison.Ordinal))
                return stat[">MX"]?[">vs"]?.Value<int>() ?? 0;
        }

        return 0;
    }

    /// <summary>Sets a stat in-place on the given (already cloned) group,
    /// adding the entry if the game hadn't written it yet.</summary>
    public static void WriteStat(JObject group, string statId, int value)
    {
        if (group["gUR"] is not JArray stats)
        {
            stats = new JArray();
            group["gUR"] = stats;
        }

        foreach (var token in stats)
        {
            if (token is JObject stat && string.Equals(stat["b2n"]?.Value<string>(), statId, StringComparison.Ordinal))
            {
                stat[">MX"] = new JObject { [">vs"] = value };
                return;
            }
        }

        stats.Add(new JObject { ["b2n"] = statId, [">MX"] = new JObject { [">vs"] = value } });
    }
}
