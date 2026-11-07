using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One of the five stats a Galactic Alliance is graded on, as the player's own
/// lifetime count, with what makes up the number and how to raise it.</summary>
public sealed class AllianceStatViewModel(string label, string iconData, int value, string breakdown, string howTo)
{
    public string Label { get; } = label;

    /// <summary>A new Geometry on every read: one Geometry can't be shown by two elements (the
    /// ring and the card each bind it). Made in code - a keyed Geometry resource crashes WinUI.</summary>
    public Microsoft.UI.Xaml.Media.Geometry Icon =>
        (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Microsoft.UI.Xaml.Media.Geometry), iconData);
    public string Value { get; } = value.ToString("N0");
    public string Breakdown { get; } = breakdown;
    public bool HasBreakdown => Breakdown.Length > 0;
    public string HowTo { get; } = howTo;
    public string AccessibleName => $"{Label}: {Value}";
}

/// <summary>
/// Alliance (#29, first part): the player's own contribution to the five stats a Galactic
/// Alliance is graded on (Systems, Plants, Minerals, Creatures Discovered, Stations Claimed),
/// read from the game's own lifetime counters in ^GLOBAL_STATS - ^DISC_SYSTEMS, ^DISC_FLORA,
/// ^DISC_MINERALS, ^DISC_CREATURES, ^STATIONS_OWNED (Slot 8, 2026-10-21: 13 / 53 / 72 / 63 / 8,
/// matching its 8 claimed stations). The alliance's name, score, rank and classes are only on
/// Hello Games' servers - in no local file (searched 2026-10-10) - so they aren't shown here.
/// Whether an alliance counts discoveries made before joining isn't known.
/// </summary>
public sealed partial class AllianceViewModel : ObservableObject
{
    /// <summary>The alliance made by ArtifactX's author, suggested to players.</summary>
    public const string ArtifactXAlliance = "ARTIFACTX";

    [ObservableProperty] public partial IReadOnlyList<AllianceStatViewModel> Stats { get; private set; } = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoSave))]
    public partial bool HasStats { get; private set; }

    public bool NoSave => !HasStats;

    [ObservableProperty] public partial AllianceStatViewModel? Systems { get; private set; }
    [ObservableProperty] public partial AllianceStatViewModel? Plants { get; private set; }
    [ObservableProperty] public partial AllianceStatViewModel? Minerals { get; private set; }
    [ObservableProperty] public partial AllianceStatViewModel? Creatures { get; private set; }
    [ObservableProperty] public partial AllianceStatViewModel? Stations { get; private set; }

    /// <summary>The player's name, from their own discovery records (as the game signs them).</summary>
    [ObservableProperty] public partial string PlayerName { get; private set; } = "";

    public void Show(PlanetSnapshot? snapshot)
    {
        PlayerName = snapshot?.Discoveries?.OfType<JObject>()
            .Select(d => d["ksu"] as JObject)
            .FirstOrDefault(k => !string.IsNullOrEmpty(k?["f5Q"]?.Value<string>()) && !string.IsNullOrEmpty(k?["V?:"]?.Value<string>()))?["V?:"]?.Value<string>() ?? "";
        var group = (snapshot?.StatGroups ?? SaveSessionManager.GetValue(S.StatGroupsPath) as JArray)?.OfType<JObject>()
            .FirstOrDefault(g => g[":rc"]?.Value<string>() == S.GlobalStatsGroupId);
        if (group is null)
        {
            Stats = [];
            HasStats = false;
            return;
        }
        int Stat(string id) => S.ReadStat(group, id);
        string Parts(params (string Label, string Id)[] parts) =>
            string.Join(" · ", parts.Select(p => (p.Label, Count: Stat(p.Id))).Where(p => p.Count > 0).Select(p => $"{p.Count:N0} {p.Label}"));

        Stats =
        [
            new("Systems Discovered", ArtifactX.WinUI3.Resources.NavIconGeometries.Planets, Stat("^DISC_SYSTEMS"),
                Parts(("rare", "^DISC_RARE_SYS"), ("abandoned", "^DISC_ABAND"), ("pirate", "^PIRATE_SYSTEMS")),
                "Warp to systems nobody has named yet - Near Core and Planet Finder list the ones within your hyperdrive range."),
            new("Plants Discovered", ArtifactX.WinUI3.Resources.NavIconGeometries.Plant, Stat("^DISC_FLORA"),
                Parts(("lush", "^DIS_FLORA_LUSH"), ("toxic", "^DIS_FLORA_TOXIC"), ("hot", "^DIS_FLORA_HOT"), ("frozen", "^DIS_FLORA_FROZ"),
                      ("radioactive", "^DIS_FLORA_RADIO"), ("barren", "^DIS_FLORA_BARR"), ("volcanic", "^DIS_FLORA_LAVA"), ("swamp", "^DIS_FLORA_SWAMP"), ("exotic", "^DIS_FLORA_WEIRD")),
                "Scan plants on planets you haven't finished - Current System shows how many each planet has left."),
            new("Minerals Discovered", ArtifactX.WinUI3.Resources.NavIconGeometries.Crystal, Stat("^DISC_MINERALS"), "",
                "Scan rocks and crystals with your analysis visor on planets you haven't finished."),
            new("Creatures Discovered", ArtifactX.WinUI3.Resources.NavIconGeometries.Companions, Stat("^DISC_CREATURES"),
                Parts(("on land", "^DISC_CRE_LAND"), ("flying", "^DISC_CRE_AIR"), ("in water", "^DISC_CRE_WATER"), ("in caves", "^DISC_CRE_CAVE"),
                      ("robotic", "^DISC_CRE_ROBOT"), ("exotic", "^DISC_CRE_WEIRD")),
                "Scan creatures on planets where the fauna count isn't complete - Current System shows found / total for each planet."),
            new("Stations Claimed", ArtifactX.WinUI3.Resources.NavIconGeometries.SpaceStations, Stat("^STATIONS_OWNED"), "",
                "Claim stations - Space Stations lists your claims in progress. Each claim keeps 123 build parts you can never remove; clear its default furniture to save the rest."),
        ];
        (Systems, Plants, Minerals, Creatures, Stations) = (Stats[0], Stats[1], Stats[2], Stats[3], Stats[4]);
        HasStats = true;
    }
}
