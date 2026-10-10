using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Space station directorship: shows the station in the player's current
/// system with its claim progress, claims it in one click, and lists owned
/// stations and claims in progress. See <see cref="NmsSpaceStationPaths"/>
/// for the confirmed save mapping.
/// </summary>
public sealed partial class SpaceStationsPage : Page
{
    private sealed record SystemStats(int Index, ulong Address, JObject Group);

    // Resolved on every LoadPage. _currentGroupIndex is where the current
    // system's ^SYSTEM_STATS group lives - or, if the game hasn't created it
    // yet, the index a new one gets appended at.
    private ulong? _currentSystemAddress;
    private int _currentGroupIndex = -1;
    private JObject? _currentGroup;

    public SpaceStationsPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionChanged;
        Unloaded += Page_Unloaded;

        LoadPage();
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadPage);

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionChanged;
    }

    private void LoadPage()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ShowNoSave();
            return;
        }

        var groups = SaveSessionManager.GetValue(S.StatGroupsPath) as JArray;
        var systemStats = ReadSystemStats(groups);
        var stationNames = ReadStationNamesBySystem();
        var owned = ReadOwnedStations();
        var ownedSystems = owned.Select(o => o.Address).ToHashSet();

        _currentSystemAddress = S.ReadSystemAddress(SaveSessionManager.GetValue(S.CurrentUniverseAddressPath));
        var current = systemStats.FirstOrDefault(s => s.Address == _currentSystemAddress);
        _currentGroupIndex = current?.Index ?? groups?.Count ?? -1;
        _currentGroup = current?.Group;

        ShowCurrentStation(stationNames, ownedSystems);
        ShowOwnedStations(owned);
        ShowInProgress(systemStats, stationNames, ownedSystems);
        UpdateResetButton();
    }

    private void ShowNoSave()
    {
        _currentSystemAddress = null;
        _currentGroupIndex = -1;
        _currentGroup = null;

        CurrentStationNameTxt.Text = "No save loaded";
        CurrentLocationTxt.Text = string.Empty;
        CurrentStatusTxt.Text = "Load a save to view and edit space stations.";
        ClaimStationBtn.IsEnabled = false;
        RequirementsPanel.Visibility = Visibility.Collapsed;
        OwnedStationsPanel.Children.Clear();
        InProgressPanel.Children.Clear();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }

    private void ShowCurrentStation(Dictionary<ulong, string> stationNames, HashSet<ulong> ownedSystems)
    {
        CurrentLocationTxt.Text = SaveSessionManager.GetValue(S.LocationDescriptionPath)?.Value<string>() ?? string.Empty;

        if (_currentSystemAddress is not ulong address)
        {
            CurrentStationNameTxt.Text = "Unknown location";
            CurrentStatusTxt.Text = "This save's current system couldn't be read.";
            ClaimStationBtn.IsEnabled = false;
            RequirementsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RequirementsPanel.Visibility = Visibility.Visible;
        CurrentStationNameTxt.Text = stationNames.TryGetValue(address, out var name) ? name : "No station recorded in this system";

        ShowRequirementValues();

        bool isOwned = ownedSystems.Contains(address);
        bool claimPending = SaveSessionManager.HasStagedEditsUnder(S.BasePath(BaseCount()));

        ClaimStationBtn.IsEnabled = !isOwned && stationNames.ContainsKey(address);
        CurrentStatusTxt.Text = claimPending
            ? "Station claimed. Save your changes to write it to the save file."
            : isOwned
                ? "You are the director of this station."
                : !stationNames.ContainsKey(address)
                    ? "No station is recorded in this system yet - dock at it in-game first."
                    : "Claim Station meets every requirement and makes you director, just like the in-game takeover.";
    }

    private void ShowRequirementValues()
    {
        var race = Best(S.RaceStats, _currentGroup);
        var guild = Best(S.GuildStats, _currentGroup);
        RaceStandingTxt.Text = $"{race.Value} / {S.RaceStandingRequired}";
        GuildStandingTxt.Text = $"{guild.Value} / {S.GuildStandingRequired}";
        ContractsTxt.Text = $"{S.ReadStat(_currentGroup, S.SalvageContractsStatId)} / {S.SalvageContractsRequired}";
    }

    /// <summary>The race/guild with the most standing in this system - the
    /// one the player has been working on is almost certainly the one the
    /// station asks for. Null name when there's no standing with any.</summary>
    private static (string? Name, int Value) Best((string StatId, string Name)[] stats, JObject? group)
    {
        var best = stats.Select(s => (s.Name, Value: S.ReadStat(group, s.StatId))).MaxBy(s => s.Value);
        return best.Value > 0 ? best : (null, 0);
    }

    private void ShowOwnedStations(List<(string Name, ulong Address)> owned)
    {
        OwnedHeaderTxt.Text = $"Stations You Direct ({owned.Count})";
        OwnedStationsPanel.Children.Clear();

        if (owned.Count == 0)
        {
            OwnedStationsPanel.Children.Add(CreateRow("None yet", null));
            return;
        }

        foreach (var (name, address) in owned)
            OwnedStationsPanel.Children.Add(CreateRow(name, address == _currentSystemAddress ? "Current system" : null));
    }

    private void ShowInProgress(List<SystemStats> systemStats, Dictionary<ulong, string> stationNames, HashSet<ulong> ownedSystems)
    {
        InProgressPanel.Children.Clear();

        var rows = systemStats
            .Where(s => !ownedSystems.Contains(s.Address) && HasAnyProgress(s.Group))
            .ToList();

        if (rows.Count == 0)
        {
            InProgressPanel.Children.Add(CreateRow("None", null));
            return;
        }

        foreach (var stats in rows)
        {
            string name = stationNames.TryGetValue(stats.Address, out var n) ? n : "Unnamed station";
            if (stats.Address == _currentSystemAddress) name += " (current system)";
            InProgressPanel.Children.Add(CreateRow(name, DescribeProgress(stats)));
        }
    }

    private static StackPanel CreateRow(string title, string? detail)
    {
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(detail))
        {
            row.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NmsTextSecondary"],
            });
        }
        return row;
    }

    /// <summary>Replicates the in-game takeover (see NmsSpaceStationPaths for
    /// the diff it's based on): requirements, the F?0 station
    /// record, ^STATIONS_OWNED + 1, and the marker's ownership stamp.</summary>
    private void ClaimStationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSessionManager.IsSaveLoaded || _currentSystemAddress is not ulong address) return;

        int markerIndex = FindStationMarkerIndex(address, out string stationName);
        JObject? owner = FindOwnerBlock();
        if (markerIndex < 0 || owner is null)
        {
            CurrentStatusTxt.Text = markerIndex < 0
                ? "No station is recorded in this system yet - dock at it in-game first."
                : "Couldn't find your player details on any existing base to sign the station with.";
            return;
        }

        MeetRequirements();

        // The game deducts a 1,000,000,000 Units donation on takeover - deliberately
        // NOT replicated here (user decision 2026-10-09: claiming shouldn't cost Units).

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveSessionManager.StageEdit(S.CreateClaimedStationRecord(address, stationName, owner, now), S.BasePath(BaseCount()));

        if (FindStationsOwnedStat() is (int g, int s))
        {
            var path = NmsPlayerStatsPaths.StatIntValuePath(g, s);
            SaveSessionManager.StageEdit((int)(SaveSessionManager.GetLong(path) ?? 0) + 1, path);
        }

        if (FindOwnershipStamp() is JArray stamp)
            SaveSessionManager.StageEdit(stamp.DeepClone(), S.MarkerOwnershipStampPath(markerIndex));
    }

    private void MeetRequirements()
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
            if (S.ReadStat(_currentGroup, statId) < required)
                raised[statId] = required;
        }
        if (raised.Count > 0)
            StageStats(raised);
    }

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGroupIndex >= 0)
            SaveSessionManager.RevertEditsUnder(S.StatGroupPath(_currentGroupIndex));

        SaveSessionManager.RevertEditsUnder(S.BasePath(BaseCount()));
        if (FindStationsOwnedStat() is (int g, int s))
            SaveSessionManager.RevertEditsUnder(NmsPlayerStatsPaths.StatIntValuePath(g, s));
        if (_currentSystemAddress is ulong address && FindStationMarkerIndex(address, out _) is int marker and >= 0)
            SaveSessionManager.RevertEditsUnder(S.MarkerOwnershipStampPath(marker));

        LoadPage();
    }

    /// <summary>Stages the current system's whole stats group at gUR/[index]
    /// (appending a new group if the game hasn't created one yet). Staging the
    /// group rather than the top-level gUR array keeps this page from
    /// colliding with other pages' edits to ^GLOBAL_STATS in the same array.</summary>
    private void StageStats(Dictionary<string, int> values)
    {
        if (_currentSystemAddress is not ulong address || _currentGroupIndex < 0) return;

        var group = (JObject?)_currentGroup?.DeepClone() ?? S.CreateSystemStatsGroup(address);
        foreach (var (statId, value) in values)
            S.WriteStat(group, statId, value);

        _currentGroup = group;
        SaveSessionManager.StageEdit(group, S.StatGroupPath(_currentGroupIndex));
    }

    private void UpdateResetButton()
    {
        bool hasEdits = (_currentGroupIndex >= 0 && SaveSessionManager.HasStagedEditsUnder(S.StatGroupPath(_currentGroupIndex)))
            || SaveSessionManager.HasStagedEditsUnder(S.BasePath(BaseCount()));
        PageResetBtn.Visibility = hasEdits ? Visibility.Visible : Visibility.Collapsed;
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
    private static List<SystemStats> ReadSystemStats(JArray? groups)
    {
        var result = new List<SystemStats>();
        if (groups is null) return result;

        for (int i = 0; i <= groups.Count; i++)
        {
            if (SaveSessionManager.GetValue(S.StatGroupPath(i)) is not JObject group) continue;
            if (!string.Equals(group[":rc"]?.Value<string>(), S.SystemStatsGroupId, StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(group["2Ak"]) is not ulong address) continue;

            result.Add(new SystemStats(i, S.ToSystemAddress(address), group));
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

    private static List<(string Name, ulong Address)> ReadOwnedStations()
    {
        var owned = new List<(string, ulong)>();
        int count = BaseCount();

        // <= count picks up a claim staged at the append slot but not yet saved.
        for (int i = 0; i <= count; i++)
        {
            if (SaveSessionManager.GetValue(S.BasePath(i)) is not JObject record) continue;
            if (!string.Equals(record["peI"]?["DPp"]?.Value<string>(), S.PlayerStationBaseType, StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(record["oZw"]) is not ulong address) continue;

            string name = record["NKm"]?.Value<string>() ?? string.Empty;
            owned.Add((string.IsNullOrWhiteSpace(name) ? $"Station {owned.Count + 1}" : name, S.ToSystemAddress(address)));
        }

        return owned;
    }

    private static bool HasAnyProgress(JObject group) =>
        S.RaceStats.Concat(S.GuildStats).Any(s => S.ReadStat(group, s.StatId) > 0)
        || S.ReadStat(group, S.SalvageContractsStatId) > 0;

    private static string DescribeProgress(SystemStats stats)
    {
        var race = Best(S.RaceStats, stats.Group);
        var guild = Best(S.GuildStats, stats.Group);

        return string.Join("  ·  ",
            $"{race.Name ?? "Race"} {race.Value}/{S.RaceStandingRequired}",
            $"{guild.Name?.Replace(" Guild", "") ?? "Guild"} {guild.Value}/{S.GuildStandingRequired}",
            $"Contracts {S.ReadStat(stats.Group, S.SalvageContractsStatId)}/{S.SalvageContractsRequired}");
    }
}
