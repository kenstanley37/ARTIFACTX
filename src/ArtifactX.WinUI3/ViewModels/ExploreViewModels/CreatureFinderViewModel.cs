using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// The Creature Finder (issue #6): creatures a player can adopt, from every system the saves know, ranked by the
/// pet-battle stats arena players choose by (owner, 2026-11-12: "the arena requires high stat creatures"). Filters by
/// Trait Potential, element, galaxy and whether this save discovered them; distances follow the game (Show).
/// </summary>
public sealed partial class CreatureFinderViewModel : ObservableObject
{
    /// <summary>Rows shown at most: the best few hundred are what players look through; filters narrow the rest.</summary>
    private const int MaxRows = 300;

    private List<KnownPlanet> _thisSave = new();
    private List<KnownPlanet> _otherSaves = new();
    private List<FoundCreature> _found = new();
    private JArray? _discoveries;
    private ulong? _here;
    private int _discoveryCount = -1;
    private int _loadVersion;
    private bool _quiet;
    private CancellationTokenSource? _searchDelay;

    public CreatureFinderViewModel()
    {
        Element.Changed += Apply;
        Galaxy.Changed += Apply;
    }

    public ChoiceFilter Element { get; } = new("Element", 180);
    public ChoiceFilter Galaxy { get; } = new("Galaxy", 220);

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial int SortIndex { get; set; }       // 0 Trait Potential, 1-3 one stat, 4 biggest, 5 nearest
    [ObservableProperty] public partial int SizeIndex { get; set; }       // 0 any, 1 Medium or bigger, 2 Large or bigger, 3 Huge only
    [ObservableProperty] public partial int PotentialIndex { get; set; }  // 0 any, 1 B or better, 2 A or better, 3 S only
    [ObservableProperty] public partial int DiscoveredIndex { get; set; } // 0 all, 1 not discovered yet, 2 discovered
    [ObservableProperty] public partial bool IncludeOtherSlots { get; set; } = true;

    [ObservableProperty] public partial IReadOnlyList<CreatureFinderRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string ResultCount { get; private set; } = "";
    [ObservableProperty] public partial string Status { get; private set; } = "";
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
    partial void OnPotentialIndexChanged(int value) => Apply();
    partial void OnSizeIndexChanged(int value) => Apply();
    partial void OnDiscoveredIndexChanged(int value) => Apply();
    partial void OnIncludeOtherSlotsChanged(bool value) => _ = RefreshAsync(++_loadVersion);

    /// <summary>Waits for a pause in typing, so the list isn't re-run per key.</summary>
    partial void OnSearchChanged(string value)
    {
        if (_quiet) return;
        _searchDelay?.Cancel();
        var delay = _searchDelay = new CancellationTokenSource();
        _ = Task.Delay(250, delay.Token).ContinueWith(_ => Apply(), delay.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public async Task LoadAsync()
    {
        int version = ++_loadVersion;
        _otherSaves = new();
        _discoveryCount = -1;
        await ShowAsync(await LiveSaveWatcher.GetLatestAsync(), version);
    }

    /// <summary>A save the game wrote: new discoveries are searched again, a new location re-measures distances.</summary>
    public Task ShowAsync(PlanetSnapshot snapshot) => ShowAsync(snapshot, ++_loadVersion);

    private async Task ShowAsync(PlanetSnapshot snapshot, int version)
    {
        var discoveries = snapshot.Discoveries as JArray;
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        int count = discoveries?.Count ?? 0;
        if (count == _discoveryCount && _found.Count > 0)
        {
            if (here != _here) { _here = here; Apply(); }
            return;
        }
        _discoveries = discoveries;
        _here = here;
        _discoveryCount = count;
        _thisSave = NmsPlanets.ReadAll(discoveries);
        await RefreshAsync(version);
    }

    private async Task RefreshAsync(int version)
    {
        if (IncludeOtherSlots && _otherSaves.Count == 0
            && SaveSessionManager.ActivePlatformFolder is string folder && SaveSessionManager.ActiveSlot is { } slot)
        {
            Status = "Reading your other save slots...";
            var others = await KnownPlanetsService.ReadOtherSlotsAsync(folder, slot.SlotId);
            if (version != _loadVersion) return;
            _otherSaves = others;
        }
        Status = "Working out the creatures on every planet you know...";
        var planets = IncludeOtherSlots ? _thisSave.Concat(_otherSaves).ToList() : _thisSave;
        var found = await CreatureFinderService.FindAsync(planets, _discoveries);
        if (version != _loadVersion) return; // a newer search started meanwhile
        _found = found;
        // Which creatures can be pets arrived with catalog format 83; older game data can't answer.
        bool oldGameData = PlanetTypesService.CreatureTables?.Creatures.Any(c => c.PetBattler is not null) != true;
        Status = oldGameData ? "Update your game data (Settings › Game data) to find creatures you can adopt."
            : found.Count == 0 ? "None of the planets in your saves has a creature you can adopt yet."
            : "";
        FillChoices();
        Apply();
    }

    private void FillChoices()
    {
        Element.Refill("Any element", _found.Where(f => f.Battle.Element.Length > 0).GroupBy(f => f.Battle.Element)
            .OrderBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => new ChoiceItem($"{g.Key} ({g.Count():N0})", g.Key)));
        Galaxy.Refill("All galaxies", _found.GroupBy(f => f.Planet.Galaxy).OrderBy(g => g.Key)
            .Select(g => new ChoiceItem($"{PlanetText.GalaxyLabel(g.Key)} ({g.Count():N0})", g.Key.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>Back to the whole list in one click; keeps the sort and save slots.</summary>
    public void ClearFilters()
    {
        _quiet = true;
        _searchDelay?.Cancel();
        Search = "";
        Element.Reset();
        Galaxy.Reset();
        PotentialIndex = 0;
        SizeIndex = 0;
        DiscoveredIndex = 0;
        _quiet = false;
        Apply();
    }

    private void Apply()
    {
        if (_quiet) return;
        string search = Search.Trim();
        int galaxy = int.TryParse(Galaxy.Value, out int g) ? g : -1;
        // Trait Potential is 0 = C ... 3 = S; "B or better" keeps 1 and up.
        int minPotential = PotentialIndex;

        var filtered = _found
            .Where(f => f.Battle.Potential >= minPotential)
            .Where(f => SizeIndex == 0 || f.Creature.Scale > 0 && f.Creature.SizeClass >= SizeIndex)
            .Where(f => !Element.IsSet || f.Battle.Element == Element.Value)
            .Where(f => galaxy < 0 || f.Planet.Galaxy == galaxy)
            .Where(f => DiscoveredIndex switch { 1 => !f.Discovered, 2 => f.Discovered, _ => true })
            .Where(f => search.Length == 0 || Matches(f, search))
            .Select(f => (Found: f, Distance: _here is ulong here ? NmsPlanets.DistanceLightYears(here, f.Planet.SystemAddress) : null))
            .ToList();

        // Best first: the chosen stat's class (or the overall potential), then the other classes, then the nearest.
        static int Total(FoundCreature f) => f.Battle.Stats.Sum(s => s.Class);
        var ordered = SortIndex switch
        {
            >= 1 and <= 3 => filtered.OrderByDescending(x => x.Found.Battle.Stats[SortIndex - 1].Class)
                .ThenByDescending(x => x.Found.Battle.Potential).ThenByDescending(x => Total(x.Found)),
            4 => filtered.OrderByDescending(x => x.Found.Height).ThenByDescending(x => x.Found.Battle.Potential),
            5 => filtered.OrderBy(x => x.Distance ?? double.MaxValue).ThenByDescending(x => x.Found.Battle.Potential),
            _ => filtered.OrderByDescending(x => x.Found.Battle.Potential).ThenByDescending(x => Total(x.Found)),
        };
        var rows = ordered.ThenBy(x => x.Distance ?? double.MaxValue).Take(MaxRows)
            .Select(x => new CreatureFinderRowViewModel(x.Found, _here, x.Distance)).ToList();
        Results = rows;
        ResultCount = filtered.Count > MaxRows
            ? $"Best {MaxRows:N0} of {filtered.Count:N0} creatures"
            : $"{filtered.Count:N0} of {_found.Count:N0} creatures";
        ActiveFilters = new[] { search.Length > 0, Element.IsSet, Galaxy.IsSet, PotentialIndex > 0, SizeIndex > 0, DiscoveredIndex > 0 }.Count(on => on);
    }

    private static bool Matches(FoundCreature f, string search) =>
        f.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || UniverseInfo.PlanetName(f.Planet.SystemAddress, f.Planet.PlanetIndex).Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || UniverseInfo.SystemName(f.Planet.SystemAddress).Contains(search, StringComparison.CurrentCultureIgnoreCase);
}
