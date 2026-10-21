using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>A system's ^SYSTEM_STATS group (local standing, salvage contracts) at gUR/[Index].</summary>
public sealed record StationSystemStats(int Index, ulong Address, JObject Group);

/// <summary>A space station the player directs: its F?0 base record.</summary>
public sealed record OwnedStation(string Name, ulong Address, JObject Record);

/// <summary>Everything the Space Stations page shows, read once from the loaded save
/// (through staged edits). CurrentGroupIndex is where the current system's stats
/// group lives, or the index a new one gets appended at if the game hasn't made it.</summary>
public sealed record StationOverview(
    ulong? CurrentAddress,
    int CurrentGroupIndex,
    JObject? CurrentGroup,
    string Location,
    Dictionary<ulong, string> StationNames,
    List<OwnedStation> Owned,
    List<StationSystemStats> Claims)
{
    public bool IsOwned(ulong address) => Owned.Any(o => o.Address == address);

    public string StationName(ulong address) =>
        StationNames.TryGetValue(address, out var name) ? name : $"Station in {UniverseInfo.SystemName(address)}";
}

/// <summary>When a station's system was discovered (and by whom) and when it was claimed, as Unix seconds (0 = unknown).</summary>
public sealed record StationHistory(long DiscoveredAt, string Discoverer, long ClaimedAt);

/// <summary>
/// Space station directorship: reads stations, claims and local standing from the
/// save, and stages a claim exactly as the in-game takeover writes it. See
/// <see cref="NmsSpaceStationPaths"/> for the confirmed save mapping.
/// </summary>
public static class SpaceStationService
{
    public static StationOverview ReadOverview()
    {
        var groups = SaveSessionManager.GetValue(S.StatGroupsPath) as JArray;
        var systemStats = ReadSystemStats(groups);
        var stationNames = ReadStationNamesBySystem();
        var owned = ReadOwnedStations();
        var ownedSystems = owned.Select(o => o.Address).ToHashSet();

        ulong? here = S.ReadSystemAddress(SaveSessionManager.GetValue(S.CurrentUniverseAddressPath));
        var current = systemStats.FirstOrDefault(s => s.Address == here);

        if (here is ulong address && !stationNames.ContainsKey(address)
            && current is not null && TryStationNameFromLocation(out string? docked))
            stationNames[address] = docked;

        var claims = systemStats.Where(s => !ownedSystems.Contains(s.Address) && HasAnyProgress(s.Group)).ToList();

        return new StationOverview(
            here,
            current?.Index ?? groups?.Count ?? -1,
            current?.Group,
            SaveSessionManager.GetValue(S.LocationDescriptionPath)?.Value<string>() ?? string.Empty,
            stationNames,
            owned,
            claims);
    }

    /// <summary>The stats group of one system, through staged edits.</summary>
    public static JObject? SystemGroup(ulong address) =>
        ReadSystemStats(SaveSessionManager.GetValue(S.StatGroupsPath) as JArray).FirstOrDefault(g => g.Address == address)?.Group;

    /// <summary>The race/guild with the most standing in this system - the
    /// one the player has been working on is almost certainly the one the
    /// station asks for. Null name when there's no standing with any.</summary>
    public static (string? Name, int Value) Best((string StatId, string Name)[] stats, JObject? group)
    {
        var best = stats.Select(s => (s.Name, Value: S.ReadStat(group, s.StatId))).MaxBy(s => s.Value);
        return best.Value > 0 ? best : (null, 0);
    }

    /// <summary>0 to 3: how far along the three requirements are.</summary>
    public static double ClaimProgress(JObject group) =>
        Math.Min(1.0, Best(S.RaceStats, group).Value / (double)S.RaceStandingRequired)
        + Math.Min(1.0, Best(S.GuildStats, group).Value / (double)S.GuildStandingRequired)
        + Math.Min(1.0, S.ReadStat(group, S.SalvageContractsStatId) / (double)S.SalvageContractsRequired);

    public static bool IsClaimPending() => SaveSessionManager.HasStagedEditsUnder(S.BasePath(BaseCount()));

    public static bool HasEdits(StationOverview overview) =>
        (overview.CurrentGroupIndex >= 0 && SaveSessionManager.HasStagedEditsUnder(S.StatGroupPath(overview.CurrentGroupIndex)))
        || IsClaimPending()
        || (overview.CurrentAddress is ulong here && DirectedStationIndex(here) is int i && SaveSessionManager.HasStagedEditsUnder(S.BasePath(i)));

    /// <summary>The base index of the station the player directs in this system - a claim
    /// staged at the append slot included - or null.</summary>
    public static int? DirectedStationIndex(ulong systemAddress)
    {
        int count = BaseCount();
        for (int i = 0; i <= count; i++)
            if (SaveSessionManager.GetValue(S.BasePath(i)) is JObject record
                && string.Equals(record["peI"]?["DPp"]?.Value<string>(), S.PlayerStationBaseType, StringComparison.Ordinal)
                && S.ParsePackedAddress(record["oZw"]) is ulong address && S.ToSystemAddress(address) == systemAddress)
                return i;
        return null;
    }

    /// <summary>The parts of the station the player directs in this system (through staged edits).</summary>
    public static JArray? DirectedStationParts(ulong systemAddress) =>
        DirectedStationIndex(systemAddress) is int i ? SaveSessionManager.GetValue(S.BasePath(i))?["@ZJ"] as JArray : null;

    /// <summary>Removes the furniture a claim comes with (about 140 parts that count toward the
    /// 21,000 build limit), keeping the station's own structure (BasePartNames.CanRemoveFromStation).
    /// A claim not saved yet is restaged whole; a saved station's parts list is staged. Returns
    /// how many parts went.</summary>
    public static int ClearFurniture(ulong systemAddress)
    {
        if (DirectedStationIndex(systemAddress) is not int i || SaveSessionManager.GetValue(S.BasePath(i)) is not JObject record
            || record["@ZJ"] is not JArray parts) return 0;
        var kept = new JArray(parts.Where(p => !BasePartNames.CanRemoveFromStation(p["r<7"]?.Value<string>() ?? "")));
        int removed = parts.Count - kept.Count;
        if (removed == 0) return 0;
        if (i == BaseCount())
        {
            var copy = (JObject)record.DeepClone();
            copy["@ZJ"] = kept;
            SaveSessionManager.StageEdit(copy, S.BasePath(i));
        }
        else
            SaveSessionManager.StageEdit(kept, S.BasePath(i).Append("@ZJ").ToArray());
        return removed;
    }

    /// <summary>Replicates the in-game takeover (see NmsSpaceStationPaths for
    /// the diff it's based on): requirements, the F?0 station record,
    /// ^STATIONS_OWNED + 1, and the marker's ownership stamp. Returns why it
    /// couldn't, or null once staged.</summary>
    public static string? Claim(StationOverview overview)
    {
        if (!SaveSessionManager.IsSaveLoaded || overview.CurrentAddress is not ulong address) return "No save loaded.";

        // The marker can lag behind docking (the game writes it later), so fall
        // back to the "Aboard <station>" location line - see TryStationNameFromLocation.
        int markerIndex = FindStationMarkerIndex(address, out string stationName);
        if (markerIndex < 0 && SystemGroup(address) is not null && TryStationNameFromLocation(out string? docked))
            stationName = docked;

        JObject? owner = FindOwnerBlock();
        if (string.IsNullOrEmpty(stationName))
            return "No station is recorded in this system yet - dock at it in-game first.";
        if (owner is null)
            return "Couldn't find your player details on any existing base to sign the station with.";

        MeetRequirements(overview);

        // The game deducts a 1,000,000,000 Units donation on takeover - deliberately
        // NOT replicated here (user decision 2026-10-09: claiming shouldn't cost Units).

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveSessionManager.StageEdit(S.CreateClaimedStationRecord(address, stationName, owner, now), S.BasePath(BaseCount()));

        if (FindStationsOwnedStat() is (int g, int s))
        {
            var path = NmsPlayerStatsPaths.StatIntValuePath(g, s);
            SaveSessionManager.StageEdit((int)(SaveSessionManager.GetLong(path) ?? 0) + 1, path);
        }

        // No marker yet = nothing to stamp; a first-ever claim without a stamp
        // was confirmed fine in-game (2026-10-09), so the claim still works.
        if (markerIndex >= 0 && FindOwnershipStamp() is JArray stamp)
            SaveSessionManager.StageEdit(stamp.DeepClone(), S.MarkerOwnershipStampPath(markerIndex));

        return null;
    }

    /// <summary>Undoes a staged claim and any standing it raised.</summary>
    public static void RevertClaim(StationOverview overview)
    {
        if (overview.CurrentGroupIndex >= 0)
            SaveSessionManager.RevertEditsUnder(S.StatGroupPath(overview.CurrentGroupIndex));

        // A saved station's cleared furniture first: the index is found while it's still there.
        if (overview.CurrentAddress is ulong here && DirectedStationIndex(here) is int directed && directed < BaseCount())
            SaveSessionManager.RevertEditsUnder(S.BasePath(directed));
        SaveSessionManager.RevertEditsUnder(S.BasePath(BaseCount()));
        if (FindStationsOwnedStat() is (int g, int s))
            SaveSessionManager.RevertEditsUnder(NmsPlayerStatsPaths.StatIntValuePath(g, s));
        if (overview.CurrentAddress is ulong address && FindStationMarkerIndex(address, out _) is int marker and >= 0)
            SaveSessionManager.RevertEditsUnder(S.MarkerOwnershipStampPath(marker));
    }

    /// <summary>Discovered (the system's own discovery record) and claimed (the base record) times.</summary>
    public static StationHistory History(OwnedStation station, List<JObject> discoveries)
    {
        var systemRecord = discoveries.FirstOrDefault(d => d["8P3"]?["<Dn"]?.Value<string>() == "SolarSystem");
        return new StationHistory(
            systemRecord?["ksu"]?["3I1"]?.Value<long>() ?? 0,
            systemRecord?["ksu"]?["V?:"]?.Value<string>() ?? string.Empty,
            station.Record["3?K"]?["3I1"]?.Value<long>() ?? 0);
    }

    public static List<JObject> ReadDiscoveriesInSystem(ulong systemAddress)
    {
        if (SaveSessionManager.GetValue(S.DiscoveriesPath) is not JArray records) return new();

        return records.OfType<JObject>()
            .Where(r => S.ParsePackedAddress(r["8P3"]?["5L6"]) is ulong a && S.ToSystemAddress(a) == systemAddress)
            .ToList();
    }

    /// <summary>(count, label) pairs for discovery types with a plain-English
    /// name; planets counted by distinct planet index.</summary>
    public static List<(int Count, string Label)> SummarizeDiscoveries(List<JObject> discoveries)
    {
        int CountOf(string type) => discoveries.Count(d => d["8P3"]?["<Dn"]?.Value<string>() == type);

        int planets = discoveries
            .Where(d => d["8P3"]?["<Dn"]?.Value<string>() == "Planet")
            .Select(d => S.ParsePackedAddress(d["8P3"]?["5L6"]) is ulong a ? a >> 52 : 0)
            .Distinct()
            .Count();

        var result = new List<(int, string)>();
        if (planets > 0) result.Add((planets, planets == 1 ? "planet" : "planets"));
        if (CountOf("Animal") is int fauna and > 0) result.Add((fauna, "fauna"));
        if (CountOf("Flora") is int flora and > 0) result.Add((flora, "flora"));
        if (CountOf("Mineral") is int minerals and > 0) result.Add((minerals, minerals == 1 ? "mineral" : "minerals"));
        return result;
    }

    /// <summary>Names of the planet bases and space bases sharing the station's
    /// system. Freighter and ship-dock records also carry an address but just
    /// follow the player around, so they're left out.</summary>
    public static List<string> ReadOtherBasesInSystem(ulong systemAddress, JObject station)
    {
        var result = new List<string>();
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return result;

        foreach (var record in bases.OfType<JObject>())
        {
            if (ReferenceEquals(record, station)) continue;
            if (record["peI"]?["DPp"]?.Value<string>() is not ("HomePlanetBase" or "PlayerSpaceBase")) continue;
            if (S.ParsePackedAddress(record["oZw"]) is not ulong a || S.ToSystemAddress(a) != systemAddress) continue;

            string name = record["NKm"]?.Value<string>() ?? string.Empty;
            result.Add(string.IsNullOrWhiteSpace(name) ? "Unnamed" : name);
        }
        return result;
    }

    /// <summary>The game writes a station's map marker (nlG) and its
    /// "SpaceStation" discovery some time after docking - a fresh save made
    /// while docked had neither (Hybelosso Station Major, 2026-10-10). What it
    /// does have straight away: the location line "Aboard &lt;station name&gt;"
    /// and an (empty) ^SYSTEM_STATS group for the system - callers must check
    /// that group exists too. "Aboard the Space Anomaly" is excluded: station
    /// names never start with "the".</summary>
    private static bool TryStationNameFromLocation([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? stationName)
    {
        const string prefix = "Aboard ";
        stationName = null;
        string location = SaveSessionManager.GetValue(S.LocationDescriptionPath)?.Value<string>() ?? string.Empty;
        if (!location.StartsWith(prefix, StringComparison.Ordinal)) return false;

        string name = location[prefix.Length..].Trim();
        if (name.Length == 0 || name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) return false;

        stationName = name;
        return true;
    }

    private static void MeetRequirements(StationOverview overview)
    {
        // Raise, never lower - a player who already overshot keeps their standing.
        var raised = new Dictionary<string, int>();
        // Each station checks one race and one guild, decided by the game at
        // runtime and not stored in the save - so raise all three of each.
        // Extra standing is local to this one system, so it's harmless.
        var requirements = S.RaceStats.Select(r => (r.StatId, Required: S.RaceStandingRequired))
            .Concat(S.GuildStats.Select(g => (g.StatId, Required: S.GuildStandingRequired)))
            .Append((S.SalvageContractsStatId, Required: S.SalvageContractsRequired));

        foreach (var (statId, required) in requirements)
        {
            if (S.ReadStat(overview.CurrentGroup, statId) < required)
                raised[statId] = required;
        }
        if (raised.Count > 0)
            StageStats(overview, raised);
    }

    /// <summary>Stages the current system's whole stats group at gUR/[index]
    /// (appending a new group if the game hasn't created one yet). Staging the
    /// group rather than the top-level gUR array keeps this page from
    /// colliding with other pages' edits to ^GLOBAL_STATS in the same array.</summary>
    private static void StageStats(StationOverview overview, Dictionary<string, int> values)
    {
        if (overview.CurrentAddress is not ulong address || overview.CurrentGroupIndex < 0) return;

        var group = (JObject?)overview.CurrentGroup?.DeepClone() ?? S.CreateSystemStatsGroup(address);
        foreach (var (statId, value) in values)
            S.WriteStat(group, statId, value);

        SaveSessionManager.StageEdit(group, S.StatGroupPath(overview.CurrentGroupIndex));
    }

    private static int BaseCount() => (SaveSessionManager.GetValue(S.BasesPath) as JArray)?.Count ?? 0;

    private static int FindStationMarkerIndex(ulong systemAddress, out string stationName)
    {
        stationName = string.Empty;
        if (SaveSessionManager.GetValue(S.MarkersPath) is not JArray markers) return -1;

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] is not JObject marker) continue;
            if (!string.Equals(marker["iAF"]?.Value<string>(), S.StationMarkerType, StringComparison.Ordinal)) continue;
            if (S.ReadSystemAddress(marker["yhJ"]) != systemAddress) continue;

            stationName = marker["NKm"]?.Value<string>() ?? string.Empty;
            return i;
        }

        return -1;
    }

    /// <summary>The 4 x uint32 stamp the game writes on an owned station's
    /// marker - identical across all of a player's stations, so it's copied
    /// from any marker that already carries one. Null on a first-ever claim,
    /// in which case the marker is left as-is - CONFIRMED fine in-game
    /// 2026-10-09 (first claim on a fresh Slot 5 still showed Manage Station).</summary>
    private static JArray? FindOwnershipStamp()
    {
        if (SaveSessionManager.GetValue(S.MarkersPath) is not JArray markers) return null;

        foreach (var marker in markers.OfType<JObject>())
        {
            if (!string.Equals(marker["iAF"]?.Value<string>(), S.StationMarkerType, StringComparison.Ordinal)) continue;
            if (marker["6H2"]?["8?J"] is JArray stamp && stamp.Any(v => v.Value<long>() != 0))
                return stamp;
        }

        return null;
    }

    /// <summary>Owner block (3?K: platform user id, username, platform) from
    /// the player's own bases, preferring an existing owned station's.</summary>
    private static JObject? FindOwnerBlock()
    {
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray bases) return null;

        return bases.OfType<JObject>()
            .OrderByDescending(b => string.Equals(b["peI"]?["DPp"]?.Value<string>(), S.PlayerStationBaseType, StringComparison.Ordinal))
            .Select(b => b["3?K"] as JObject)
            .FirstOrDefault(o => !string.IsNullOrEmpty(o?["f5Q"]?.Value<string>()));
    }

    private static (int Group, int Stat)? FindStationsOwnedStat()
    {
        if (SaveSessionManager.GetValue(S.StatGroupsPath) is not JArray groups) return null;

        for (int g = 0; g < groups.Count; g++)
        {
            if (groups[g] is not JObject group) continue;
            if (!string.Equals(group[":rc"]?.Value<string>(), S.GlobalStatsGroupId, StringComparison.Ordinal)) continue;
            if (group["gUR"] is not JArray stats) return null;

            for (int i = 0; i < stats.Count; i++)
            {
                if (string.Equals(stats[i]?["b2n"]?.Value<string>(), S.StationsOwnedStatId, StringComparison.Ordinal))
                    return (g, i);
            }
            return null;
        }

        return null;
    }

    /// <summary>Every ^SYSTEM_STATS group, read through any staged edit so the
    /// page reflects pending changes - plus a staged-but-not-yet-committed new
    /// group sitting one past the end of the array.</summary>
    private static List<StationSystemStats> ReadSystemStats(JArray? groups)
    {
        var result = new List<StationSystemStats>();
        if (groups is null) return result;

        for (int i = 0; i <= groups.Count; i++)
        {
            if (SaveSessionManager.GetValue(S.StatGroupPath(i)) is not JObject group) continue;
            if (!string.Equals(group[":rc"]?.Value<string>(), S.SystemStatsGroupId, StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(group["2Ak"]) is not ulong address) continue;

            result.Add(new StationSystemStats(i, S.ToSystemAddress(address), group));
        }

        return result;
    }

    private static Dictionary<ulong, string> ReadStationNamesBySystem()
    {
        var names = new Dictionary<ulong, string>();
        if (SaveSessionManager.GetValue(S.MarkersPath) is not JArray markers) return names;

        foreach (var token in markers)
        {
            if (token is not JObject marker) continue;
            if (!string.Equals(marker["iAF"]?.Value<string>(), S.StationMarkerType, StringComparison.Ordinal)) continue;
            if (S.ReadSystemAddress(marker["yhJ"]) is not ulong address) continue;

            string name = marker["NKm"]?.Value<string>() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
                names.TryAdd(address, name);
        }

        return names;
    }

    private static List<OwnedStation> ReadOwnedStations()
    {
        var owned = new List<OwnedStation>();
        int count = BaseCount();

        // <= count picks up a claim staged at the append slot but not yet saved.
        for (int i = 0; i <= count; i++)
        {
            if (SaveSessionManager.GetValue(S.BasePath(i)) is not JObject record) continue;
            if (!string.Equals(record["peI"]?["DPp"]?.Value<string>(), S.PlayerStationBaseType, StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(record["oZw"]) is not ulong address) continue;

            string name = record["NKm"]?.Value<string>() ?? string.Empty;
            owned.Add(new OwnedStation(string.IsNullOrWhiteSpace(name) ? $"Station {owned.Count + 1}" : name, S.ToSystemAddress(address), record));
        }

        return owned;
    }

    private static bool HasAnyProgress(JObject group) =>
        S.RaceStats.Concat(S.GuildStats).Any(s => S.ReadStat(group, s.StatId) > 0)
        || S.ReadStat(group, S.SalvageContractsStatId) > 0;
}
