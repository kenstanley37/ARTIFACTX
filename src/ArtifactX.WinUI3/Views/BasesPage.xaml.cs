using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only list of the player's planet bases (the save's base list, F?0,
/// entries whose type peI.DPp is "HomePlanetBase") with portal addresses.
/// Each base's oZw is a packed address whose top nibble is the planet. Planet
/// type comes from the matching discovery record when the save has one.
/// </summary>
public sealed partial class BasesPage : Page
{
    private static readonly string[] BasesPath = { "vLc", "6f=", "F?0" };
    private List<BaseRowViewModel> _rows = new();
    private readonly LiveSaveWatcher _live;
    private string _lastKey = "";

    public const string CategoryFreighter = "Freighter Base";
    public const string CategoryPlanetary = "Planetary Bases";
    public const string CategoryStations = "Claimed Space Stations & Outposts";
    public const string CategorySettlements = "Settlements";

    private bool _freighterExpanded = true;
    private bool _planetaryExpanded = true;
    private bool _stationsExpanded = true;
    private bool _settlementsExpanded = true;

    public BasesPage()
    {
        InitializeComponent();
        SortBox.SelectedIndex = 0;
        _live = new LiveSaveWatcher(DispatcherQueue);
        _live.SnapshotRead += OnLiveSnapshot;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        JumpPlanner.RangeChanged += OnRangeChanged;
        PlanetDetailsService.Captured += OnDataChanged;
        PlanetDetailsService.Changed += OnDataChanged;
        PlanetWaterService.Changed += OnDataChanged;
        FindsService.Changed += OnFindsChanged;
        SystemNotesService.Changed += OnSystemChanged;
        SystemPageReader.Captured += OnSystemChanged;
        CreatureListService.Changed += OnCreaturesChanged;
        Unloaded += (_, _) =>
        {
            JumpPlanner.RangeChanged -= OnRangeChanged;
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            _live.SnapshotRead -= OnLiveSnapshot;
            PlanetDetailsService.Captured -= OnDataChanged;
            PlanetDetailsService.Changed -= OnDataChanged;
            PlanetWaterService.Changed -= OnDataChanged;
            FindsService.Changed -= OnFindsChanged;
            SystemNotesService.Changed -= OnSystemChanged;
            SystemPageReader.Captured -= OnSystemChanged;
            CreatureListService.Changed -= OnCreaturesChanged;
            _live.Dispose();
        };
        _ = LoadAsync();
    }

    private void OnLiveSnapshot(PlanetSnapshot snapshot)
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        Build(snapshot);
    }

    private void OnDataChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) Build(_lastSnapshot);
    });

    private void OnSystemChanged(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) Build(_lastSnapshot);
    });

    private void OnFindsChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) Build(_lastSnapshot);
    });

    private void OnCreaturesChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) Build(_lastSnapshot);
    });

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        await CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));
        _lastKey = "";
        Build(LiveSaveWatcher.FromSession());
        _live.Start();
    }

    /// <summary>Builds the rows from a snapshot; skipped when neither your
    /// position nor the base list changed, so the list doesn't reset.</summary>
    private PlanetSnapshot? _lastSnapshot;

    /// <summary>Rebuilds the rows so their distances pick up the new jump estimate.</summary>
    private void OnRangeChanged()
    {
        if (_lastSnapshot is null) return;
        _lastKey = "";
        Build(_lastSnapshot);
    }

    private void Build(PlanetSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        bool objects = _showObjects;

        int baseCount = (snapshot.Bases ?? new JArray()).OfType<JObject>()
            .Count(x => !((x["peI"]?["DPp"]?.Value<string>() ?? "").StartsWith("External", StringComparison.Ordinal)));
        int settlementCount = (snapshot.Settlements ?? new JArray()).OfType<JObject>()
            .Count(s => !string.IsNullOrEmpty(s["NKm"]?.Value<string>())
                && (string.IsNullOrEmpty(snapshot.LocalOnlineId) || s["3?K"]?["K7E"]?.Value<string>() == snapshot.LocalOnlineId));

        BasesTab.Text = $"Bases ({baseCount + settlementCount:N0})";
        ObjectsTab.Text = $"Placed objects ({snapshot.LooseObjects?.Count ?? 0:N0})";

        string key = $"{objects}|{here}|{snapshot.Bases?.Count}|{snapshot.LooseObjects?.Count}|{snapshot.Settlements?.Count}|{NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects)}|{string.Join(",", snapshot.Bases?.OfType<JObject>().Select(b => b["NKm"]?.Value<string>()) ?? Enumerable.Empty<string?>())}|{string.Join(",", snapshot.Settlements?.OfType<JObject>().Select(s => s["NKm"]?.Value<string>()) ?? Enumerable.Empty<string?>())}";
        if (key == _lastKey) return;
        _lastKey = key;

        // Parts placed: the game caps them per base and in total (2026-10-18).
        var counts = NmsBuildCounts.PerBase(snapshot.Bases).ToDictionary(c => c.Base, c => c.Parts);
        int total = NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects);
        PartsTotalTxt.Text = $"Building parts placed: {NmsBuildCounts.GlobalLabel(total)} before the game's construction limit";
        PartsTotalTxt.Foreground = new SolidColorBrush(LevelColour(NmsBuildCounts.GlobalLevel(total)));
        PartsBreakdownTxt.Text = NmsBuildCounts.Breakdown(snapshot.Bases, snapshot.LooseObjects);

        var planets = NmsPlanets.ReadAll(snapshot.Discoveries)
            .GroupBy(p => p.Address).ToDictionary(g => g.Key, g => g.First());

        _rows = new();
        if (objects)
        {
            BuildPlacedObjects(snapshot, here, planets);
            Apply();
            return;
        }

        // 1. Freighter Base (pinned at top)
        if (snapshot.Bases is JArray allBases)
        {
            foreach (var b in allBases.OfType<JObject>())
            {
                if (b["peI"]?["DPp"]?.Value<string>() != "FreighterBase") continue;
                ulong address = S.ParsePackedAddress(b["oZw"]) ?? here ?? 0;
                int planetIndex = (int)((address >> 52) & 0xF);
                int galaxy = address != 0 ? S.GalaxyNumber(address) : 1;
                string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Capital Freighter Base";
                string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
                double? distance = (here is ulong h && address != 0) ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = address != 0 ? S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1) : "";
                int parts = counts.TryGetValue(b, out int p) ? p : 0;

                _rows.Add(new BaseRowViewModel
                {
                    Name = name,
                    Detail = address != 0 ? $"Capital Ship · {UniverseInfo.SystemName(address)} · {galaxyName}" : "Capital Ship · In Transit",
                    PartsLabel = NmsBuildCounts.UploadLabel(parts),
                    PartsBrush = new SolidColorBrush(LevelColour(NmsBuildCounts.UploadLevel(parts))),
                    Facts = address != 0 ? UniverseInfo.Compact(address) : "Mobile Capital Ship",
                    Swatch = new SolidColorBrush(ParseColour("#7E57C2")), // Freighter indigo
                    PortalCode = code,
                    Glyphs = GlyphsFor(code),
                    DistanceLabel = address != 0 ? DistanceText(here, address, distance) : "Mobile Capital Ship",
                    AreaLabel = "Mobile capital ship base with bridge command operations, storage rooms, refiners, and fleet teleporter.",
                    AreaOpacity = 1.0,
                    SortDistance = distance ?? double.MaxValue,
                    Galaxy = galaxy,
                    Base = b,
                    Picture = address != 0 ? Picture(address, name) : null,
                    Category = CategoryFreighter,
                });
            }
        }

        // 2. Planetary Bases
        if (snapshot.Bases is JArray planetBases)
        {
            foreach (var b in planetBases.OfType<JObject>())
            {
                if (b["peI"]?["DPp"]?.Value<string>() != "HomePlanetBase") continue;
                if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;

                int planetIndex = (int)((address >> 52) & 0xF);
                int galaxy = S.GalaxyNumber(address);
                planets.TryGetValue(address, out var planet);
                string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
                string type = planet is null ? "Planet" : planet.Classification(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Descriptor, ringSuffix: " · rings");
                string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, planetIndex);
                int parts = counts.TryGetValue(b, out int p) ? p : 0;

                _rows.Add(new BaseRowViewModel
                {
                    Name = name,
                    Detail = $"{UniverseInfo.PlanetName(address, planetIndex)} ({type})"
                        + (NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "")
                        + $" · {UniverseInfo.SystemName(address)} · {galaxyName}",
                    PartsLabel = NmsBuildCounts.UploadLabel(parts),
                    PartsBrush = new SolidColorBrush(LevelColour(NmsBuildCounts.UploadLevel(parts))),
                    Facts = UniverseInfo.Compact(address),
                    Swatch = new SolidColorBrush(ParseColour(planet is null ? "#777777" : NmsPlanets.TypeColour(planet.BiomeIndex))),
                    PortalCode = code,
                    Glyphs = GlyphsFor(code),
                    DistanceLabel = DistanceText(here, address, distance),
                    AreaLabel = AreaText(address, planetIndex, galaxy, code) is { Length: > 0 } area ? area
                        : "Nothing recorded about this planet yet - add its resources, sentinels and notes on the Planets page.",
                    AreaOpacity = AreaText(address, planetIndex, galaxy, code).Length > 0 ? 1.0 : 0.55,
                    SortDistance = distance ?? double.MaxValue,
                    Galaxy = galaxy,
                    Base = b,
                    Picture = Picture(address, name),
                    Category = CategoryPlanetary,
                });
            }
        }

        // 3. Claimed Space Stations & Outposts
        if (snapshot.Bases is JArray stationBases)
        {
            foreach (var b in stationBases.OfType<JObject>())
            {
                string typeStr = b["peI"]?["DPp"]?.Value<string>() ?? "";
                if (typeStr == "HomePlanetBase" || typeStr == "FreighterBase" || typeStr.StartsWith("External", StringComparison.Ordinal)) continue;
                if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;

                int galaxy = S.GalaxyNumber(address);
                string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : (typeStr == "PlayerSpaceStationBase" ? "Claimed Space Station" : "Space Outpost");
                string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, 1);
                int parts = counts.TryGetValue(b, out int p) ? p : 0;

                _rows.Add(new BaseRowViewModel
                {
                    Name = name,
                    Detail = $"Space Station Directorate · {UniverseInfo.SystemName(address)} · {galaxyName}",
                    PartsLabel = $"{parts:N0} parts placed · Station Directorate",
                    PartsBrush = new SolidColorBrush(LevelColour(NmsBuildCounts.UploadLevel(parts))),
                    Facts = UniverseInfo.Compact(address),
                    Swatch = new SolidColorBrush(ParseColour("#FF9800")), // Station amber
                    PortalCode = code,
                    Glyphs = GlyphsFor(code),
                    DistanceLabel = DistanceText(here, address, distance),
                    AreaLabel = $"Claimed station in {UniverseInfo.SystemName(address)} ({UniverseInfo.RaceLabel(address)}). Station core terminal, concession shops, and landing bays.",
                    AreaOpacity = 1.0,
                    SortDistance = distance ?? double.MaxValue,
                    Galaxy = galaxy,
                    Base = b,
                    Picture = Picture(address, name),
                    Category = CategoryStations,
                });
            }
        }

        // 4. Settlements
        string localOnlineId = snapshot.LocalOnlineId
            ?? SaveSessionManager.GetValue(NmsSettlementPaths.LocalPlayerOnlineIdPath)?.Value<string>()
            ?? "";

        if (snapshot.Settlements is JArray settlements)
        {
            foreach (var s in settlements.OfType<JObject>())
            {
                string name = s["NKm"]?.Value<string>() ?? "";
                if (string.IsNullOrEmpty(name)) continue;

                string ownerOnlineId = s["3?K"]?["K7E"]?.Value<string>() ?? "";
                if (!string.IsNullOrEmpty(localOnlineId) && ownerOnlineId != localOnlineId) continue;

                if (S.ParsePackedAddress(s["yhJ"]) is not ulong address || address == 0) continue;

                int planetIndex = (int)((address >> 52) & 0xF);
                int galaxy = S.GalaxyNumber(address);
                planets.TryGetValue(address, out var planet);
                string planetType = planet is null ? "Colony Planet" : planet.Classification(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Descriptor, ringSuffix: " · rings");
                string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1);
                int population = s["x3<"]?.Value<int>() ?? 0;

                string area = AreaText(address, planetIndex, galaxy, code);
                if (string.IsNullOrEmpty(area))
                    area = $"Player-managed colony on {UniverseInfo.PlanetName(address, planetIndex)} in {UniverseInfo.SystemName(address)} ({UniverseInfo.RaceLabel(address)}).";

                _rows.Add(new BaseRowViewModel
                {
                    Name = name,
                    Detail = $"Settlement · {UniverseInfo.PlanetName(address, planetIndex)} ({planetType}) · {UniverseInfo.SystemName(address)} · {galaxyName}",
                    PartsLabel = $"Population {population:N0} citizens · Overseer",
                    PartsBrush = new SolidColorBrush(ParseColour("#00BCD4")),
                    Facts = UniverseInfo.Compact(address),
                    Swatch = new SolidColorBrush(ParseColour(planet is null ? "#00BCD4" : NmsPlanets.TypeColour(planet.BiomeIndex))),
                    PortalCode = code,
                    Glyphs = GlyphsFor(code),
                    DistanceLabel = DistanceText(here, address, distance),
                    AreaLabel = area,
                    AreaOpacity = 1.0,
                    SortDistance = distance ?? double.MaxValue,
                    Galaxy = galaxy,
                    Base = null,
                    Picture = Picture(address, name),
                    Category = CategorySettlements,
                });
            }
        }

        Apply();
    }

    /// <summary>Everything placed outside a base (the save's c5s list): beacons, signal
    /// boosters, save points, refiners, garages, decor - easily forgotten, and each one counts
    /// toward the game's building cap (user, 2026-10-18). Each has its type, its planet's
    /// address, its position from the planet's centre (so the scanner's coordinates) and the
    /// time it was placed.</summary>
    private void BuildPlacedObjects(PlanetSnapshot snapshot, ulong? here, Dictionary<ulong, KnownPlanet> planets)
    {
        var placed = (snapshot.LooseObjects ?? new JArray()).OfType<JObject>().ToList();
        var ids = placed.Select(o => o["r<7"]?.Value<string>()).Where(id => id is not null).Distinct().ToList();
        if (!_warmedObjects && ids.Any(id => CatalogService.TryGet(id) is null))
        {
            // Names and icons for these ids aren't loaded yet: load them, then build again.
            _warmedObjects = true;
            _ = CatalogService.WarmCacheAsync(ids).ContinueWith(_ => DispatcherQueue.TryEnqueue(() =>
            {
                _lastKey = "";
                if (_lastSnapshot is not null) Build(_lastSnapshot);
            }), System.Threading.Tasks.TaskScheduler.Default);
        }
        foreach (var o in placed)
        {
            if (S.ParsePackedAddress(o["oZw"]) is not ulong address) continue;
            string id = o["r<7"]?.Value<string>() ?? "";
            var entry = CatalogService.TryGet(id);
            string name = entry?.DisplayName is { Length: > 0 } n ? n
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.TrimStart('^').Replace('_', ' ').ToLowerInvariant());
            int planetIndex = (int)((address >> 52) & 0xF);
            int galaxy = S.GalaxyNumber(address);
            planets.TryGetValue(address, out var planet);
            string type = planet is null ? "Planet" : planet.Classification(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Descriptor);
            string galaxyName = NmsGalaxyNames.ForNumber(galaxy) is string g ? $"{g} ({galaxy})" : $"Galaxy {galaxy}";
            double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
            string code = S.PortalAddress(address, planetIndex);
            long placedAt = o["b1:"]?.Value<long>() ?? 0;
            _rows.Add(new BaseRowViewModel
            {
                Name = name,
                Detail = $"{UniverseInfo.PlanetName(address, planetIndex)} ({type})"
                    + (NmsPlanetCoordinates.OfBase(o) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "")
                    + $" · {UniverseInfo.SystemName(address)} · {galaxyName}",
                Icon = entry?.Icon,
                Facts = placedAt > 0 ? $"Placed {DateTimeOffset.FromUnixTimeSeconds(placedAt).LocalDateTime:d}" : "",
                Swatch = new SolidColorBrush(ParseColour(planet is null ? "#777777" : NmsPlanets.TypeColour(planet.BiomeIndex))),
                PortalCode = code,
                Glyphs = GlyphsFor(code),
                DistanceLabel = DistanceText(here, address, distance),
                AreaLabel = AreaText(address, planetIndex, galaxy, code),
                AreaOpacity = 1.0,
                SortDistance = distance ?? double.MaxValue,
                Galaxy = galaxy,
            });
        }
    }

    private bool _warmedObjects;

    private bool _showObjects;

    // Base pictures from the game's cache, by planet address; loaded once, then the cards rebuilt.
    private readonly Dictionary<ulong, Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap?> _pictures = new();
    private readonly HashSet<ulong> _picturesLoading = new();

    private ImageSource? Picture(ulong address, string name)
    {
        if (_pictures.TryGetValue(address, out var picture)) return picture;
        if (_picturesLoading.Add(address))
            _ = LoadPictureAsync(address, name);
        return null;
    }

    private async System.Threading.Tasks.Task LoadPictureAsync(ulong address, string name)
    {
        _pictures[address] = await GameScreenshotCache.LoadAsync(SaveSessionManager.ActivePlatformFolder, address, name);
        if (_pictures[address] is null || _lastSnapshot is null) return;
        _lastKey = "";
        Build(_lastSnapshot);
    }

    private void ShowBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        _showObjects = sender.SelectedItem == ObjectsTab;
        if (IntroTxt is null) return;
        IntroTxt.Text = _showObjects
            ? "Everything you've built outside a base - beacons, signal boosters, save points, refiners, garages - with where it is, so you can go back for it. Each one counts toward the game's building limit. This page only reads your save."
            : "Every planet base in this save, with the portal address to dial to get back to it. Handy for old bases you've lost track of. This page only reads your save.";
        _lastKey = "";
        if (_lastSnapshot is not null) Build(_lastSnapshot);
    }

    /// <summary>"Resources: Paraffinium, Copper, Silver, Ancient Bones · Infrequent
    /// sentinels · Painfully Hot Rain · Water · 13 kinds of fish caught here ·
    /// 2 finds · Ships seen: Solar" - from the planet's details (PlanetDetailsService),
    /// water, recorded catches, Finds and the system's notes. Empty when nothing
    /// is recorded. User request 2026-10-14: see what an area has without
    /// needing its glyphs (bases are a teleport away).</summary>
    private static string AreaText(ulong address, int planet, int galaxy, string code)
    {
        ulong system = S.ToSystemAddress(address);
        var parts = new List<string>();
        var d = PlanetDetailsService.Get(system, planet);
        if (d is not null)
        {
            if (d.Resources.Count > 0) parts.Add($"Resources: {string.Join(", ", d.Resources)}");
            if (d.Sentinels != "") parts.Add($"{d.Sentinels} sentinels");
            if (d.Weather != "") parts.Add(d.Weather);
            if (d.Flora != "") parts.Add($"Flora: {d.Flora}");
            if (d.Fauna != "") parts.Add($"Fauna: {d.Fauna}");
        }
        if (PlanetWaterService.Get(system, planet) == true) parts.Add("Water");
        int fish = FishCatchService.CaughtOn(system, planet).Count;
        if (fish > 0) parts.Add(fish == 1 ? "1 kind of fish caught here" : $"{fish} kinds of fish caught here");
        int finds = FindsService.All.Count(f => f.PortalCode == code && f.Galaxy == galaxy);
        if (finds > 0) parts.Add(finds == 1 ? "1 find" : $"{finds} finds");
        if (SystemNotesService.Get(system)?.Ships is { Count: > 0 } ships) parts.Add($"Ships seen: {string.Join(", ", ships)}");
        if (d is not null) parts.AddRange(d.Notes);
        return string.Join(" · ", parts);
    }

    /// <summary>"About 1,131 ly from you · 742,100 ly from the galaxy core".
    /// Distances to another galaxy can't be measured, so only the core distance shows.</summary>
    private static string DistanceText(ulong? here, ulong address, double? distance)
    {
        string core = $"{NmsPlanets.DistanceToCoreLightYears(address):N0} ly from the galaxy core";
        string fromYou = (here is ulong h && S.ToSystemAddress(address) == h) ? "In the system you're in"
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
        return $"{fromYou} · {core}";
    }

    private void Apply()
    {
        if (SearchBox is null || FreighterItemsList is null) return;
        string search = SearchBox.Text.Trim();
        bool isSearching = search.Length > 0;

        // Names and what's recorded about the area ("copper" finds bases near copper).
        var shown = _rows.Where(r => !isSearching || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || (r.AreaOpacity > 0.9 && r.AreaLabel.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            || r.Detail.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || r.Facts.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || r.Category.Contains(search, StringComparison.CurrentCultureIgnoreCase));

        shown = SortBox.SelectedIndex switch
        {
            1 => shown.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => shown.OrderBy(r => r.Galaxy).ThenBy(r => r.SortDistance),
            _ => shown.OrderBy(r => r.SortDistance).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        var list = shown.ToList();

        // Bases and placed objects have their own card layouts.
        BasesCategorizedScroll.Visibility = _showObjects ? Visibility.Collapsed : Visibility.Visible;
        ObjectsList.Visibility = _showObjects ? Visibility.Visible : Visibility.Collapsed;

        if (_showObjects)
        {
            ObjectsList.ItemsSource = list;
            CountTxt.Text = _rows.Count == 0 ? "Nothing placed outside a base" : $"{list.Count} of {_rows.Count} placed objects";
            return;
        }

        // Split filtered list by category
        var freighters = list.Where(r => r.Category == CategoryFreighter).ToList();
        var planetaries = list.Where(r => r.Category == CategoryPlanetary).ToList();
        var stations = list.Where(r => r.Category == CategoryStations).ToList();
        var settlements = list.Where(r => r.Category == CategorySettlements).ToList();

        int totalFreighters = _rows.Count(r => r.Category == CategoryFreighter);
        int totalPlanetaries = _rows.Count(r => r.Category == CategoryPlanetary);
        int totalStations = _rows.Count(r => r.Category == CategoryStations);
        int totalSettlements = _rows.Count(r => r.Category == CategorySettlements);

        FreighterItemsList.ItemsSource = freighters;
        PlanetaryItemsList.ItemsSource = planetaries;
        StationsItemsList.ItemsSource = stations;
        SettlementsItemsList.ItemsSource = settlements;

        FreighterCountTxt.Text = isSearching ? $"{freighters.Count} of {totalFreighters}" : $"{freighters.Count}";
        PlanetaryCountTxt.Text = isSearching ? $"{planetaries.Count} of {totalPlanetaries}" : $"{planetaries.Count}";
        StationsCountTxt.Text = isSearching ? $"{stations.Count} of {totalStations}" : $"{stations.Count}";
        SettlementsCountTxt.Text = isSearching ? $"{settlements.Count} of {totalSettlements}" : $"{settlements.Count}";

        // Summaries
        int freighterParts = freighters.Sum(f => f.Base is JObject b ? (b["@ZJ"] as JArray)?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0 : 0);
        FreighterSummaryTxt.Text = freighters.Count > 0 ? $"Mobile capital ship base · {freighterParts:N0} parts placed" : "No capital ship base recorded";

        int planetParts = planetaries.Sum(p => p.Base is JObject b ? (b["@ZJ"] as JArray)?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0 : 0);
        PlanetarySummaryTxt.Text = planetaries.Count > 0 ? $"{planetParts:N0} parts placed across {planetaries.Count:N0} planet bases" : "No planetary bases recorded";

        int stationParts = stations.Sum(s => s.Base is JObject b ? (b["@ZJ"] as JArray)?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0 : 0);
        StationsSummaryTxt.Text = stations.Count > 0 ? $"{stations.Count:N0} claimed facilities · {stationParts:N0} parts total" : "No claimed stations in this save";

        SettlementsSummaryTxt.Text = settlements.Count > 0 ? $"{settlements.Count:N0} governed colonies" : "No settlements governed";

        // Visibility of empty messages
        FreighterEmptyTxt.Visibility = freighters.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        PlanetaryEmptyTxt.Visibility = planetaries.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        StationsEmptyTxt.Visibility = stations.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        SettlementsEmptyTxt.Visibility = settlements.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;

        // During search, auto-expand categories that have matches, and hide categories with 0 matches
        if (isSearching)
        {
            FreighterSectionCard.Visibility = freighters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PlanetarySectionCard.Visibility = planetaries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            StationsSectionCard.Visibility = stations.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SettlementsSectionCard.Visibility = settlements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (freighters.Count > 0) _freighterExpanded = true;
            if (planetaries.Count > 0) _planetaryExpanded = true;
            if (stations.Count > 0) _stationsExpanded = true;
            if (settlements.Count > 0) _settlementsExpanded = true;
        }
        else
        {
            FreighterSectionCard.Visibility = Visibility.Visible;
            PlanetarySectionCard.Visibility = Visibility.Visible;
            StationsSectionCard.Visibility = Visibility.Visible;
            SettlementsSectionCard.Visibility = Visibility.Visible;
        }

        UpdateCategoryExpansions();

        CountTxt.Text = _rows.Count == 0 ? "No bases or settlements in this save"
            : $"{list.Count} of {_rows.Count} bases & settlements";
    }

    private void CategoryToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string category) return;
        switch (category)
        {
            case "Freighter":
                _freighterExpanded = !_freighterExpanded;
                break;
            case "Planetary":
                _planetaryExpanded = !_planetaryExpanded;
                break;
            case "Stations":
                _stationsExpanded = !_stationsExpanded;
                break;
            case "Settlements":
                _settlementsExpanded = !_settlementsExpanded;
                break;
        }
        UpdateCategoryExpansions();
    }

    private void UpdateCategoryExpansions()
    {
        if (FreighterContentArea is null) return;
        FreighterContentArea.Visibility = _freighterExpanded ? Visibility.Visible : Visibility.Collapsed;
        FreighterChevron.Glyph = _freighterExpanded ? "\uE70E" : "\uE70D";

        PlanetaryContentArea.Visibility = _planetaryExpanded ? Visibility.Visible : Visibility.Collapsed;
        PlanetaryChevron.Glyph = _planetaryExpanded ? "\uE70E" : "\uE70D";

        StationsContentArea.Visibility = _stationsExpanded ? Visibility.Visible : Visibility.Collapsed;
        StationsChevron.Glyph = _stationsExpanded ? "\uE70E" : "\uE70D";

        SettlementsContentArea.Visibility = _settlementsExpanded ? Visibility.Visible : Visibility.Collapsed;
        SettlementsChevron.Glyph = _settlementsExpanded ? "\uE70E" : "\uE70D";
    }

    private void BaseCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var cur = dep;
            while (cur != null && !ReferenceEquals(cur, sender))
            {
                if (cur is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase) return;
                cur = VisualTreeHelper.GetParent(cur);
            }
        }
        if ((sender as FrameworkElement)?.DataContext is BaseRowViewModel row)
        {
            ShowDetail(row);
        }
    }

    private void OpenCard_Click(object sender, RoutedEventArgs e)
    {
        var row = (sender as FrameworkElement)?.DataContext as BaseRowViewModel
            ?? (sender as FrameworkElement)?.Tag as BaseRowViewModel;
        if (row != null) ShowDetail(row);
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_showObjects)
        {
            var rows = ObjectsList.ItemsSource as List<BaseRowViewModel> ?? new();
            await CsvExportService.ExportAsync(ExportBtn, "ArtifactX placed objects",
                new[] { "Object", "Location", "Portal code", "Distance" },
                rows.Select(r => new[] { r.Name, r.Detail, r.PortalCode, r.DistanceLabel }));
            return;
        }

        string search = SearchBox.Text.Trim();
        bool isSearching = search.Length > 0;
        var filtered = _rows.Where(r => !isSearching || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || (r.AreaOpacity > 0.9 && r.AreaLabel.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            || r.Detail.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || r.Facts.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || r.Category.Contains(search, StringComparison.CurrentCultureIgnoreCase));

        filtered = SortBox.SelectedIndex switch
        {
            1 => filtered.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => filtered.OrderBy(r => r.Galaxy).ThenBy(r => r.SortDistance),
            _ => filtered.OrderBy(r => r.SortDistance).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        var list = filtered.ToList();

        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX bases",
            new[] { "Category", "Base", "Location", "Portal code", "Distance", "Parts / Status" },
            list.Select(r => new[] { r.Category, r.Name, r.Detail, r.PortalCode, r.DistanceLabel, r.PartsLabel }));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Apply();

    /// <summary>A base clicked: its parts, one row per kind ("Wooden Floor · 600"), biggest first
    /// (user request 2026-10-18) - what to thin out when a base is near its limit.</summary>
    private async void ShowDetail(BaseRowViewModel row)
    {
        DetailTitleTxt.Text = row.Name;
        DetailSubTxt.Text = row.Detail;
        DetailPartsTxt.Text = row.PartsLabel;
        DetailPartsTxt.Foreground = row.PartsBrush;
        DetailPicture.Source = row.Picture;
        DetailPictureBorder.Visibility = row.PictureVisibility;
        DetailFactsTxt.Text = row.Facts;
        DetailAreaTxt.Text = row.AreaLabel;
        DetailAreaTxt.Opacity = row.AreaOpacity;
        DetailDistanceTxt.Text = row.DistanceLabel;
        DetailGlyphStrip.Code = row.PortalCode;
        DetailGlyphs.ItemsSource = row.Glyphs;
        DetailCopyBtn.Tag = row.PortalCode;
        ToolTipService.SetToolTip(DetailCopyBtn, row.PortalCode);
        DetailList.Children.Clear();

        if (row.Base is JObject b)
        {
            _backdropBase = row.Picture is null ? null : b;
            _backdropAspect = 0;
            DetailBackdrop.Visibility = Visibility.Collapsed;

            var objects = (b["@ZJ"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id != "^BASE_FLAG").ToList();
            var groups = objects.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            await CatalogService.WarmCacheAsync(groups.Select(g => g.Key));
            DetailKindsTxt.Text = $"{groups.Count:N0} kinds of part";

            string NameOf(string id) => CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } n ? n
                : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.TrimStart('^').Replace('_', ' ').ToLowerInvariant());
            // Kinds sharing a name are numbered as the game numbers them: CONTAINER0..9 are ten
            // separate "Storage Container"s, told apart only by the digit on their icons (2026-10-18).
            var shared = groups.GroupBy(g => NameOf(g.Key)).Where(n => n.Count() > 1).Select(n => n.Key).ToHashSet();
            foreach (var g in groups)
            {
                var entry = CatalogService.TryGet(g.Key);
                string name = NameOf(g.Key);
                if (shared.Contains(name) && System.Text.RegularExpressions.Regex.Match(g.Key, @"\d+$") is { Success: true } number)
                    name = $"{name} {number.Value}";
                var line = new Grid { ColumnSpacing = 12, Padding = new Thickness(10, 6, 14, 6), Width = 360 };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                if (entry?.Icon is { } icon) line.Children.Add(new Image { Source = icon, Width = 32, Height = 32 });
                var label = new TextBlock { Text = name, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (Brush)Application.Current.Resources["ThemeTextBrush"] };
                ToolTipService.SetToolTip(label, name);
                Grid.SetColumn(label, 1);
                line.Children.Add(label);
                var count = new TextBlock { Text = g.Count().ToString("N0", CultureInfo.CurrentCulture), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["ThemeAccentBrush"] };
                Grid.SetColumn(count, 2);
                line.Children.Add(count);
                DetailList.Children.Add(new Border
                {
                    Child = line, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
                    Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"], BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                });
            }
        }
        else
        {
            _backdropBase = null;
            DetailBackdrop.Visibility = Visibility.Collapsed;
            DetailKindsTxt.Text = "Settlement Administration";

            var infoCard = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                Background = (Brush)Application.Current.Resources["ThemePanelAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ThemeLineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = "SETTLEMENT OVERVIEW", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] },
                        new TextBlock { Text = "Settlement buildings and citizen decisions are managed from your Overseer's Office.", Style = (Style)Application.Current.Resources["ThemeBodyTextStyle"], FontSize = 14 },
                        new TextBlock { Text = "You can view full debt, productivity, happiness, and active features on the Settlements page.", Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12 }
                    }
                }
            };
            DetailList.Children.Add(infoCard);
        }

        ListArea.Visibility = Visibility.Collapsed;
        DetailArea.Visibility = Visibility.Visible;
        DetailHeaderGrid.UpdateLayout();
        await ShowBackdropAsync();
    }

    private JObject? _backdropBase;
    private double _backdropAspect;

    /// <summary>The base's picture behind the header, faded to transparent at the edges of the
    /// area it fills - so it melts into the page with no seam (2026-10-18). Redone when the
    /// header's shape changes much (window resized).</summary>
    private async System.Threading.Tasks.Task ShowBackdropAsync()
    {
        if (_backdropBase is not JObject b || S.ParsePackedAddress(b["oZw"]) is not ulong address) return;
        double w = DetailHeaderGrid.ActualWidth + 24, h = DetailHeaderGrid.ActualHeight + 14;
        if (w < 50 || h < 50) return;
        double aspect = w / h;
        if (_backdropAspect > 0 && Math.Abs(aspect / _backdropAspect - 1) < 0.08) return;
        _backdropAspect = aspect;
        if (await GameScreenshotCache.LoadAsync(SaveSessionManager.ActivePlatformFolder, address, b["NKm"]?.Value<string>() ?? "", faded: true, aspect: aspect) is { } faded
            && _backdropBase == b)
        {
            DetailBackdropBrush.ImageSource = faded;
            DetailBackdrop.Visibility = Visibility.Visible;
        }
    }

    private void DetailHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DetailArea.Visibility == Visibility.Visible) _ = ShowBackdropAsync();
    }

    private void DetailBack_Click(object sender, RoutedEventArgs e)
    {
        DetailArea.Visibility = Visibility.Collapsed;
        ListArea.Visibility = Visibility.Visible;
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rows is not null && FreighterItemsList is not null) Apply();
    }

    private static List<ImageSource> GlyphsFor(string code)
    {
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

    /// <summary>Green with room to spare, yellow when close, red when over (user request 2026-10-18).</summary>
    private static Windows.UI.Color LevelColour(int level) => level switch
    {
        2 => Windows.UI.Color.FromArgb(255, 0xFF, 0x5A, 0x5A),
        1 => Windows.UI.Color.FromArgb(255, 0xF5, 0xC5, 0x42),
        _ => Windows.UI.Color.FromArgb(255, 0x3D, 0xDC, 0x84),
    };

    private static Windows.UI.Color ParseColour(string hex)
    {
        uint rgb = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}
