using ArtifactX.Core.Collections;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>Everything the row shows: rows are replaced only when this changes, so a new save
    /// does not rebuild every card and its glyphs.</summary>
    public string DisplayKey => $"{IsHere}|{Name}|{Progress}|{System.SystemData}|{Visited}|{FoundBy}|"
        + string.Join(",", System.Bodies.Select(b => $"{b.Index}{(b.IsMoon ? "m" : "")}{(int)b.Level}{b.FaunaTotalKnown}{b.Planet is not null}"));
}

/// <summary>One not-yet-visited system on the Regions page: name, what it is, and its glyphs.</summary>
public sealed class UnvisitedSystemViewModel(UnvisitedSystem system, float? distance = null)
{
    /// <summary>Region units from your system, when you're in this region and it has positions.</summary>
    public float? Distance { get; } = distance;
    public string DistanceText { get; } = distance is float d
        ? string.Create(CultureInfo.CurrentCulture, $"{(int)(d * RegionStarPositions.LightYearsPerUnit):N0} ly") : "";
    public ulong System { get; } = system.System;
    public int Index { get; } = system.Index;
    public string Name { get; } = system.Name;
    public string Detail { get; } = (system.Index > 1000 ? $"System {system.Index} (purple)" : $"System {system.Index}")
        + (system.Summary.Length > 0 ? $" · {system.Summary}" : "");
    public string PortalCode { get; } = S.PortalAddress(system.System, 0);
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

    public ChoiceFilter Region { get; } = new("Region", 460);

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
    /// <summary>Reconciled in place (ObservableCollectionSync) and shown virtualized (ItemsRepeater).</summary>
    public ObservableCollection<RegionSystemViewModel> Systems { get; } = new();
    /// <summary>"539 star systems and 65 purple systems · you've visited 255 · 349 to go"; empty without generator settings.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasRegionTotal))] public partial string RegionTotalStat { get; private set; } = "";
    public bool HasRegionTotal => RegionTotalStat.Length > 0;
    [ObservableProperty] public partial double SystemsVisited { get; private set; }
    [ObservableProperty] public partial double SystemsTotal { get; private set; } = 1;
    [ObservableProperty] public partial string UnvisitedSummary { get; private set; } = "";
    public ObservableCollection<UnvisitedSystemViewModel> Unvisited { get; } = new();
    /// <summary>0: game numbering, 1: nearest to the system you're in.</summary>
    [ObservableProperty] public partial int UnvisitedSortIndex { get; set; }
    partial void OnUnvisitedSortIndexChanged(int value)
    {
        // A new order moves nearly every card; starting empty is cheaper than ~600 moves.
        Unvisited.Clear();
        _ = ShowRegionAsync();
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMap), nameof(ShowMapCard), nameof(MapSummary))] public partial IReadOnlyList<RegionMapStar> MapStars { get; private set; } = [];
    public bool HasMap => MapStars.Count > 0;
    public bool ShowMapCard => HasMap || HasMapNote;
    public string MapSummary => HasMap ? string.Create(CultureInfo.CurrentCulture, $"{MapStars.Count:N0} stars · {MapStars.Count(s => !s.Visited):N0} not visited") : "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelectedMapStar), nameof(NoSelectedMapStar))] public partial RegionMapStar? SelectedMapStar { get; set; }
    public bool HasSelectedMapStar => SelectedMapStar is not null;
    public bool NoSelectedMapStar => SelectedMapStar is null;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMapNote), nameof(ShowMapCard))] public partial string MapNote { get; private set; } = "";
    public bool HasMapNote => MapNote.Length > 0;
    private string _mapKey = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasUnvisited))] public partial int UnvisitedCount { get; private set; }
    public bool HasUnvisited => UnvisitedCount > 0;

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
            await ShowUnvisitedAsync(key, systems);
        }
        finally
        {
            _loading = false;
            IsLoading = false;
        }
    }

    /// <summary>The region's total from the game's generator and the systems not in the save yet.</summary>
    private async Task ShowUnvisitedAsync(ulong key, IReadOnlyList<SystemCapture> systems)
    {
        var count = await RegionSystemsService.GetAsync(key);
        if (Region.Value != key.ToString(CultureInfo.InvariantCulture)) return;
        if (count is null)
        {
            RegionTotalStat = UnvisitedSummary = "";
            UnvisitedCount = 0;
            Unvisited.Clear();
            await ShowMapAsync(key, null, [], 0);
            return;
        }
        var visited = systems.Select(s => (int)((s.System >> 40) & 0xFFF)).ToHashSet();
        var stars = await RegionSystemsService.GetStarsAsync(key);
        ulong? hereSystem = S.ReadSystemAddress(_snapshot.Universe);
        int here = hereSystem is ulong h && RegionMapService.RegionKey(h) == key ? (int)((h >> 40) & 0xFFF) : 0;
        var positions = stars?.ToDictionary(s => s.System) ?? new Dictionary<int, RegionStar>();
        bool hasHere = positions.TryGetValue(here, out var herePosition);
        bool byDistance = UnvisitedSortIndex == 1 && hasHere;
        var unvisited = await Task.Run(() =>
        {
            var rows = RegionMapService.Unvisited(key, count, visited).Select(s =>
                new UnvisitedSystemViewModel(s, hasHere && positions.TryGetValue(s.Index, out var p) ? RegionStarPositions.Distance(p, herePosition) : null));
            return (byDistance ? rows.OrderBy(r => r.Distance ?? float.MaxValue) : rows).ToList();
        });
        if (Region.Value != key.ToString(CultureInfo.InvariantCulture)) return;
        await ShowMapAsync(key, stars, visited, here);

        int total = count.Ordinary + count.Purple;
        int seen = total - unvisited.Count;
        RegionTotalStat = count.Ordinary == 0
            ? "This region lies inside the galactic centre and holds no star systems"
            : string.Create(CultureInfo.CurrentCulture,
                $"{count.Ordinary:N0} star systems and {count.Purple} purple systems · you've visited {seen:N0} · {unvisited.Count:N0} to go");
        SystemsVisited = seen;
        SystemsTotal = Math.Max(1, total);
        UnvisitedCount = unvisited.Count;
        UnvisitedSummary = string.Create(CultureInfo.CurrentCulture, $"{unvisited.Count:N0} systems");
        // A visited system disappears; every other card (and its glyphs) stays unless its distance changed.
        ObservableCollectionSync.Apply(Unvisited, unvisited, s => s.System, (a, b) => a.DistanceText == b.DistanceText);
    }

    /// <summary>The region map: every generated star, coloured by whether you've been there. Rebuilt only
    /// when the visited set or your system changes, so a save does not redraw 600 stars.</summary>
    private async Task ShowMapAsync(ulong key, IReadOnlyList<RegionStar>? stars, HashSet<int> visited, int here)
    {
        if (stars is null)
        {
            MapStars = [];
            SelectedMapStar = null;
            // Null stars: no placement settings, settings from before spiral regions (format 46), or the galactic centre.
            MapNote = await CatalogService.GetGalaxyPlacementSettingsAsync() is not { } placement
                ? "Rebuild the game data (Settings) to see the region map."
                : !placement.HasSpiral
                    ? "Rebuild the game data (Settings) to see the map of this region; it's a spiral-arm region."
                    : "This region lies inside the galactic centre and has no stars to map.";
            _mapKey = "";
            return;
        }
        string mapKey = $"{key}|{here}|{string.Join(",", visited.Order())}";
        if (mapKey == _mapKey) return;
        _mapKey = mapKey;
        RegionStar? herePosition = stars.Any(s => s.System == here) ? stars.First(s => s.System == here) : null;
        var built = await Task.Run(() => stars.Select(s =>
        {
            ulong system = key | (ulong)s.System << 40;
            int? lightYears = herePosition is { } h && s.System != here ? RegionStarPositions.LightYears(s, h) : null;
            return new RegionMapStar(s.System, s.X, s.Y, s.Z, visited.Contains(s.System), s.System == here,
                UniverseInfo.SystemName(system), UniverseInfo.Summary(system), S.PortalAddress(system, 0), lightYears);
        }).ToList());
        if (Region.Value != key.ToString(CultureInfo.InvariantCulture)) return;
        int? selected = SelectedMapStar?.System;
        MapStars = built;
        // Until a star is clicked the panel shows your own system, so there's always a reference point.
        SelectedMapStar = (selected is int keep ? built.FirstOrDefault(s => s.System == keep) : null) ?? built.FirstOrDefault(s => s.IsHere);
        MapNote = here != 0 ? "" : "You're not in this region, so no system is marked as yours.";
    }

    public void SelectMapStar(RegionMapStar star) => SelectedMapStar = star;

    /// <summary>The map filling the page (RegionsPage's expanded view) instead of sitting in its card.</summary>
    [ObservableProperty] public partial bool IsMapExpanded { get; set; }

    [RelayCommand] private void ExpandMap() => IsMapExpanded = true;
    [RelayCommand] private void CloseMap() => IsMapExpanded = false;

    private void Apply()
    {
        IEnumerable<RegionSystemViewModel> shown = OnlyMissing ? _all.Where(s => !s.IsComplete) : _all;
        shown = SortIndex switch
        {
            1 => shown.OrderBy(s => s.System.Complete - s.System.Bodies.Count + (s.System.SystemData ? 0 : -1)).ThenBy(s => s.Name, StringComparer.CurrentCulture),
            2 => shown.OrderBy(s => s.Name, StringComparer.CurrentCulture),
            _ => shown.OrderByDescending(s => s.IsHere).ThenByDescending(s => s.System.LatestUnixSeconds),
        };
        ObservableCollectionSync.Apply(Systems, shown.ToList(), s => s.System.System, (current, next) => current.DisplayKey == next.DisplayKey);
    }
}
