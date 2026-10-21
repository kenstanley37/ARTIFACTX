using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Planet Finder: every planet in the save's discoveries (and, if wanted, the other
/// save slots'), searched and filtered by what the save knows, what the game's generator
/// says about each system, and what the player recorded. Distances are from where the
/// save is now, so it follows the game (Show).
/// </summary>
public sealed partial class PlanetFinderViewModel : ObservableObject
{
    private List<KnownPlanet> _thisSave = new();
    private List<KnownPlanet> _otherSaves = new();
    private List<KnownPlanet> _all = new();
    private HashSet<ulong> _waterSystems = new();
    private PlanetVocabulary? _vocabulary;
    private ulong? _here;
    private int _loadVersion;
    private bool _quiet;
    private CancellationTokenSource? _searchDelay;

    public PlanetFinderViewModel()
    {
        foreach (var filter in ChoiceFilters.Append(Type).Append(Galaxy).Append(Region)) filter.Changed += Apply;
        JumpPlanner.RangeChanged += Apply;
    }

    public ChoiceFilter Type { get; } = new("Planet type", 220);
    public ChoiceFilter Galaxy { get; } = new("Galaxy", 220);
    public ChoiceFilter Region { get; } = new("Region", 240);
    public ChoiceFilter Star { get; } = new("Star colour");
    public ChoiceFilter Economy { get; } = new("Economy");
    public ChoiceFilter Wealth { get; } = new("Wealth", 180);
    public ChoiceFilter Conflict { get; } = new("Conflict", 180);
    public ChoiceFilter Race { get; } = new("Race", 180);
    public ChoiceFilter Ship { get; } = new("Ships seen", 180);
    public ChoiceFilter Sentinels { get; } = new("Sentinels");
    public ChoiceFilter Flora { get; } = new("Flora", 180);
    public ChoiceFilter Fauna { get; } = new("Fauna", 180);
    public ChoiceFilter Resource { get; } = new("Resource");

    /// <summary>The filters made from recorded and generated system and planet facts, in the order shown.</summary>
    public IReadOnlyList<ChoiceFilter> ChoiceFilters => [Star, Economy, Wealth, Conflict, Race, Ship, Sentinels, Flora, Fauna, Resource];

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial int SortIndex { get; set; }      // 0 nearest, 1 newest, 2 furthest from core, 3 nearest to core
    [ObservableProperty] public partial int BodiesIndex { get; set; }    // 0 all, 1 planets only, 2 moons only
    [ObservableProperty] public partial bool RingsOnly { get; set; }
    [ObservableProperty] public partial bool MineOnly { get; set; }
    [ObservableProperty] public partial bool IncludeOtherSlots { get; set; } = true;
    [ObservableProperty] public partial bool WaterOnly { get; set; }
    [ObservableProperty] public partial bool DissonantOnly { get; set; }

    [ObservableProperty] public partial string WaterLabel { get; private set; } = "";
    [ObservableProperty] public partial bool ShowWater { get; private set; }
    [ObservableProperty] public partial string DissonantLabel { get; private set; } = "";
    [ObservableProperty] public partial bool ShowDissonant { get; private set; }
    [ObservableProperty] public partial bool ShowBodies { get; private set; }
    [ObservableProperty] public partial bool HasNoteFilters { get; private set; }
    [ObservableProperty] public partial string Status { get; private set; } = "";

    [ObservableProperty] public partial IReadOnlyList<PlanetRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string ResultCount { get; private set; } = "";
    [ObservableProperty] public partial int ActiveFilters { get; private set; }

    public bool HasStatus => Status.Length > 0;
    public bool HasActiveFilters => ActiveFilters > 0;
    public string ClearLabel => $"Clear filters ({ActiveFilters})";

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnActiveFiltersChanged(int value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ClearLabel));
    }

    partial void OnSortIndexChanged(int value) => Apply();
    partial void OnBodiesIndexChanged(int value) => Apply();
    partial void OnRingsOnlyChanged(bool value) => Apply();
    partial void OnMineOnlyChanged(bool value) => Apply();
    partial void OnWaterOnlyChanged(bool value) => Apply();
    partial void OnDissonantOnlyChanged(bool value) => Apply();
    partial void OnIncludeOtherSlotsChanged(bool value) { Refresh(); _ = LoadOtherSavesAsync(_loadVersion); }

    /// <summary>Waits for a pause in typing, so the finder isn't re-run per key.</summary>
    partial void OnSearchChanged(string value)
    {
        if (_quiet) return;
        _searchDelay?.Cancel();
        var delay = _searchDelay = new CancellationTokenSource();
        _ = Task.Delay(250, delay.Token).ContinueWith(_ => Apply(), delay.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void Detach() => JumpPlanner.RangeChanged -= Apply;

    public async Task LoadAsync()
    {
        int version = ++_loadVersion;
        _vocabulary = await PlanetVocabulary.LoadAsync();
        await SystemNotesService.EnsureDescriptorsAsync();
        _otherSaves = new();
        Show(await LiveSaveWatcher.GetLatestAsync(), force: true);
        await LoadOtherSavesAsync(version);
    }

    /// <summary>A save the game wrote: new discoveries, and distances from where you are now.</summary>
    public void Show(PlanetSnapshot snapshot, bool force = false)
    {
        var planets = NmsPlanets.ReadAll(snapshot.Discoveries);
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        if (!force && here == _here && planets.Count == _thisSave.Count) return;
        _thisSave = planets;
        _here = here;
        Refresh();
    }

    private async Task LoadOtherSavesAsync(int version)
    {
        if (!IncludeOtherSlots || _otherSaves.Count > 0) return;
        if (SaveSessionManager.ActivePlatformFolder is not string folder || SaveSessionManager.ActiveSlot is not { } slot) return;
        Status = "Reading your other save slots...";
        var others = await KnownPlanetsService.ReadOtherSlotsAsync(folder, slot.SlotId);
        if (version != _loadVersion) return; // a newer load started meanwhile
        _otherSaves = others;
        Status = "";
        Refresh();
    }

    /// <summary>The planets changed (or something recorded about them): new filter choices, then the list.</summary>
    public void Refresh()
    {
        _all = IncludeOtherSlots
            ? _thisSave.Concat(_otherSaves).GroupBy(p => p.Address).Select(g => g.First()).ToList()
            : _thisSave;
        FillChoices();
        Apply();
    }

    private void FillChoices()
    {
        Type.Refill($"All types ({_all.Count})", NmsPlanets.SelectableTypes
            .Select(t => (Type: t, Count: _all.Count(p => p.BiomeIndex == t))).Where(t => t.Count > 0)
            .Select(t => new ChoiceItem($"{NmsPlanets.TypeName(t.Type)} ({t.Count})", t.Type.ToString(CultureInfo.InvariantCulture))));
        Region.Refill("All regions", _all.GroupBy(p => RegionMapService.RegionKey(p.SystemAddress))
            .OrderByDescending(g => g.Count()).ThenBy(g => UniverseInfo.Region(g.First().SystemAddress), StringComparer.CurrentCulture)
            .Select(g => new ChoiceItem($"{UniverseInfo.Region(g.First().SystemAddress)} ({g.Count():N0})", g.Key.ToString(CultureInfo.InvariantCulture))));
        Galaxy.Refill("All galaxies", _all.GroupBy(p => p.Galaxy).OrderBy(g => g.Key)
            .Select(g => new ChoiceItem($"{PlanetText.GalaxyLabel(g.Key)} ({g.Count()})", g.Key.ToString(CultureInfo.InvariantCulture))));

        // Star, economy, wealth, conflict and race come from the game's generator for every
        // planet the finder knows (the player's notes override), counted in planets.
        IEnumerable<ChoiceItem> Known(Func<ulong, string> value) => _all
            .Select(p => value(p.SystemAddress)).Where(v => v.Length > 0 && v != "None")
            .GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ChoiceItem($"{g.Key} ({g.Count():N0})", g.Key));
        Star.Refill("Any", Known(PlanetText.StarOf));
        Economy.Refill("Any", Known(UniverseInfo.Economy));
        Wealth.Refill("Any", Known(UniverseInfo.Wealth));
        Conflict.Refill("Any", Known(UniverseInfo.Conflict));
        Race.Refill("Any", Known(PlanetText.RaceOf));

        // Planet details: only what's been recorded, counted in planets, by level
        // ("Frenzied" and "Aggressive" are both High sentinels).
        var notes = SystemNotesService.All;
        var details = PlanetDetailsService.All;
        var words = _vocabulary?.Words ?? [];
        static IEnumerable<ChoiceItem> Levels(IEnumerable<string> values) => values.Where(v => v.Length > 0)
            .GroupBy(v => v).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ChoiceItem($"{g.Key} ({g.Count():N0})", g.Key));
        Ship.Refill("Any", Levels(notes.SelectMany(n => n.Ships)));
        Sentinels.Refill("Any", Levels(details.Select(d => PlanetText.LevelOf(words, "Sentinels", d.Sentinels))));
        Flora.Refill("Any", Levels(details.Select(d => PlanetText.LevelOf(words, "Life", d.Flora))));
        Fauna.Refill("Any", Levels(details.Select(d => PlanetText.LevelOf(words, "Life", d.Fauna))));
        Resource.Refill("Any", Levels(details.SelectMany(d => d.Resources)));

        _quiet = true;
        _waterSystems = WaterSystems();
        int water = _waterSystems.Count, dissonant = notes.Count(n => n.Dissonant), moons = notes.Count(n => n.Moons is not null);
        WaterLabel = $"Water ({water} {(water == 1 ? "system" : "systems")})";
        ShowWater = water > 0;
        if (water == 0) WaterOnly = false;
        DissonantLabel = $"Dissonant ({dissonant} {(dissonant == 1 ? "system" : "systems")})";
        ShowDissonant = dissonant > 0;
        if (dissonant == 0) DissonantOnly = false;
        ShowBodies = moons > 0;
        if (moons == 0) BodiesIndex = 0;
        _quiet = false;
        HasNoteFilters = ChoiceFilters.Any(f => f.IsShown) || water > 0 || dissonant > 0 || moons > 0;
    }

    /// <summary>Systems known to have water: recorded as such (a Galaxy Map card, or the player's
    /// notes), a Water World among their planets, or a fish caught on one of them - you can't
    /// catch a fish without water (Waterworld, 2026-10-18).</summary>
    private HashSet<ulong> WaterSystems() => _all.GroupBy(p => p.SystemAddress)
        .Where(system => SystemNotesService.Get(system.Key)?.Water == true
            || system.Any(p => p.TypeName == "Water World" || PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex) == true
                || FishCatchService.CaughtOn(p.SystemAddress, p.PlanetIndex).Count > 0))
        .Select(system => system.Key).ToHashSet();

    /// <summary>Back to the whole list in one click (2026-10-13: a search left in the box quietly
    /// hid nearly every planet). Keeps the sort, save slots and hyperdrive range.</summary>
    public void ClearFilters()
    {
        _quiet = true;
        _searchDelay?.Cancel();
        Search = "";
        Type.Reset();
        Region.Reset();
        Galaxy.Reset();
        BodiesIndex = 0;
        foreach (var filter in ChoiceFilters) filter.Reset();
        RingsOnly = MineOnly = WaterOnly = DissonantOnly = false;
        _quiet = false;
        Apply();
    }

    private void Apply()
    {
        if (_quiet) return;
        int type = int.TryParse(Type.Value, out int t) ? t : -1;
        int galaxy = int.TryParse(Galaxy.Value, out int g) ? g : -1;
        ulong? region = ulong.TryParse(Region.Value, out ulong rk) ? rk : null;
        string search = Search.Trim();
        var words = _vocabulary?.Words ?? [];
        bool systemFilter = Star.IsSet || Economy.IsSet || Wealth.IsSet || Conflict.IsSet || Race.IsSet || WaterOnly || DissonantOnly;
        bool planetFilter = Sentinels.IsSet || Flora.IsSet || Fauna.IsSet || Resource.IsSet;

        var filtered = _all
            .Where(p => type < 0 || p.BiomeIndex == type)
            .Where(p => galaxy < 0 || p.Galaxy == galaxy)
            .Where(p => region is null || RegionMapService.RegionKey(p.SystemAddress) == region)
            .Where(p => !RingsOnly || p.HasRings)
            .Where(p => !MineOnly || p.DiscoveredHere)
            .Where(p => !systemFilter
                || ((!Star.IsSet || PlanetText.StarOf(p.SystemAddress) == Star.Value)
                    && (!Economy.IsSet || UniverseInfo.Economy(p.SystemAddress) == Economy.Value)
                    && (!Wealth.IsSet || UniverseInfo.Wealth(p.SystemAddress) == Wealth.Value)
                    && (!Conflict.IsSet || UniverseInfo.Conflict(p.SystemAddress) == Conflict.Value)
                    && (!Race.IsSet || PlanetText.RaceOf(p.SystemAddress) == Race.Value)
                    && (!WaterOnly || _waterSystems.Contains(p.SystemAddress))
                    && (!DissonantOnly || SystemNotesService.Get(p.SystemAddress)?.Dissonant == true)))
            .Where(p => !planetFilter
                || PlanetDetailsService.Get(p.SystemAddress, p.PlanetIndex) is { } d
                    && (!Sentinels.IsSet || PlanetText.LevelOf(words, "Sentinels", d.Sentinels) == Sentinels.Value)
                    && (!Flora.IsSet || PlanetText.LevelOf(words, "Life", d.Flora) == Flora.Value)
                    && (!Fauna.IsSet || PlanetText.LevelOf(words, "Life", d.Fauna) == Fauna.Value)
                    && (!Resource.IsSet || d.Resources.Contains(Resource.Value, StringComparer.OrdinalIgnoreCase)))
            .Where(p => !Ship.IsSet || SystemNotesService.Get(p.SystemAddress)?.Ships.Contains(Ship.Value, StringComparer.OrdinalIgnoreCase) == true)
            .Where(p => search.Length == 0 || PlanetText.SearchText(p).Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .Where(p => BodiesIndex switch
            {
                1 => !BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex),
                2 => BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex),
                _ => true,
            })
            .Select(p => (Planet: p, Distance: _here is ulong here ? NmsPlanets.DistanceLightYears(here, p.SystemAddress) : null));

        // Nearest first: same-galaxy planets by distance, other galaxies after.
        var ordered = SortIndex switch
        {
            1 => filtered.OrderByDescending(x => x.Planet.DiscoveredUnixSeconds),
            2 => filtered.OrderByDescending(x => NmsPlanets.DistanceToCoreLightYears(x.Planet.Address)),
            3 => filtered.OrderBy(x => NmsPlanets.DistanceToCoreLightYears(x.Planet.Address)),
            _ => filtered.OrderBy(x => x.Distance ?? double.MaxValue).ThenByDescending(x => x.Planet.DiscoveredUnixSeconds),
        };
        Results = ordered.Select(x => new PlanetRowViewModel(x.Planet, _here, x.Distance)).ToList();
        ResultCount = $"{Results.Count:N0} of {_all.Count:N0} planets";
        ActiveFilters = new[] { search.Length > 0, Type.IsSet, Galaxy.IsSet, Region.IsSet, BodiesIndex > 0, RingsOnly, MineOnly, WaterOnly, DissonantOnly }
            .Concat(ChoiceFilters.Select(f => f.IsSet)).Count(on => on);
    }
}
