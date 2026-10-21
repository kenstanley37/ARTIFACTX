using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The selected station's details: its system and galaxy, when it was discovered and
/// claimed, the player's discoveries and local standing there, other bases in the
/// system and its cached screenshot. Sections with nothing to show are hidden.
/// </summary>
public sealed partial class StationDetailsViewModel : ObservableObject
{
    public StationDetailsViewModel(OwnedStation station)
    {
        ulong address = station.Address;
        Station = station;
        Title = station.Name.ToUpperInvariant();
        Facts = $"{UniverseInfo.SystemName(address)} system · {UniverseInfo.Region(address)} · {UniverseInfo.Compact(address)}";
        PortalCode = S.PortalAddress(address);

        int galaxy = S.GalaxyNumber(address);
        string? galaxyName = NmsGalaxyNames.ForNumber(galaxy);
        GalaxyName = galaxyName ?? $"Galaxy {galaxy}";
        GalaxyNumber = galaxyName is null ? "" : $"Galaxy {galaxy}";

        var discoveries = SpaceStationService.ReadDiscoveriesInSystem(address);
        var history = SpaceStationService.History(station, discoveries);
        DiscoveredAt = FormatTimestamp(history.DiscoveredAt);
        DiscoveredBy = history.Discoverer.Length > 0 ? $"by {history.Discoverer}" : "";
        ClaimedAt = FormatTimestamp(history.ClaimedAt);

        Chips = SpaceStationService.SummarizeDiscoveries(discoveries).Select(c => new StationChipViewModel(c.Count, c.Label)).ToList();

        JObject? group = SpaceStationService.SystemGroup(address);
        Standings = S.RaceStats.Select(r => (Label: r.Name, Value: S.ReadStat(group, r.StatId), Required: S.RaceStandingRequired))
            .Concat(S.GuildStats.Select(g => (Label: g.Name, Value: S.ReadStat(group, g.StatId), Required: S.GuildStandingRequired)))
            .Append((Label: "Salvage contracts", Value: S.ReadStat(group, S.SalvageContractsStatId), Required: S.SalvageContractsRequired))
            .Where(s => s.Value > 0)
            .Select(s => new StationStandingViewModel(s.Label, s.Value, s.Required))
            .ToList();

        PartsPlaced = station.Record["@ZJ"] is JArray parts ? parts.Count.ToString("N0") : "";
        var otherBases = SpaceStationService.ReadOtherBasesInSystem(address, station.Record);
        OtherBases = otherBases.Count == 0 ? "None" : string.Join(", ", otherBases);
    }

    public OwnedStation Station { get; }
    public string Title { get; }
    public string Facts { get; }
    public string PortalCode { get; }

    public string GalaxyName { get; }
    /// <summary>"Galaxy 3" beside a named galaxy; empty when the name already is that.</summary>
    public string GalaxyNumber { get; }
    public bool HasGalaxyNumber => GalaxyNumber.Length > 0;

    public string DiscoveredAt { get; }
    public string DiscoveredBy { get; }
    public bool HasDiscoveredBy => DiscoveredBy.Length > 0;
    public string ClaimedAt { get; }

    public List<StationChipViewModel> Chips { get; }
    public bool HasChips => Chips.Count > 0;

    public List<StationStandingViewModel> Standings { get; }
    public bool HasStandings => Standings.Count > 0;

    public string PartsPlaced { get; }
    public bool HasPartsPlaced => PartsPlaced.Length > 0;
    public string OtherBases { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoScreenshot))]
    public partial ImageSource? Screenshot { get; set; }

    /// <summary>Set once the screenshot lookup finished, so "no screenshot" doesn't flash while it loads.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoScreenshot))]
    public partial bool ScreenshotLoaded { get; set; }

    public bool ShowNoScreenshot => ScreenshotLoaded && Screenshot is null;

    private static string FormatTimestamp(long unixSeconds) =>
        unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString("g") : "Unknown";
}
