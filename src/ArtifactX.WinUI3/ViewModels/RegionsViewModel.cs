using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One body's chip on the Regions page: filled when its page was captured, half when
/// only its hover was, an outline when nothing was.</summary>
public sealed class BodyChipViewModel(BodyCapture body)
{
    public BodyCapture Body { get; } = body;
    public string Label => Body.IsMoon ? "M" : Body.Index.ToString(CultureInfo.InvariantCulture);
    public string Tooltip => $"{(Body.IsMoon ? "Moon" : $"Planet {Body.Index}")} · {Body.Name}" + (Body.Missing.Length > 0 ? $"\n{Body.Missing}" : "\nEverything captured");
    public bool CanOpen => Body.Planet is not null;
    public Brush Fill => Body.Level switch
    {
        CaptureLevel.Page => ThemeResources.LevelBrush(0),
        CaptureLevel.Hover => ThemeResources.LevelBrush(1),
        _ => ThemeResources.Brush("ThemeVoidBrush"),
    };
    public Brush Foreground => Body.Level == CaptureLevel.None ? ThemeResources.Brush("ThemeTextBrush") : ThemeResources.Brush("ThemeVoidBrush");
}

/// <summary>One system's row on the Regions page.</summary>
public sealed class RegionSystemViewModel(SystemCapture system, bool here)
{
    public SystemCapture System { get; } = system;
    public string Name => System.Name;
    public bool IsHere { get; } = here;
    public IReadOnlyList<BodyChipViewModel> Bodies { get; } = system.Bodies.Select(b => new BodyChipViewModel(b)).ToList();
    public string Progress => System.Bodies.Count == 0 ? "No planets" : $"{System.Complete} of {System.Bodies.Count} planets captured";
    public string SystemDataLabel => System.SystemData ? "System data captured" : "System data missing - hover the system in Discoveries";
    public Brush SystemDataBrush => System.SystemData ? ThemeResources.LevelBrush(0) : ThemeResources.LevelBrush(1);
    public string Visited => System.LatestUnixSeconds > 0 ? $"Last discovery {PlanetText.Date(System.LatestUnixSeconds)}" : "";
    public string FoundBy => System.Mine ? "" : "Discovered by another player";
    public bool HasFoundBy => FoundBy.Length > 0;
    public string PortalCode => S.PortalAddress(System.System, 0);
    public bool IsComplete => System.IsComplete;
}

/// <summary>
/// Regions: how much of each region you've found systems in is mapped - for each system, its
/// system data and, per planet, whether its Discoveries hover and its page were captured. Built
/// for mapping a whole region (2026-10-20: the user mapping Noturbo Spur, where nobody else has
/// been, as clean data for the generator work).
/// </summary>
public sealed partial class RegionsViewModel : ObservableObject
{
    private PlanetSnapshot _snapshot = new(null, null, null);
    private List<RegionSummary> _regions = new();
    private List<RegionSystemViewModel> _all = new();
    private bool _loading;

    public ChoiceFilter Region { get; } = new("Region", 360);

    public RegionsViewModel() => Region.Changed += () => _ = ShowRegionAsync();

    [ObservableProperty] public partial bool IsLoading { get; private set; } = true;
    [ObservableProperty] public partial string RegionTitle { get; private set; } = "";
    [ObservableProperty] public partial string RegionSub { get; private set; } = "";
    [ObservableProperty] public partial string SystemsStat { get; private set; } = "";
    [ObservableProperty] public partial string SystemDataStat { get; private set; } = "";
    [ObservableProperty] public partial string PlanetsStat { get; private set; } = "";
    [ObservableProperty] public partial string HoverOnlyStat { get; private set; } = "";
    [ObservableProperty] public partial double PlanetsDone { get; private set; }
    [ObservableProperty] public partial double PlanetsTotal { get; private set; } = 1;
    [ObservableProperty] public partial IReadOnlyList<RegionSystemViewModel> Systems { get; private set; } = [];
    [ObservableProperty] public partial bool OnlyMissing { get; set; }
    [ObservableProperty] public partial int SortIndex { get; set; } // 0 newest first, 1 most to capture first, 2 name

    partial void OnOnlyMissingChanged(bool value) => Apply();
    partial void OnSortIndexChanged(int value) => Apply();

    /// <summary>Reads the save (or a newer one the game wrote) and shows the region you're in,
    /// or keeps the one picked.</summary>
    public async Task ShowAsync(PlanetSnapshot snapshot)
    {
        _snapshot = snapshot;
        _regions = await Task.Run(() => RegionMapService.Regions(snapshot));
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        string keep = Region.Value;
        string hereKey = here is ulong h ? RegionMapService.RegionKey(h).ToString(CultureInfo.InvariantCulture) : "";
        Region.Refill("Pick a region", _regions.Select(r => new ChoiceItem(
            $"{r.Name} · {PlanetText.GalaxyLabel(r.Galaxy)} · {r.Systems} system{(r.Systems == 1 ? "" : "s")}" + (r.Key.ToString(CultureInfo.InvariantCulture) == hereKey ? " · you're here" : ""),
            r.Key.ToString(CultureInfo.InvariantCulture))));
        Region.Select(keep.Length > 0 ? keep : hereKey.Length > 0 ? hereKey : _regions.FirstOrDefault()?.Key.ToString(CultureInfo.InvariantCulture) ?? "");
        await ShowRegionAsync();
    }

    /// <summary>Something captured changed: the same region again.</summary>
    public Task RefreshAsync() => ShowRegionAsync();

    private async Task ShowRegionAsync()
    {
        if (_loading || !ulong.TryParse(Region.Value, out ulong key)) { IsLoading = false; return; }
        _loading = true;
        try
        {
            var snapshot = _snapshot;
            var systems = await Task.Run(() => RegionMapService.Systems(snapshot, key));
            ulong? here = S.ReadSystemAddress(snapshot.Universe);
            _all = systems.Select(s => new RegionSystemViewModel(s, s.System == here)).ToList();
            var info = _regions.FirstOrDefault(r => r.Key == key);
            RegionTitle = info?.Name ?? "";
            // The region's galactic coordinates as a signal booster shows them, without the system part.
            string coords = systems.Count > 0 ? NmsPortalCode.ToCoordinates(S.PortalAddress(systems[0].System, 0))[..14] : "";
            RegionSub = info is null ? "" : $"{PlanetText.GalaxyLabel(info.Galaxy)}" + (coords.Length > 0 ? $" · galactic coordinates {coords}" : "");

            int bodies = systems.Sum(s => s.Bodies.Count), done = systems.Sum(s => s.Complete);
            int hoverOnly = systems.Sum(s => s.Bodies.Count(b => b.Level == CaptureLevel.Hover));
            SystemsStat = $"{systems.Count:N0} system{(systems.Count == 1 ? "" : "s")} in your save" + (info is { } r && r.SystemsMine < r.Systems ? $" · {r.SystemsMine:N0} discovered by you" : "");
            SystemDataStat = $"{systems.Count(s => s.SystemData):N0} of {systems.Count:N0} with system data";
            PlanetsStat = $"{done:N0} of {bodies:N0} planets and moons fully captured";
            HoverOnlyStat = hoverOnly > 0 ? $"{hoverOnly:N0} more hovered but not opened" : "";
            PlanetsDone = done;
            PlanetsTotal = Math.Max(1, bodies);
            Apply();
        }
        finally
        {
            _loading = false;
            IsLoading = false;
        }
    }

    private void Apply()
    {
        IEnumerable<RegionSystemViewModel> shown = OnlyMissing ? _all.Where(s => !s.IsComplete) : _all;
        shown = SortIndex switch
        {
            1 => shown.OrderBy(s => s.System.Complete - s.System.Bodies.Count + (s.System.SystemData ? 0 : -1)).ThenBy(s => s.Name, StringComparer.CurrentCulture),
            2 => shown.OrderBy(s => s.Name, StringComparer.CurrentCulture),
            _ => shown.OrderByDescending(s => s.IsHere).ThenByDescending(s => s.System.LatestUnixSeconds),
        };
        Systems = shown.ToList();
    }
}
