using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Helpers.Fishing;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// Tools > Fishing: the fish that live on the planet you're on, from the game's fish table (CatalogService.GetFishAsync),
/// with your catches from the save's fishing records; the filters, the "still to catch" tiles, the You are here band, and
/// the "Where to catch it" panel for a clicked fish. Follows the game through FishCatchRecorder.SnapshotSeen (the page
/// passes each snapshot to OnSnapshot). Read-only on the save - marking fish caught stays on the Fishing Records page;
/// "Found it here" and "No water" go to the Finds and planet-water stores (FishingPlacesService).
/// </summary>
public sealed partial class FishingPageViewModel : ObservableObject
{
    private List<FishInfo> _fish = new();
    private FishingRules _rules = new([], []);
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private FishingPlace _place = FishingPlace.None;
    // Fish the game saved a catch of while this page was open, newest first, and whether it was the first ever: shown at
    // the top with a pill.
    private readonly List<(string Id, bool First)> _justCaught = new();
    private List<KnownPlanet>? _otherSaves;
    private FishInfo? _whereFish;
    private string _liveKey = "";
    private readonly Dictionary<string, string> _rowTitles = new();
    private bool _ready;
    // Set by the "Any planet with water" tile; cleared when a planet type is picked.
    private bool _anywhereOnly;
    private int _shownCaught, _shownTotal;

    private Dictionary<string, (uint Count, float Largest)> Caught => _place.Caught;

    // ---------------------------------------------------------------- filters

    public IReadOnlyList<PlanetTypeOption> PlanetTypes { get; } =
        new[] { new PlanetTypeOption("Where I am", PlanetTypeOption.WhereIAm), new PlanetTypeOption("All planet types", PlanetTypeOption.AllTypes) }
            .Concat(NmsPlanets.SelectableTypes.Select(b => new PlanetTypeOption(NmsPlanets.TypeName(b), b))).ToList();

    public IReadOnlyList<TextOption> ShowOptions { get; } =
        new TextOption[] { new("All fish", ""), new("Not caught yet", "uncaught"), new("Caught", "caught") };

    public IReadOnlyList<TextOption> TimeOptions { get; } =
        new TextOption[] { new("Any time", ""), new("Bites by day", "Day"), new("Bites at night", "Night") };

    public IReadOnlyList<TextOption> RarityOptions { get; } =
        new[] { new TextOption("Any rarity", "") }
            .Concat(new[] { "Common", "Rare", "Epic", "Legendary" }.Select(q => new TextOption(FishInfo.RarityName(q), q))).ToList();

    [ObservableProperty] public partial PlanetTypeOption? SelectedPlanetType { get; set; }
    [ObservableProperty] public partial TextOption? SelectedShow { get; set; }
    [ObservableProperty] public partial TextOption? SelectedTime { get; set; }
    [ObservableProperty] public partial TextOption? SelectedRarity { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IncludeAnywhere { get; set; } = true;

    public FishingPageViewModel()
    {
        SelectedPlanetType = PlanetTypes[0];
        SelectedShow = ShowOptions[0];
        SelectedTime = TimeOptions[0];
        SelectedRarity = RarityOptions[0];
    }

    partial void OnSelectedPlanetTypeChanged(PlanetTypeOption? value)
    {
        _anywhereOnly = false;
        Apply();
    }

    partial void OnSelectedShowChanged(TextOption? value) => Apply();
    partial void OnSelectedTimeChanged(TextOption? value) => Apply();
    partial void OnSelectedRarityChanged(TextOption? value) => Apply();
    partial void OnSearchTextChanged(string value) => Apply();
    partial void OnIncludeAnywhereChanged(bool value) => Apply();

    // ---------------------------------------------------------------- what's shown

    [ObservableProperty] public partial List<FishCardViewModel> Cards { get; private set; } = new();
    [ObservableProperty] public partial IReadOnlyList<FishProgressTileViewModel> Tiles { get; private set; } = [];
    [ObservableProperty] public partial bool ShowTiles { get; private set; }
    [ObservableProperty] public partial string CountText { get; private set; } = "";
    [ObservableProperty] public partial bool ShowClearFilters { get; private set; }
    [ObservableProperty] public partial string LiveStatus { get; private set; } = "Following your game";

    [ObservableProperty] public partial bool ShowHere { get; private set; }
    [ObservableProperty] public partial string HereTitle { get; private set; } = "";
    [ObservableProperty] public partial string HerePlace { get; private set; } = "";
    [ObservableProperty] public partial bool ShowHereProgress { get; private set; }
    [ObservableProperty] public partial string HereProgress { get; private set; } = "";

    // A catch the game just saved: without it a new catch only moved a card off screen and nudged the count (2026-10-21).
    [ObservableProperty] public partial bool CatchBarOpen { get; set; }
    [ObservableProperty] public partial string CatchBarTitle { get; private set; } = "";
    [ObservableProperty] public partial string CatchBarMessage { get; private set; } = "";

    // ---------------------------------------------------------------- loading and following the game

    public async Task LoadAsync()
    {
        // Only the 210 real species - junk catches and mission-only fish are left out, the same set the Fishing Records
        // page shows.
        _fish = (await CatalogService.GetFishAsync()).Where(f => CatalogService.IsFishSpecies(f.Id) && !f.NeedsMission).ToList();
        _rules = await FishingRules.LoadAsync();
        await CatalogService.WarmCacheAsync(_rules.Baits.Select(b => b.Id));
        _names = await CatalogService.GetItemNamesAsync();
        await CatalogService.WarmCacheAsync(_fish.Select(f => f.Id).Concat(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}")));

        if (SaveSessionManager.IsSaveLoaded)
        {
            var first = LiveSaveWatcher.FromSession();
            FishCatchService.Observe(FishCatchService.ActiveSaveKey(), first, _fish); // starting counts
            _place = FishingPlacesService.ReadPlace(first);
            _liveKey = LiveKey();
        }
        _ready = true;
        UpdateHere();
        Apply();
    }

    /// <summary>A new save snapshot; FishCatchRecorder has already recorded its catches.</summary>
    public void OnSnapshot(PlanetSnapshot snapshot)
    {
        var before = Caught;
        _place = FishingPlacesService.ReadPlace(snapshot);
        var landed = Caught
            .Where(c => !before.TryGetValue(c.Key, out var was) || c.Value.Count > was.Count)
            .Select(c => (Id: c.Key, First: !before.ContainsKey(c.Key)))
            .ToList();
        // Before the page has its starting counts, or a jump no rod makes (another save's records), every fish would
        // read as just caught.
        if (!_ready || landed.Count > 5) landed.Clear();
        LiveStatus = $"Following your game · updated {DateTime.Now:t}";
        // The game saves often; only touch the page when something it shows changed.
        string key = LiveKey();
        if (key == _liveKey) return;
        _liveKey = key;
        if (landed.Count > 0) NoteCatches(landed);
        UpdateHere();
        Apply();
        UpdateWhere();
        if (landed.Count > 0) ShowCatchBar(landed);
    }

    private void NoteCatches(List<(string Id, bool First)> landed)
    {
        foreach (var catchNow in landed)
        {
            // A repeat catch of a fish first caught a moment ago stays a first.
            int was = _justCaught.FindIndex(j => j.Id.Equals(catchNow.Id, StringComparison.OrdinalIgnoreCase));
            bool first = catchNow.First || (was >= 0 && _justCaught[was].First);
            if (was >= 0) _justCaught.RemoveAt(was);
            _justCaught.Insert(0, (catchNow.Id, first));
        }
    }

    /// <summary>"You caught your first Wispscale Darter" - the count line reads how many of the fish shown are now caught.</summary>
    private void ShowCatchBar(List<(string Id, bool First)> landed)
    {
        string Line((string Id, bool First) c) =>
            c.First ? $"your first {NameOf(c.Id)}" : $"a {NameOf(c.Id)} ({Caught[c.Id].Count:N0} times now)";
        CatchBarTitle = landed.Any(c => c.First) ? "New fish caught" : "Fish caught";
        CatchBarMessage = $"You caught {string.Join(", ", landed.Select(Line))}. {_shownCaught} of {_shownTotal} caught.";
        CatchBarOpen = true;
    }

    /// <summary>Everything the page shows that a new save can change: where you are, your catches, your known planets
    /// and bases.</summary>
    private string LiveKey() =>
        $"{_place.System}|{_place.Planet}|{_place.Biome}|{_place.Planets.Count}|{string.Join(",", _place.Bases.Keys.OrderBy(k => k))}|"
        + string.Join(",", Caught.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase).Select(c => $"{c.Key}:{c.Value.Count}:{c.Value.Largest}"));

    // ---------------------------------------------------------------- here

    /// <summary>The "Still to catch" tiles (user, 2026-10-13): the whole collection first, one per group of planet types
    /// that shares its own fish (e.g. Exotic/Red/Green/Blue), and one for the fish found on any planet with water. The
    /// tile for what the list is showing is outlined.</summary>
    private void UpdateTiles()
    {
        if (!SaveSessionManager.IsSaveLoaded)
        {
            ShowTiles = false;
            return;
        }
        var groups = _fish.Where(f => !f.Biomes.Contains(FishInfo.AnyPlanet))
            .GroupBy(f => string.Join(",", f.Biomes.OrderBy(b => b)))
            .Select(g => (Biomes: g.First().Biomes, Caught: g.Count(f => Caught.ContainsKey(f.Id)), Total: g.Count()))
            .OrderBy(g => NmsPlanets.TypeName(g.Biomes[0]), StringComparer.CurrentCulture)
            .ToList();
        var anywhere = _fish.Where(f => f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();

        int? typeTag = SelectedPlanetType?.Biome;
        int? selected = _anywhereOnly ? FishInfo.AnyPlanet : typeTag == PlanetTypeOption.WhereIAm ? _place.Biome : typeTag is >= 0 ? typeTag : null;

        var tiles = new List<FishProgressTileViewModel>
        {
            new("All fish", _fish.Count(f => Caught.ContainsKey(f.Id)), _fish.Count, PlanetTypeOption.AllTypes,
                !_anywhereOnly && typeTag == PlanetTypeOption.AllTypes, isOverall: true),
        };
        foreach (var g in groups)
            tiles.Add(new(NmsPlanets.TypeList(g.Biomes), g.Caught, g.Total, g.Biomes[0],
                selected is int sel && sel != FishInfo.AnyPlanet && g.Biomes.Contains(sel)));
        tiles.Add(new("Any planet with water", anywhere.Count(f => Caught.ContainsKey(f.Id)), anywhere.Count, FishInfo.AnyPlanet,
            selected == FishInfo.AnyPlanet));
        Tiles = tiles;
        ShowTiles = true;
    }

    /// <summary>Shows the uncaught fish of a clicked tile's planet type ("Any planet with water" shows only the fish that
    /// live anywhere).</summary>
    public void ShowTile(FishProgressTileViewModel tile)
    {
        int typeTag = tile.Biome == FishInfo.AnyPlanet || tile.Biome == PlanetTypeOption.AllTypes ? PlanetTypeOption.AllTypes : tile.Biome;
        _ready = false; // one Apply at the end, not one per filter
        SelectedPlanetType = PlanetTypes.FirstOrDefault(p => p.Biome == typeTag) ?? SelectedPlanetType;
        SelectedShow = ShowOptions[1]; // Not caught yet
        IncludeAnywhere = true;
        _anywhereOnly = tile.Biome == FishInfo.AnyPlanet; // after the type change, which clears it
        _ready = true;
        Apply();
    }

    private void UpdateHere()
    {
        UpdateTiles();
        string typeName = _place.Biome is int b ? NmsPlanets.TypeName(b) : "";
        PlanetTypes[0].Label = typeName.Length > 0 ? $"Where I am ({typeName})" : "Where I am";

        if (_place.System is not ulong system)
        {
            ShowHere = false;
            return;
        }
        ShowHere = true;
        string systemName = UniverseInfo.SystemName(system);
        string galaxy = PlanetText.GalaxyLabel(S.GalaxyNumber(system));

        if (_place.Planet == 0)
        {
            HereTitle = "In space";
            HerePlace = $"{systemName} · {galaxy} · save on a planet to see its fish";
            ShowHereProgress = false;
            return;
        }

        string planetName = UniverseInfo.PlanetName(system, _place.Planet);
        HereTitle = typeName.Length > 0 ? $"{planetName} · {typeName} planet" : planetName;
        HerePlace = typeName.Length > 0
            ? $"{systemName} · {galaxy} · {UniverseInfo.Compact(system)}"
            : $"{systemName} · {galaxy} · this planet's type isn't in the save yet - scan it, or pick a type below";

        if (_place.Biome is int biome)
        {
            var here = _fish.Where(f => f.LivesOn(biome)).ToList();
            HereProgress = $"{here.Count(f => Caught.ContainsKey(f.Id))} / {here.Count}";
            ShowHereProgress = true;
        }
        else ShowHereProgress = false;
    }

    // ---------------------------------------------------------------- list

    /// <summary>Filters and sorts the fish. Same cards in the same order are updated in place, so the grid keeps its scroll
    /// position and doesn't blink; otherwise Cards is replaced (the page keeps the scroll position).</summary>
    public void Apply()
    {
        if (!_ready) return;
        int typeTag = SelectedPlanetType?.Biome ?? PlanetTypeOption.WhereIAm;
        int? biome = typeTag switch
        {
            PlanetTypeOption.WhereIAm => _place.Biome,
            PlanetTypeOption.AllTypes => null,
            _ => typeTag,
        };
        string show = SelectedShow?.Key ?? "";
        string time = SelectedTime?.Key ?? "";
        string rarity = SelectedRarity?.Key ?? "";
        string text = SearchText.Trim();

        var planetFish = _fish.Where(f => biome is not int b || f.LivesOn(b))
            .Where(f => IncludeAnywhere || !f.Biomes.Contains(FishInfo.AnyPlanet))
            .Where(f => !_anywhereOnly || f.Biomes.Contains(FishInfo.AnyPlanet))
            .ToList();
        int? currentBiome = biome ?? _place.Biome;

        var shown = planetFish
            .Where(f => show.Length == 0 || (show == "caught") == Caught.ContainsKey(f.Id))
            .Where(f => time.Length == 0 || f.Time == time || f.Time == "Both")
            .Where(f => rarity.Length == 0 || f.Quality == rarity)
            .Where(f => text.Length == 0 || NameOf(f.Id).Contains(text, StringComparison.CurrentCultureIgnoreCase))
            // Current planet type's fish first, then other planet-type fish, then "anywhere" fish - keep each habitat
            // group contiguous (issue #53).
            .OrderBy(f => currentBiome is int cb && f.Biomes.Contains(cb) ? 0 : f.Biomes.Contains(FishInfo.AnyPlanet) ? 2 : 1)
            .ThenBy(f => string.Join(",", f.Biomes.OrderBy(b => b)))
            .ThenBy(f => JustCaughtRank(f.Id))
            .ThenBy(f => Caught.ContainsKey(f.Id))
            .ThenBy(f => FishLabels.RarityRank(f.Quality))
            .ThenBy(f => NameOf(f.Id), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var spots = FishingPlacesService.SpotNamesByFish();
        var cards = shown.Select(f => ToCard(f, spots)).ToList();
        if (Cards.Select(c => c.Id).SequenceEqual(cards.Select(c => c.Id)))
            for (int i = 0; i < cards.Count; i++) Cards[i].UpdateFrom(cards[i]);
        else Cards = cards;

        int caughtCount = planetFish.Count(f => Caught.ContainsKey(f.Id));
        (_shownCaught, _shownTotal) = (caughtCount, planetFish.Count);
        UpdateTiles(); // outline the tile for what's shown
        ShowClearFilters = FiltersSet();
        CountText = (_anywhereOnly ? "Fish found on any planet · " : "") + $"Showing {shown.Count} · {caughtCount} of {planetFish.Count} caught";
    }

    /// <summary>Anything picked away from how the page opens.</summary>
    private bool FiltersSet() =>
        (SelectedPlanetType ?? PlanetTypes[0]) != PlanetTypes[0] || (SelectedShow ?? ShowOptions[0]) != ShowOptions[0]
        || (SelectedTime ?? TimeOptions[0]) != TimeOptions[0] || (SelectedRarity ?? RarityOptions[0]) != RarityOptions[0]
        || SearchText.Length > 0 || !IncludeAnywhere || _anywhereOnly;

    /// <summary>Back to how the page opens: every fish for where you are.</summary>
    public void ClearFilters()
    {
        _ready = false; // one Apply at the end, not one per box
        SelectedPlanetType = PlanetTypes[0];
        SelectedShow = ShowOptions[0];
        SelectedTime = TimeOptions[0];
        SelectedRarity = RarityOptions[0];
        SearchText = "";
        IncludeAnywhere = true;
        _anywhereOnly = false;
        _ready = true;
        Apply();
    }

    /// <summary>Fish just caught come first, newest first.</summary>
    private int JustCaughtRank(string id)
    {
        int i = _justCaught.FindIndex(j => j.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? int.MaxValue : i;
    }

    private FishCardViewModel ToCard(FishInfo f, Dictionary<string, List<string>> spots)
    {
        bool caught = Caught.TryGetValue(f.Id, out var record);
        var traits = new List<string> { FishLabels.Size(f.Size), FishLabels.Time(f.Time) };
        if (f.NeedsStorm) traits.Add("Storms only");
        string habitat = f.Biomes.Contains(FishInfo.AnyPlanet) ? "Any planet with water" : NmsPlanets.TypeList(f.Biomes) + " planets";
        spots.TryGetValue(f.Id, out var spotList);
        int just = JustCaughtRank(f.Id);
        bool selected = f.Id == _whereFish?.Id;

        return new FishCardViewModel
        {
            Id = f.Id,
            Name = NameOf(f.Id),
            Icon = CatalogService.TryGet(f.Id)?.Icon,
            Rarity = f.RarityName().ToUpperInvariant(),
            RarityBrush = new SolidColorBrush(FishLabels.RarityColour(f.Quality)),
            Traits = string.Join(" · ", traits),
            Habitat = habitat,
            Depth = _rules.DepthText(f),
            Bait = _rules.BestBait(f) is { Length: > 0 } bait ? $"Best bait: {bait}" : "",
            Caught = caught
                ? $"Caught {record.Count:N0} time{(record.Count == 1 ? "" : "s")}{(record.Largest > 0 ? $" · largest {record.Largest:0.##} kg" : "")}"
                : "Not caught yet",
            JustCaught = just == int.MaxValue ? "" : _justCaught[just].First ? "NEW CATCH" : "JUST CAUGHT",
            CaughtBrush = ThemeResources.Brush(caught ? "ThemeDoneBrush" : "ThemeWarningBrush"),
            CardOpacity = caught && just == int.MaxValue ? 0.6 : 1.0,
            Spots = spotList is null ? "" : $"Your spots: {string.Join(", ", spotList)}",
            SpotsVisibility = spotList is null ? Visibility.Collapsed : Visibility.Visible,
            CardBorder = ThemeResources.Brush(selected ? "ThemeAccentBrush" : "ThemeLineBrush"),
            CardBackground = ThemeResources.Brush(selected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush"),
        };
    }

    /// <summary>The list as shown, for Export CSV.</summary>
    public (string[] Headers, IEnumerable<string[]> Rows) CsvRows() =>
        (new[] { "Fish", "Rarity", "Size and time", "Lives on", "Caught", "Your spots" },
            Cards.Select(r => new[] { r.Name, r.Rarity, r.Traits, r.Habitat, r.Caught, r.Spots.Replace("Your spots: ", "") }).ToList());

    private string NameOf(string id) =>
        _names.TryGetValue(id, out var n) ? n
        : CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } raw
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant()) : id;

    // ---------------------------------------------------------------- where to catch it

    [ObservableProperty] public partial bool ShowWhere { get; private set; }
    [ObservableProperty] public partial ImageSource? WhereIcon { get; private set; }
    [ObservableProperty] public partial string WhereName { get; private set; } = "";
    [ObservableProperty] public partial string WhereHabitat { get; private set; } = "";
    [ObservableProperty] public partial string WhereCountText { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<FishPlanetRowViewModel> WhereRows { get; private set; } = [];
    [ObservableProperty] public partial bool IncludeOtherSlots { get; set; } = true;
    [ObservableProperty] public partial bool ShowDry { get; set; }

    partial void OnIncludeOtherSlotsChanged(bool value)
    {
        UpdateWhere();
        _ = LoadOtherSavesAsync();
    }

    partial void OnShowDryChanged(bool value) => UpdateWhere();

    /// <summary>A fish card clicked: the Where to catch panel for it.</summary>
    public async Task SelectFishAsync(FishCardViewModel card)
    {
        _whereFish = _fish.FirstOrDefault(f => f.Id == card.Id);
        if (_whereFish is null) return;
        HighlightSelected();
        WhereIcon = card.Icon;
        WhereName = card.Name;
        WhereHabitat = $"{card.Habitat} · {card.Traits}";
        ShowWhere = true;
        UpdateWhere();
        await LoadOtherSavesAsync();
    }

    public void CloseWhere()
    {
        _whereFish = null;
        ShowWhere = false;
        HighlightSelected();
    }

    /// <summary>Marks the clicked fish's card (accent border and fill) without rebuilding the grid.</summary>
    private void HighlightSelected()
    {
        foreach (var card in Cards)
        {
            bool selected = card.Id == _whereFish?.Id;
            card.CardBorder = ThemeResources.Brush(selected ? "ThemeAccentBrush" : "ThemeLineBrush");
            card.CardBackground = ThemeResources.Brush(selected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush");
        }
    }

    /// <summary>Reads the other save slots' planets once (KnownPlanetsService caches them by file time), only when the
    /// panel first needs them.</summary>
    private async Task LoadOtherSavesAsync()
    {
        if (!IncludeOtherSlots || _otherSaves is not null || _whereFish is null) return;
        if (SaveSessionManager.ActivePlatformFolder is not string folder || SaveSessionManager.ActiveSlot is not { } slot) return;
        WhereCountText = "Reading your other save slots...";
        _otherSaves = await KnownPlanetsService.ReadOtherSlotsAsync(folder, slot.SlotId);
        UpdateWhere();
    }

    /// <summary>Every known planet whose type the fish lives on. Order: planets where you've caught it (recorded or
    /// marked), then planets known to have water (your other fishing spots, and Swamp / Water World planets, which always
    /// do), then planets with your bases, then nearest. The save doesn't record water for any other planet, so the
    /// list says so.</summary>
    public void UpdateWhere()
    {
        if (_whereFish is not FishInfo fish) return;
        bool anyPlanet = fish.Biomes.Contains(FishInfo.AnyPlanet);
        var spots = FishingPlacesService.SpotsByPlanet();
        var planets = (IncludeOtherSlots && _otherSaves is not null
            ? _place.Planets.Concat(_otherSaves).GroupBy(p => p.Address).Select(g => g.First())
            : _place.Planets).ToList();

        // Spots marked for this fish always show, even on a planet these saves don't know.
        var known = planets.Select(p => FishingPlacesService.SpotKey(p.PortalCode, p.Galaxy)).ToHashSet();
        foreach (var (key, spot) in spots)
            if (!known.Contains(key) && spot.ItemIds.Contains(fish.Id, StringComparer.OrdinalIgnoreCase))
                planets.Add(FishingPlacesService.SpotPlanet(spot));

        // Planets where ArtifactX recorded this fish being caught (FishCatchService) come first with the marked spots -
        // real catches, the data to share through the API later (2026-10-15).
        var caughtAt = FishCatchService.WhereCaught(fish.Id).ToDictionary(c => (c.System, c.Planet), c => c.Count);
        int CaughtCount(KnownPlanet p) => caughtAt.GetValueOrDefault((p.SystemAddress, p.PlanetIndex));

        int Rank(KnownPlanet p)
        {
            if (CaughtCount(p) > 0) return 0;
            if (spots.TryGetValue(FishingPlacesService.SpotKey(p.PortalCode, p.Galaxy), out var spot))
                return spot.ItemIds.Contains(fish.Id, StringComparer.OrdinalIgnoreCase) ? 0 : 1;
            if (PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex) == true || NmsPlanets.AlwaysHasWater(p.BiomeIndex)) return 1;
            return _place.Bases.ContainsKey(p.Address) ? 2 : 3;
        }

        // Planets the player found dry leave every list unless they ask to see them.
        int dry = 0;
        bool Wet(KnownPlanet p)
        {
            if (PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex) != false || NmsPlanets.AlwaysHasWater(p.BiomeIndex)) return true;
            dry++;
            return ShowDry;
        }

        var rows = planets.Where(p => fish.LivesOn(p.BiomeIndex) || Rank(p) == 0)
            .Where(Wet)
            .Select(p => (Planet: p, Rank: Rank(p), Distance: _place.System is ulong h ? NmsPlanets.DistanceLightYears(h, p.SystemAddress) : null))
            .OrderBy(x => x.Rank)
            .ThenBy(x => _place.Bases.ContainsKey(x.Planet.Address) ? 0 : 1)
            .ThenBy(x => x.Distance ?? double.MaxValue)
            .ToList();

        _rowTitles.Clear();
        var shown = rows.Take(100).Select(x => ToWhereRow(x.Planet, x.Distance, spots, fish, CaughtCount(x.Planet))).ToList();
        WhereRows = shown;
        int marked = rows.Count(x => x.Rank == 0);
        int wet = rows.Count(x => x.Rank == 1);
        bool anyUnknown = rows.Any(x => x.Rank > 1);
        string where = anyPlanet ? "planets" : NmsPlanets.TypeList(fish.Biomes, " or ") + " planets";
        WhereCountText = (rows.Count == 0
            ? $"You haven't discovered any {where} on {(IncludeOtherSlots ? "your saves" : "this save")} yet."
            : $"{rows.Count:N0} known {where}{(rows.Count > shown.Count ? $", nearest {shown.Count} shown" : "")}"
              + (marked > 0 ? $" - {marked} where you've caught it (recorded or marked), listed first." : " - mark a planet with Found it here once you catch it there."))
            + (wet > 0 ? $" {wet:N0} {(wet == 1 ? "is" : "are")} known to have water and listed {(marked > 0 ? "next" : "first")}." : "")
            + (anyUnknown ? " Your save doesn't record water for other planets, so look for lakes or seas from orbit." : "")
            + (dry > 0 && !ShowDry ? $" {dry} you marked as having no water {(dry == 1 ? "is" : "are")} hidden." : "");
    }

    /// <summary>"Found it here" on a planet row (key: FishingPlacesService.SpotKey).</summary>
    public void ToggleCaughtHere(string key)
    {
        if (_whereFish is not FishInfo fish) return;
        FishingPlacesService.ToggleCaughtHere(fish.Id, key, _rowTitles.GetValueOrDefault(key) ?? "Fishing spot");
        UpdateWhere();
        Apply();
    }

    /// <summary>"No water" / "Has water" on a planet row (key: FishingPlacesService.WaterKey).</summary>
    public void ToggleNoWater(string key)
    {
        FishingPlacesService.ToggleNoWater(key);
        UpdateWhere();
    }

    private FishPlanetRowViewModel ToWhereRow(KnownPlanet p, double? distance, Dictionary<string, Find> spots, FishInfo fish, int caughtTimes = 0)
    {
        string planetName = UniverseInfo.PlanetName(p.SystemAddress, p.PlanetIndex);
        bool isMoon = BodyKindsService.IsMoon(p.SystemAddress, p.PlanetIndex);
        var detail = new List<string>
        {
            p.Classification(PlanetTypesService.Descriptor(p.SystemAddress, p.PlanetIndex), ringSuffix: " · rings", isMoon: isMoon),
            UniverseInfo.SystemName(p.SystemAddress),
            UniverseInfo.Compact(p.SystemAddress),
            PlanetText.GalaxyLabel(p.Galaxy),
        };
        if (p.Source.Length > 0) detail.Add(p.Source);

        string distanceText = _place.System is ulong here && p.SystemAddress == here
            ? (p.PlanetIndex == _place.Planet ? "You're on this planet" : "In the system you're in")
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
        _place.Bases.TryGetValue(p.Address, out var baseName);

        bool dry = PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex) == false;
        bool alwaysWet = NmsPlanets.AlwaysHasWater(p.BiomeIndex);
        string key = FishingPlacesService.SpotKey(p.PortalCode, p.Galaxy);
        _rowTitles[key] = planetName.StartsWith("Planet ", StringComparison.Ordinal) && baseName is not null ? baseName : planetName;
        spots.TryGetValue(key, out var spot);
        bool markedHere = spot?.ItemIds.Contains(fish.Id, StringComparer.OrdinalIgnoreCase) == true;
        var otherFish = spot?.ItemIds.Where(i => !string.Equals(i, fish.Id, StringComparison.OrdinalIgnoreCase)).Select(NameOf).ToList() ?? new();
        string spotText = caughtTimes > 0 ? $"Caught here {caughtTimes} time{(caughtTimes == 1 ? "" : "s")} (recorded while ArtifactX was open)"
            : markedHere ? "You've caught it here"
            : otherFish.Count > 0 ? $"Your fishing spot (water) - caught here: {string.Join(", ", otherFish)}"
            : "";
        // Spots already say so; otherwise say why water is known.
        string waterText = spotText.Length > 0 ? ""
            : alwaysWet ? $"\U0001F4A7 Has water: {p.TypeName} planets always do"
            : PlanetWaterService.Get(p.SystemAddress, p.PlanetIndex) == true ? "\U0001F4A7 Has water: you've fished here"
            : "";

        return new FishPlanetRowViewModel
        {
            Title = planetName,
            Detail = string.Join(" · ", detail),
            Distance = distanceText,
            Swatch = new SolidColorBrush(FishLabels.ParseColour(NmsPlanets.TypeColour(p.BiomeIndex))),
            PortalCode = p.PortalCode,
            BaseText = baseName is null ? "" : $"Your base: {baseName}",
            BaseVisibility = baseName is null ? Visibility.Collapsed : Visibility.Visible,
            SpotText = spotText,
            SpotVisibility = spotText.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            MarkKey = key,
            MarkText = markedHere ? "✓ Caught here" : "Found it here",
            CardBorder = ThemeResources.Brush(markedHere ? "ThemeAccentBrush" : "ThemeLineBrush"),
            WaterKey = FishingPlacesService.WaterKey(p.SystemAddress, p.PlanetIndex),
            WaterButtonText = dry ? "Has water" : "No water",
            NoWaterVisibility = dry && !alwaysWet ? Visibility.Visible : Visibility.Collapsed,
            WaterButtonVisibility = alwaysWet ? Visibility.Collapsed : Visibility.Visible,
            WaterText = waterText,
            WaterVisibility = waterText.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    // ---------------------------------------------------------------- overview toggle

    // The info, You are here and planet-type boxes can be hidden so the fish list and Where to catch get the room
    // (2026-10-15); remembered across visits.
    private const string OverviewHiddenKey = "FishingOverviewHidden";

    private static bool OverviewHidden => SettingsStore.Get(SettingsStore.Scope.Profile, OverviewHiddenKey) == "1";

    public bool ShowOverview => !OverviewHidden;
    public string OverviewToggleText => OverviewHidden ? "Show overview" : "Hide overview";
    public string OverviewToggleGlyph => OverviewHidden ? "" : "";

    public void ToggleOverview()
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, OverviewHiddenKey, OverviewHidden ? null : "1");
        OnPropertyChanged(nameof(ShowOverview));
        OnPropertyChanged(nameof(OverviewToggleText));
        OnPropertyChanged(nameof(OverviewToggleGlyph));
    }
}
