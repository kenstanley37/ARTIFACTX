using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Planet Finder: every planet in the discoveries of this save and the account's other
/// save slots, and every body in the systems of the player's region and the regions touching
/// it worked out by the game's generator (<see cref="NearbyIndex"/>; a save keeps only so many
/// discoveries, so it can't map a region on its own), searched and filtered by what the save
/// knows, what the generator says about each system, and what the player recorded. Distances are from where the
/// save is now, so it follows the game (Show). The galaxy picker starts on (and follows)
/// the galaxy the player is in until they pick another: a portal can't reach other galaxies.
/// The search box, planet type, rings and infested show at once; the rest sits behind
/// More filters (<see cref="ShowMoreFilters"/>), whose label counts the ones in use.
/// </summary>
public sealed partial class PlanetFinderViewModel : ObservableObject
{
    private List<KnownPlanet> _thisSave = new();
    private List<KnownPlanet> _otherSaves = new();
    private List<KnownPlanet> _all = new();
    private HashSet<ulong> _waterSystems = new();
    private HashSet<(ulong System, int Planet)> _infested = new();
    private int _infestedVersion;
    private PlanetVocabulary? _vocabulary;
    private ulong? _here;
    private int _loadVersion;
    private bool _quiet;
    private CancellationTokenSource? _searchDelay;
    /// <summary>The galaxy picker follows the player's galaxy (until they pick another one).</summary>
    private bool _galaxyFollowsYou = true;
    private const string MoreFiltersSetting = "PlanetFinderMoreFilters";
    private const string NearbySetting = "PlanetFinderNearby";
    /// <summary>The bodies worked out around the player (<see cref="LoadNearbyAsync"/>), and for which region and reach.</summary>
    private List<KnownPlanet> _nearby = new();
    private (ulong Region, int Reach)? _nearbyFor;
    private int _nearbyVersion;
    private int _applyVersion;
    /// <summary>The most result cards built at once: the list is sorted, and a card per body for
    /// tens of thousands of generated planets would stall the page. Filters narrow it.</summary>
    private const int MaxCards = 500;

    public PlanetFinderViewModel()
    {
        foreach (var filter in ChoiceFilters.Append(Type).Append(Galaxy).Append(Region)) filter.Changed += Apply;
        Galaxy.Changed += () => _galaxyFollowsYou = !GalaxyMoved;
        JumpPlanner.RangeChanged += Apply;
        ShowMoreFilters = SettingsStore.Get(SettingsStore.Scope.Profile, MoreFiltersSetting) == "1";
        NearbyIndex = int.TryParse(SettingsStore.Get(SettingsStore.Scope.Profile, NearbySetting), out int nearby) && nearby is >= 0 and <= 2 ? nearby : 0;
    }

    /// <summary>Every picker's width, so the filter groups line up in columns.</summary>
    private const double FilterWidth = 220;

    public ChoiceFilter Type { get; } = new("Planet type", FilterWidth);
    /// <summary>The Type choice for every haunted world (<see cref="NmsPlanets.IsHaunted"/>).</summary>
    private const string HauntedType = "haunted";
    public ChoiceFilter Galaxy { get; } = new("Galaxy", FilterWidth);
    public ChoiceFilter Region { get; } = new("Region", FilterWidth);
    public ChoiceFilter Star { get; } = new("Star colour", FilterWidth);
    public ChoiceFilter Economy { get; } = new("Economy", FilterWidth);
    public ChoiceFilter Wealth { get; } = new("Wealth", FilterWidth);
    public ChoiceFilter Conflict { get; } = new("Conflict", FilterWidth);
    public ChoiceFilter Race { get; } = new("Race", FilterWidth);
    public ChoiceFilter Ship { get; } = new("Ships seen", FilterWidth);
    public ChoiceFilter Sentinels { get; } = new("Sentinels", FilterWidth);
    public ChoiceFilter Flora { get; } = new("Flora", FilterWidth);
    public ChoiceFilter Fauna { get; } = new("Fauna", FilterWidth);
    public ChoiceFilter Resource { get; } = new("Resource", FilterWidth);

    /// <summary>The filters made from recorded and generated system and planet facts.</summary>
    public IReadOnlyList<ChoiceFilter> ChoiceFilters => (ChoiceFilter[])[.. SystemFilters, .. PlanetFilters];
    /// <summary>The star system group's pickers, in the order shown.</summary>
    public IReadOnlyList<ChoiceFilter> SystemFilters => (ChoiceFilter[])[Star, Economy, Wealth, Conflict, Race, Ship];
    /// <summary>The planet group's pickers made from what the player recorded, in the order shown.</summary>
    public IReadOnlyList<ChoiceFilter> PlanetFilters => (ChoiceFilter[])[Sentinels, Flora, Fauna, Resource];

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial int SortIndex { get; set; }      // 0 nearest, 1 newest, 2 furthest from core, 3 nearest to core
    [ObservableProperty] public partial int BodiesIndex { get; set; }    // 0 all, 1 planets only, 2 moons only
    [ObservableProperty] public partial bool RingsOnly { get; set; }
    [ObservableProperty] public partial bool MineOnly { get; set; }
    /// <summary>The less used filters are open (remembered).</summary>
    [ObservableProperty] public partial bool ShowMoreFilters { get; set; }
    /// <summary>Which undiscovered planets to add: 0 your region and the regions touching it (remembered), 1 your
    /// region, 2 none (your saves only).</summary>
    [ObservableProperty] public partial int NearbyIndex { get; set; }
    /// <summary>"More filters", or "More filters (2 on)" when some of them are in use, open or not.</summary>
    [ObservableProperty] public partial string MoreFiltersLabel { get; private set; } = "More filters";
    [ObservableProperty] public partial bool WaterOnly { get; set; }
    [ObservableProperty] public partial bool DissonantOnly { get; set; }
    [ObservableProperty] public partial bool InfestedOnly { get; set; }
    /// <summary>"Infested (1,980 planets)": how many the other filters leave (planet type included), like
    /// <see cref="RingsLabel"/> and the planet type counts (<see cref="ApplyAsync"/>).</summary>
    [ObservableProperty] public partial string InfestedLabel { get; private set; } = "";
    [ObservableProperty] public partial string RingsLabel { get; private set; } = "Rings only";
    [ObservableProperty] public partial bool ShowInfested { get; private set; }

    [ObservableProperty] public partial string WaterLabel { get; private set; } = "";
    [ObservableProperty] public partial bool ShowWater { get; private set; }
    [ObservableProperty] public partial string DissonantLabel { get; private set; } = "";
    [ObservableProperty] public partial bool ShowDissonant { get; private set; }
    [ObservableProperty] public partial bool ShowBodies { get; private set; }
    /// <summary>No planet has recorded sentinels, life, resources or moons yet, so the planet group says how to get those filters.</summary>
    [ObservableProperty] public partial bool ShowPlanetNotesHint { get; private set; }
    [ObservableProperty] public partial string Status { get; private set; } = "";

    [ObservableProperty] public partial IReadOnlyList<PlanetRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string ResultCount { get; private set; } = "";
    /// <summary>"Showing the first 500 of 23,000..." when there are more results than cards, else "".</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasShownNote))] public partial string ShownNote { get; private set; } = "";
    public bool HasShownNote => ShownNote.Length > 0;
    [ObservableProperty] public partial int ActiveFilters { get; private set; }

    public bool HasStatus => Status.Length > 0;
    public bool HasActiveFilters => ActiveFilters > 0;
    public string ClearLabel => $"Clear filters ({ActiveFilters})";
    /// <summary>The search card's band, so a folded card still says filters are on.</summary>
    public string FilterSummary => ActiveFilters switch { 0 => "No filters", 1 => "1 filter on", var n => $"{n} filters on" };

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnActiveFiltersChanged(int value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ClearLabel));
        OnPropertyChanged(nameof(FilterSummary));
    }

    partial void OnSortIndexChanged(int value) => Apply();
    partial void OnBodiesIndexChanged(int value) => Apply();
    partial void OnRingsOnlyChanged(bool value) => Apply();
    partial void OnMineOnlyChanged(bool value) => Apply();
    partial void OnWaterOnlyChanged(bool value) => Apply();
    partial void OnDissonantOnlyChanged(bool value) => Apply();
    partial void OnInfestedOnlyChanged(bool value) => Apply();
    partial void OnShowMoreFiltersChanged(bool value) => SettingsStore.Set(SettingsStore.Scope.Profile, MoreFiltersSetting, value ? "1" : null);
    partial void OnNearbyIndexChanged(int value)
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, NearbySetting, value == 0 ? null : value.ToString(CultureInfo.InvariantCulture));
        _ = LoadNearbyAsync();
    }

    /// <summary>The galaxy the save is in, as the galaxy picker's value ("" without a save).</summary>
    private string HereGalaxy => _here is ulong here ? S.GalaxyNumber(here).ToString(CultureInfo.InvariantCulture) : "";
    /// <summary>The player picked a galaxy other than the one they're in, or All galaxies while theirs is listed (with
    /// no known planet in their galaxy, All galaxies is where it rests).</summary>
    private bool GalaxyMoved => HereGalaxy.Length == 0 ? Galaxy.IsSet
        : Galaxy.IsSet ? Galaxy.Value != HereGalaxy : Galaxy.Choices.Any(c => c.Value == HereGalaxy);

    private void FollowGalaxy() { if (_galaxyFollowsYou) Galaxy.Select(HereGalaxy); }

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
        if (_otherSaves.Count > 0) return;
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
        var known = _thisSave.Concat(_otherSaves).GroupBy(p => p.Address).Select(g => g.First()).ToList();
        var have = known.Select(p => (p.SystemAddress, p.PlanetIndex)).ToHashSet();
        _all = [.. known, .. _nearby.Where(p => !have.Contains((p.SystemAddress, p.PlanetIndex)))];
        FillChoices();
        _ = FindInfestedAsync();
        Apply();
        _ = LoadNearbyAsync();
    }

    /// <summary>
    /// Every body in the systems of the player's region (and the regions touching it, <see cref="NearbyIndex"/>),
    /// from the game's generator (NearbySystems, PlanetTypesService.Undiscovered: the same port the Ingredient Finder
    /// searches with), worked out off the UI thread and again only when the player reaches another region. A save
    /// keeps a limited number of discoveries, so older ones drop out and a region can't be mapped from the save alone
    /// (the owner, 2026-11-14).
    /// </summary>
    private async Task LoadNearbyAsync()
    {
        int reach = NearbyIndex switch { 0 => 1, 1 => 0, _ => -1 };
        (ulong, int)? want = _here is ulong here && reach >= 0 ? (RegionMapService.RegionKey(here), reach) : null;
        if (want == _nearbyFor) return;
        int version = ++_nearbyVersion;
        if (want is not (ulong region, int radius))
        {
            _nearbyFor = null;
            if (_nearby.Count > 0) { _nearby = new(); Refresh(); }
            return;
        }
        await PlanetTypesService.EnsureLoadedAsync();
        if (await CatalogService.GetGalaxyGenerationSettingsAsync() is not { } settings) return;
        if (version != _nearbyVersion) return;
        Status = "Working out the planets around you...";
        var found = await Task.Run(() =>
        {
            var bodies = new ConcurrentBag<KnownPlanet>();
            // About 10,000 systems and 47,000 bodies with the regions around (build 185034, 2026-11-14: 0.4 s).
            Parallel.ForEach(NearbySystems.Around(region, radius, settings), system =>
            {
                UniverseInfo.For(system.SystemAddress); // the star, economy and race pickers read these on the UI thread
                foreach (var body in PlanetTypesService.Undiscovered(system.SystemAddress, Array.Empty<int>())) bodies.Add(body);
            });
            return bodies.ToList();
        });
        if (version != _nearbyVersion) return;
        Status = "";
        _nearby = found;
        _nearbyFor = want;
        Refresh();
    }

    /// <summary>
    /// Which planets are infested (the infested variant of their type, where Whispering Eggs and ground worms are -
    /// <see cref="NmsPlanets.InfestedSubType"/>). The save records only the type, so it comes from the game's
    /// generator (PlanetTypesService.For), off the UI thread; the checkbox and list update when it's done.
    /// </summary>
    private async Task FindInfestedAsync()
    {
        int version = ++_infestedVersion;
        var planets = _all;
        await PlanetTypesService.EnsureLoadedAsync();
        var infested = await Task.Run(() => planets
            .Where(p => PlanetTypesService.For(p.SystemAddress) is var types
                && p.PlanetIndex >= 1 && p.PlanetIndex <= types.Length && types[p.PlanetIndex - 1].SubType == NmsPlanets.InfestedSubType)
            .Select(p => (p.SystemAddress, p.PlanetIndex)).ToHashSet());
        if (version != _infestedVersion) return;
        bool changed = !infested.SetEquals(_infested);
        _infested = infested;
        bool any = planets.Any(p => infested.Contains((p.SystemAddress, p.PlanetIndex)));
        ShowInfested = any;
        if (!any && InfestedOnly) InfestedOnly = false;
        else if (changed) Apply(); // the infested and planet type counts
    }

    private void FillChoices()
    {
        Region.Refill("All regions", _all.GroupBy(p => RegionMapService.RegionKey(p.SystemAddress))
            .OrderByDescending(g => g.Count()).ThenBy(g => UniverseInfo.Region(g.First().SystemAddress), StringComparer.CurrentCulture)
            .Select(g => new ChoiceItem($"{UniverseInfo.Region(g.First().SystemAddress)} ({g.Count():N0})", g.Key.ToString(CultureInfo.InvariantCulture))));
        Galaxy.Refill("All galaxies", _all.GroupBy(p => p.Galaxy).OrderBy(g => g.Key)
            .Select(g => new ChoiceItem($"{PlanetText.GalaxyLabel(g.Key)} · {g.Count():N0}", g.Key.ToString(CultureInfo.InvariantCulture))));
        FollowGalaxy();

        // Star, economy, wealth, conflict and race come from the game's generator for every
        // planet the finder knows (the player's notes override), counted in planets.
        var systems = _all.GroupBy(p => p.SystemAddress).Select(g => (System: g.Key, Planets: g.Count())).ToList();
        IEnumerable<ChoiceItem> Known(Func<ulong, string> value) => systems
            .Select(s => (Value: value(s.System), s.Planets)).Where(v => v.Value.Length > 0 && v.Value != "None")
            .GroupBy(v => v.Value).Select(g => (g.Key, Count: g.Sum(v => v.Planets)))
            .OrderByDescending(g => g.Count).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ChoiceItem($"{g.Key} ({g.Count:N0})", g.Key));
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
        ShowPlanetNotesHint = !PlanetFilters.Any(f => f.IsShown) && moons == 0;
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
    /// hid nearly every planet), in the galaxy the player is in. Keeps the sort and hyperdrive range.</summary>
    public void ClearFilters()
    {
        _quiet = true;
        _searchDelay?.Cancel();
        Search = "";
        Type.Reset();
        Region.Reset();
        _galaxyFollowsYou = true;
        FollowGalaxy();
        BodiesIndex = 0;
        foreach (var filter in ChoiceFilters) filter.Reset();
        RingsOnly = MineOnly = WaterOnly = DissonantOnly = InfestedOnly = false;
        _quiet = false;
        Apply();
    }

    private void Apply() => _ = ApplyAsync();

    /// <summary>
    /// The planet type choices, counted in what the other filters leave (2026-11-14, the owner: with Infested ticked,
    /// the list should only offer the types that come infested). A type with none is left out, except the one picked,
    /// which stays (with 0) so a filter change never swaps the player's choice behind their back.
    /// </summary>
    private void FillTypes(IReadOnlyDictionary<int, int> counts)
    {
        int Count(int type) => counts.TryGetValue(type, out int n) ? n : 0;
        string picked = Type.Value;
        int haunted = counts.Where(c => NmsPlanets.IsHaunted(c.Key)).Sum(c => c.Value);
        Type.Refill($"All types ({counts.Values.Sum():N0})", NmsPlanets.SelectableTypes
            .Select(t => (Type: t, Value: t.ToString(CultureInfo.InvariantCulture), Count: Count(t)))
            .Where(t => t.Count > 0 || t.Value == picked)
            .Select(t => new ChoiceItem($"{NmsPlanets.TypeName(t.Type)} ({t.Count:N0})", t.Value))
            // The game's fishing text sends players to "haunted homeworlds": Exotic and the three anomalies together.
            .Concat(haunted > 0 || picked == HauntedType ? (ChoiceItem[])[new ChoiceItem($"{NmsPlanets.HauntedName} ({haunted:N0})", HauntedType)] : (ChoiceItem[])[]));
    }

    /// <summary>
    /// Filters and sorts off the UI thread (tens of thousands of bodies with the generated ones), then builds cards for
    /// the first <see cref="MaxCards"/>. Every setting is read here first; a newer run drops an older one's result.
    /// </summary>
    private async Task ApplyAsync()
    {
        if (_quiet) return;
        int version = ++_applyVersion;
        bool hauntedOnly = Type.Value == HauntedType;
        int type = int.TryParse(Type.Value, out int t) ? t : -1;
        int galaxy = int.TryParse(Galaxy.Value, out int g) ? g : -1;
        ulong? region = ulong.TryParse(Region.Value, out ulong rk) ? rk : null;
        string search = Search.Trim();
        var words = _vocabulary?.Words ?? [];
        var all = _all;
        ulong? here = _here;
        var infested = _infested;
        var water = _waterSystems;
        bool rings = RingsOnly, infestedOnly = InfestedOnly, mine = MineOnly, waterOnly = WaterOnly, dissonant = DissonantOnly;
        int bodies = BodiesIndex, sort = SortIndex;
        string star = Star.Value, economy = Economy.Value, wealth = Wealth.Value, conflict = Conflict.Value, race = Race.Value, ship = Ship.Value;
        string sentinels = Sentinels.Value, flora = Flora.Value, fauna = Fauna.Value, resource = Resource.Value;
        bool systemFilter = star.Length > 0 || economy.Length > 0 || wealth.Length > 0 || conflict.Length > 0 || race.Length > 0 || waterOnly || dissonant;
        bool planetFilter = sentinels.Length > 0 || flora.Length > 0 || fauna.Length > 0 || resource.Length > 0;

        int more = new[] { GalaxyMoved, region is not null, bodies > 0, mine, waterOnly, dissonant }
            .Concat(ChoiceFilters.Select(f => f.IsSet)).Count(on => on);
        MoreFiltersLabel = more > 0 ? $"More filters ({more} on)" : "More filters";
        ActiveFilters = more + new[] { search.Length > 0, type >= 0 || hauntedOnly, rings, infestedOnly }.Count(on => on);

        var (shown, total, typeCounts, infestedCount, ringsCount) = await Task.Run(() =>
        {
            bool TypeOk(KnownPlanet p) => (type < 0 || p.BiomeIndex == type) && (!hauntedOnly || NmsPlanets.IsHaunted(p.BiomeIndex));
            bool IsInfested(KnownPlanet p) => infested.Contains((p.SystemAddress, p.PlanetIndex));
            // Everything but planet type, rings and infested, whose counts each leave out their own filter.
            var others = all
                .Where(p => galaxy < 0 || p.Galaxy == galaxy)
                .Where(p => region is null || RegionMapService.RegionKey(p.SystemAddress) == region)
                .Where(p => !mine || p.DiscoveredHere)
                .Where(p => !systemFilter
                    || ((star.Length == 0 || PlanetText.StarOf(p.SystemAddress) == star)
                        && (economy.Length == 0 || UniverseInfo.Economy(p.SystemAddress) == economy)
                        && (wealth.Length == 0 || UniverseInfo.Wealth(p.SystemAddress) == wealth)
                        && (conflict.Length == 0 || UniverseInfo.Conflict(p.SystemAddress) == conflict)
                        && (race.Length == 0 || PlanetText.RaceOf(p.SystemAddress) == race)
                        && (!waterOnly || water.Contains(p.SystemAddress))
                        && (!dissonant || SystemNotesService.Get(p.SystemAddress)?.Dissonant == true)))
                .Where(p => !planetFilter
                    || PlanetDetailsService.Get(p.SystemAddress, p.PlanetIndex) is { } d
                        && (sentinels.Length == 0 || PlanetText.LevelOf(words, "Sentinels", d.Sentinels) == sentinels)
                        && (flora.Length == 0 || PlanetText.LevelOf(words, "Life", d.Flora) == flora)
                        && (fauna.Length == 0 || PlanetText.LevelOf(words, "Life", d.Fauna) == fauna)
                        && (resource.Length == 0 || d.Resources.Contains(resource, StringComparer.OrdinalIgnoreCase)))
                .Where(p => ship.Length == 0 || SystemNotesService.Get(p.SystemAddress)?.Ships.Contains(ship, StringComparer.OrdinalIgnoreCase) == true)
                // A generated body is searched by its names and type only: its descriptor would mean working out every
                // nearby system's weather and creatures for one search.
                .Where(p => search.Length == 0 || (p.Predicted ? PlanetText.NameText(p) : PlanetText.SearchText(p)).Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .Where(p => bodies switch
                {
                    1 => !BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex),
                    2 => BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex),
                    _ => true,
                })
                .ToList();
            var typeCounts = others.Where(p => (!rings || p.HasRings) && (!infestedOnly || IsInfested(p)))
                .GroupBy(p => p.BiomeIndex).ToDictionary(g => g.Key, g => g.Count());
            int infestedCount = others.Count(p => TypeOk(p) && (!rings || p.HasRings) && IsInfested(p));
            int ringsCount = others.Count(p => TypeOk(p) && (!infestedOnly || IsInfested(p)) && p.HasRings);
            var filtered = others.Where(p => TypeOk(p) && (!rings || p.HasRings) && (!infestedOnly || IsInfested(p)))
                .Select(p => (Planet: p, Distance: here is ulong h ? NmsPlanets.DistanceLightYears(h, p.SystemAddress) : null));

            // Nearest first: same-galaxy planets by distance, other galaxies after.
            var ordered = (sort switch
            {
                1 => filtered.OrderByDescending(x => x.Planet.DiscoveredUnixSeconds),
                2 => filtered.OrderByDescending(x => NmsPlanets.DistanceToCoreLightYears(x.Planet.Address)),
                3 => filtered.OrderBy(x => NmsPlanets.DistanceToCoreLightYears(x.Planet.Address)),
                _ => filtered.OrderBy(x => x.Distance ?? double.MaxValue).ThenByDescending(x => x.Planet.DiscoveredUnixSeconds),
            }).ToList();
            // What the cards read that the generator works out (the descriptor needs the system's profile), done here.
            foreach (var (p, _) in ordered.Take(MaxCards)) PlanetTypesService.Descriptor(p.SystemAddress, p.PlanetIndex);
            return (ordered.Take(MaxCards).ToList(), ordered.Count, typeCounts, infestedCount, ringsCount);
        });
        if (version != _applyVersion) return;
        _quiet = true;
        FillTypes(typeCounts);
        _quiet = false;
        InfestedLabel = $"Infested ({infestedCount:N0} {(infestedCount == 1 ? "planet" : "planets")})";
        RingsLabel = $"Rings only ({ringsCount:N0})";
        Results = shown.Select(x => new PlanetRowViewModel(x.Planet, here, x.Distance)).ToList();
        ResultCount = total == all.Count ? $"{total:N0} planets" : $"{total:N0} of {all.Count:N0} planets";
        ShownNote = total > shown.Count ? $"Showing the first {shown.Count:N0} of {total:N0}. Pick a planet type or other filters to narrow the list." : "";
    }
}
