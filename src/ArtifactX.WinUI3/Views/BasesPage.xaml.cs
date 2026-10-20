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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only list of the player's planet bases, freighter base, claimed space stations,
/// settlements, and standalone placed equipment across all galaxies.
/// </summary>
public sealed partial class BasesPage : Page
{
    private List<BaseRowViewModel> _rows = new();
    private readonly LiveSaveWatcher _live;
    private string _lastKey = "";

    public const string CategoryFreighter = "Freighter Base";
    public const string CategoryPlanetary = "Planetary Bases";
    public const string CategoryStations = "Claimed Space Stations & Outposts";
    public const string CategorySettlements = "Settlements";
    public const string CategoryPlaced = "Placed Equipment (No Base)";

    private bool _freighterExpanded = true;
    private bool _planetaryExpanded = true;
    private bool _stationsExpanded = true;
    private bool _settlementsExpanded = true;
    private bool _placedExpanded = true;

    // Base pictures from the game's cache, by planet/system address
    private readonly Dictionary<ulong, Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap?> _pictures = new();
    private readonly HashSet<ulong> _picturesLoading = new();

    private sealed record BaseRawData(
        string Name,
        string Detail,
        string Facts,
        string SwatchHex,
        string PortalCode,
        string DistanceLabel,
        string AreaLabel,
        double AreaOpacity,
        string PartsLabel,
        int PartsLevel,
        JObject? Base,
        double SortDistance,
        int Galaxy,
        string Category,
        ulong Address,
        string ScreenshotName,
        string? PlacedIconId
    );

    private sealed record BasesComputationResult(
        string Key,
        int TotalBasesCount,
        int PlacedCount,
        int PartsTotal,
        int PartsGlobalLevel,
        string PartsBreakdownText,
        List<BaseRawData> Rows
    );

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
        _ = BuildAsync(snapshot);
    }

    private void OnDataChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) _ = BuildAsync(_lastSnapshot);
    });

    private void OnSystemChanged(ulong system) => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) _ = BuildAsync(_lastSnapshot);
    });

    private void OnFindsChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) _ = BuildAsync(_lastSnapshot);
    });

    private void OnCreaturesChanged(ulong system, int planet) => DispatcherQueue.TryEnqueue(() =>
    {
        _lastKey = "";
        if (_lastSnapshot is not null) _ = BuildAsync(_lastSnapshot);
    });

    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    private async Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;

        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        ListArea.Visibility = Visibility.Collapsed;

        // Force a dispatcher yield so WinUI immediately completes navigation and paints BasesPage with active spinner
        await Task.Yield();

        await CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));
        _lastKey = "";
        await BuildAsync(LiveSaveWatcher.FromSession());
        _live.Start();
    }

    private PlanetSnapshot? _lastSnapshot;

    private void OnRangeChanged()
    {
        if (_lastSnapshot is null) return;
        _lastKey = "";
        _ = BuildAsync(_lastSnapshot);
    }

    private async Task BuildAsync(PlanetSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        ulong? here = S.ReadSystemAddress(snapshot.Universe);

        string key = $"{here}|{snapshot.Bases?.Count}|{snapshot.LooseObjects?.Count}|{snapshot.Settlements?.Count}|{NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects)}";
        if (key == _lastKey && _rows.Count > 0) return;

        if (_rows.Count == 0)
        {
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            ListArea.Visibility = Visibility.Collapsed;
        }

        // Warm catalog cache for placed equipment IDs so their real names and icons load immediately
        var placedIds = (snapshot.LooseObjects as JArray)?.OfType<JObject>()
            .Select(o => o["r<7"]?.Value<string>())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        if (placedIds != null && placedIds.Count > 0)
        {
            await CatalogService.WarmCacheAsync(placedIds);
        }

        // Heavy computations, counts, and SQLite reads run off the UI thread
        var result = await Task.Run(() => ComputeBases(snapshot, here, key));

        if (result.Key != key) return; // Stale calculation
        _lastKey = key;

        // Parts placed
        PartsTotalTxt.Text = $"Building parts placed: {NmsBuildCounts.GlobalLabel(result.PartsTotal)} before the game's construction limit";
        PartsTotalTxt.Foreground = new SolidColorBrush(LevelColour(result.PartsGlobalLevel));
        PartsBreakdownTxt.Text = result.PartsBreakdownText;

        // Convert raw DTOs to BaseRowViewModel on UI thread
        var rows = new List<BaseRowViewModel>(result.Rows.Count);
        foreach (var r in result.Rows)
        {
            var vm = new BaseRowViewModel
            {
                Name = r.Name,
                Detail = r.Detail,
                Facts = r.Facts,
                Swatch = new SolidColorBrush(ParseColour(r.SwatchHex)),
                PortalCode = r.PortalCode,
                Glyphs = GlyphsFor(r.PortalCode),
                DistanceLabel = r.DistanceLabel,
                AreaLabel = r.AreaLabel,
                AreaOpacity = r.AreaOpacity,
                PartsLabel = r.PartsLabel,
                PartsBrush = r.PartsLevel >= 0 ? new SolidColorBrush(LevelColour(r.PartsLevel)) : null,
                Base = r.Base,
                SortDistance = r.SortDistance,
                Galaxy = r.Galaxy,
                Category = r.Category,
                Icon = r.PlacedIconId != null ? CatalogService.TryGet(r.PlacedIconId)?.Icon : null,
            };

            if (r.Address != 0)
            {
                LoadPictureFor(vm, r.Address, r.ScreenshotName);
            }

            rows.Add(vm);
        }

        _rows = rows;
        Apply();

        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ListArea.Visibility = Visibility.Visible;
    }

    private static BasesComputationResult ComputeBases(PlanetSnapshot snapshot, ulong? here, string key)
    {
        var rawItems = new List<BaseRawData>();
        var counts = NmsBuildCounts.PerBase(snapshot.Bases).ToDictionary(c => c.Base, c => c.Parts);
        int total = NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects);
        string breakdown = NmsBuildCounts.Breakdown(snapshot.Bases, snapshot.LooseObjects);

        var planets = NmsPlanets.ReadAll(snapshot.Discoveries)
            .GroupBy(p => p.Address).ToDictionary(g => g.Key, g => g.First());

        // Index loose objects by planetary address to correlate with settlements
        var looseByAddress = new Dictionary<ulong, List<JObject>>();
        if (snapshot.LooseObjects is JArray looseArr)
        {
            foreach (var o in looseArr.OfType<JObject>())
            {
                if (S.ParsePackedAddress(o["oZw"]) is ulong address && address != 0)
                {
                    if (!looseByAddress.TryGetValue(address, out var list))
                    {
                        list = new List<JObject>();
                        looseByAddress[address] = list;
                    }
                    list.Add(o);
                }
            }
        }

        var settlementAddresses = new HashSet<ulong>();

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
                double? distance = (here is ulong h && address != 0) ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = address != 0 ? S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1) : "";
                int parts = counts.TryGetValue(b, out int p) ? p : 0;
                string loc = address != 0 ? UniverseInfo.LocationWithRegion(address, galaxy) : "In Transit";

                rawItems.Add(new BaseRawData(
                    Name: name,
                    Detail: $"Capital Ship · {loc}",
                    Facts: address != 0 ? UniverseInfo.Compact(address) : "Mobile Capital Ship",
                    SwatchHex: "#7E57C2",
                    PortalCode: code,
                    DistanceLabel: address != 0 ? DistanceText(here, address, distance) : "Mobile Capital Ship",
                    AreaLabel: "Mobile capital ship base with bridge command operations, storage rooms, refiners, and fleet teleporter.",
                    AreaOpacity: 1.0,
                    PartsLabel: NmsBuildCounts.UploadLabel(parts),
                    PartsLevel: NmsBuildCounts.UploadLevel(parts),
                    Base: b,
                    SortDistance: distance ?? double.MaxValue,
                    Galaxy: galaxy,
                    Category: CategoryFreighter,
                    Address: address,
                    ScreenshotName: name,
                    PlacedIconId: null
                ));
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
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, planetIndex);
                int parts = counts.TryGetValue(b, out int p) ? p : 0;
                string loc = UniverseInfo.LocationWithRegion(address, galaxy);
                string coords = NmsPlanetCoordinates.OfBase(b) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";

                string area = AreaText(address, planetIndex, galaxy, code);
                rawItems.Add(new BaseRawData(
                    Name: name,
                    Detail: $"{UniverseInfo.PlanetName(address, planetIndex)} ({type}){coords} · {loc}",
                    Facts: UniverseInfo.Compact(address),
                    SwatchHex: planet is null ? "#777777" : NmsPlanets.TypeColour(planet.BiomeIndex),
                    PortalCode: code,
                    DistanceLabel: DistanceText(here, address, distance),
                    AreaLabel: area.Length > 0 ? area : "Nothing recorded about this planet yet - add its resources, sentinels and notes on the Planets page.",
                    AreaOpacity: area.Length > 0 ? 1.0 : 0.55,
                    PartsLabel: NmsBuildCounts.UploadLabel(parts),
                    PartsLevel: NmsBuildCounts.UploadLevel(parts),
                    Base: b,
                    SortDistance: distance ?? double.MaxValue,
                    Galaxy: galaxy,
                    Category: CategoryPlanetary,
                    Address: address,
                    ScreenshotName: name,
                    PlacedIconId: null
                ));
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
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, 1);
                int parts = counts.TryGetValue(b, out int p) ? p : 0;
                string loc = UniverseInfo.LocationWithRegion(address, galaxy);

                rawItems.Add(new BaseRawData(
                    Name: name,
                    Detail: $"Space Station Directorate · {loc}",
                    Facts: UniverseInfo.Compact(address),
                    SwatchHex: "#FF9800",
                    PortalCode: code,
                    DistanceLabel: DistanceText(here, address, distance),
                    AreaLabel: $"Claimed station in {UniverseInfo.SystemName(address)} ({UniverseInfo.RaceLabel(address)}). Station core terminal, concession shops, and landing bays.",
                    AreaOpacity: 1.0,
                    PartsLabel: $"{parts:N0} parts placed · Station Directorate",
                    PartsLevel: NmsBuildCounts.UploadLevel(parts),
                    Base: b,
                    SortDistance: distance ?? double.MaxValue,
                    Galaxy: galaxy,
                    Category: CategoryStations,
                    Address: address,
                    ScreenshotName: name,
                    PlacedIconId: null
                ));
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
                settlementAddresses.Add(address);

                int planetIndex = (int)((address >> 52) & 0xF);
                int galaxy = S.GalaxyNumber(address);
                planets.TryGetValue(address, out var planet);
                string planetType = planet is null ? "Colony Planet" : planet.Classification(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Descriptor, ringSuffix: " · rings");
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1);
                int population = s["x3<"]?.Value<int>() ?? 0;
                string loc = UniverseInfo.LocationWithRegion(address, galaxy);

                // Collect parts placed on this settlement's planet (loose equipment + nearby home base parts)
                var settlementParts = new JArray();
                if (looseByAddress.TryGetValue(address, out var onPlanetLoose))
                {
                    foreach (var lo in onPlanetLoose)
                    {
                        settlementParts.Add(new JObject { ["r<7"] = lo["r<7"] });
                    }
                }

                if (snapshot.Bases is JArray allBasesList)
                {
                    foreach (var b in allBasesList.OfType<JObject>())
                    {
                        if (S.ParsePackedAddress(b["oZw"]) == address && b["@ZJ"] is JArray baseParts)
                        {
                            foreach (var p in baseParts.OfType<JObject>())
                            {
                                if (p["r<7"]?.Value<string>() != "^BASE_FLAG")
                                {
                                    settlementParts.Add(p);
                                }
                            }
                        }
                    }
                }

                int partsPlaced = settlementParts.Count;
                var settlementBase = new JObject(s)
                {
                    ["@ZJ"] = settlementParts,
                    ["peI"] = new JObject { ["DPp"] = "Settlement" },
                    ["oZw"] = s["yhJ"]
                };

                string partsLabel = partsPlaced > 0
                    ? $"{partsPlaced:N0} parts placed · {population:N0} citizens"
                    : $"Population {population:N0} citizens · Overseer";

                string area = AreaText(address, planetIndex, galaxy, code);
                if (string.IsNullOrEmpty(area))
                    area = $"Player-managed colony on {UniverseInfo.PlanetName(address, planetIndex)} in {UniverseInfo.SystemName(address)} ({UniverseInfo.RaceLabel(address)}).";

                rawItems.Add(new BaseRawData(
                    Name: name,
                    Detail: $"Settlement · {UniverseInfo.PlanetName(address, planetIndex)} ({planetType}) · {loc}",
                    Facts: UniverseInfo.Compact(address),
                    SwatchHex: planet is null ? "#00BCD4" : NmsPlanets.TypeColour(planet.BiomeIndex),
                    PortalCode: code,
                    DistanceLabel: DistanceText(here, address, distance),
                    AreaLabel: area,
                    AreaOpacity: 1.0,
                    PartsLabel: partsLabel,
                    PartsLevel: 0,
                    Base: settlementBase,
                    SortDistance: distance ?? double.MaxValue,
                    Galaxy: galaxy,
                    Category: CategorySettlements,
                    Address: address,
                    ScreenshotName: name,
                    PlacedIconId: null
                ));
            }
        }

        // 5. Placed Equipment (No Base) - refiners, save points, beacons left on planets without bases or settlements
        if (snapshot.LooseObjects is JArray placedObjects)
        {
            foreach (var o in placedObjects.OfType<JObject>())
            {
                if (S.ParsePackedAddress(o["oZw"]) is not ulong address) continue;
                if (settlementAddresses.Contains(address)) continue; // Attributed to settlement

                string id = o["r<7"]?.Value<string>() ?? "";
                int planetIndex = (int)((address >> 52) & 0xF);
                int galaxy = S.GalaxyNumber(address);
                planets.TryGetValue(address, out var planet);
                string type = planet is null ? "Planet" : planet.Classification(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Descriptor);
                double? distance = here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;
                string code = S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1);
                long placedAt = o["b1:"]?.Value<long>() ?? 0;
                string timeStr = placedAt > 0 ? $"Placed {DateTimeOffset.FromUnixTimeSeconds(placedAt).LocalDateTime:d}" : "Standalone Equipment";
                string loc = UniverseInfo.LocationWithRegion(address, galaxy);
                string coords = NmsPlanetCoordinates.OfBase(o) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";

                rawItems.Add(new BaseRawData(
                    Name: ResolvePlacedName(id),
                    Detail: $"Placed on {UniverseInfo.PlanetName(address, planetIndex)} ({type}){coords} · {loc}",
                    Facts: $"{timeStr} · Standalone Equipment",
                    SwatchHex: planet is null ? "#00BCD4" : NmsPlanets.TypeColour(planet.BiomeIndex),
                    PortalCode: code,
                    DistanceLabel: DistanceText(here, address, distance),
                    AreaLabel: AreaText(address, planetIndex, galaxy, code) is { Length: > 0 } a ? a : $"Standalone equipment placed on {UniverseInfo.PlanetName(address, planetIndex)}.",
                    AreaOpacity: 1.0,
                    PartsLabel: "1 part · Outside base",
                    PartsLevel: 0,
                    Base: null,
                    SortDistance: distance ?? double.MaxValue,
                    Galaxy: galaxy,
                    Category: CategoryPlaced,
                    Address: address,
                    ScreenshotName: "",
                    PlacedIconId: id
                ));
            }
        }

        int totalBases = rawItems.Count(r => r.Category != CategoryPlaced);
        int placedCount = rawItems.Count(r => r.Category == CategoryPlaced);

        return new BasesComputationResult(key, totalBases, placedCount, total, NmsBuildCounts.GlobalLevel(total), breakdown, rawItems);
    }

    private static readonly Dictionary<string, string> PlacedItemFriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BUILDBEACON"] = "Save Beacon",
        ["BUILDSAVE"] = "Save Point",
        ["BUILDSIGNAL"] = "Signal Booster",
        ["BUILD_REFINER1"] = "Portable Refiner",
        ["PORTABLE_REFINER"] = "Portable Refiner",
        ["REFINER"] = "Portable Refiner",
        ["COOKER"] = "Nutrient Processor",
        ["SUMMON_GARAGE"] = "Exocraft Summoning Station",
        ["BUILDHARVESTER"] = "Autonomous Mining Unit",
        ["BUILDTERMINAL"] = "Galactic Trade Terminal",
        ["BASE_FLAG"] = "Base Computer",
        ["GARAGE_M"] = "Roamer Geobay",
        ["GARAGE_B"] = "Colossus Geobay",
        ["GARAGE_S"] = "Nomad Geobay",
        ["GARAGE_SUB"] = "Nautilon Chamber",
        ["GARAGE_MECH"] = "Minotaur Geobay",
        ["WATERBUBBLE"] = "Marine Shelter",
        ["BASE_BEAMSTONE"] = "Light Fissure",
        ["BASE_HYDROPOD"] = "Calcishroom",
        ["BASE_SHELLWHITE"] = "Capillary Shell",
        ["BASE_MEDGEOMETR"] = "Hexplate Bush",
        ["BASE_SHARD"] = "Glitching Separator",
        ["BASE_ENGINEORB"] = "Terbium Growth",
        ["BASE_STARJOINT"] = "Ossified Star",
        ["ANTENNA"] = "Communications Station",
        ["MESSAGEMODULE"] = "Message Module",
        ["TELEPORTER"] = "Base Teleport Module",
    };

    private static string ResolvePlacedName(string id)
    {
        string norm = CatalogService.NormalizeId(id);
        if (PlacedItemFriendlyNames.TryGetValue(norm, out var friendly))
            return friendly;

        if (CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } n)
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(n.ToLowerInvariant());

        string cleaned = norm.TrimStart('^').Replace('_', ' ');
        cleaned = Regex.Replace(cleaned, @"([a-z])([A-Z])", "$1 $2");
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLowerInvariant());
    }

    private void LoadPictureFor(BaseRowViewModel row, ulong address, string name)
    {
        if (_pictures.TryGetValue(address, out var pic))
        {
            row.Picture = pic;
            return;
        }

        if (_picturesLoading.Add(address))
        {
            _ = Task.Run(async () =>
            {
                var bmp = await GameScreenshotCache.LoadAsync(SaveSessionManager.ActivePlatformFolder, address, name);
                _pictures[address] = bmp;
                if (bmp != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        row.Picture = bmp;
                        foreach (var other in _rows)
                        {
                            if (other != row && other.Base is JObject b && S.ParsePackedAddress(b["oZw"]) == address)
                            {
                                other.Picture = bmp;
                            }
                        }
                    });
                }
            });
        }
    }

    private void Apply()
    {
        if (SearchBox is null || FreighterItemsList is null) return;
        string search = SearchBox.Text.Trim();
        bool isSearching = search.Length > 0;

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

        // Split filtered list by category
        var freighters = list.Where(r => r.Category == CategoryFreighter).ToList();
        var planetaries = list.Where(r => r.Category == CategoryPlanetary).ToList();
        var stations = list.Where(r => r.Category == CategoryStations).ToList();
        var settlements = list.Where(r => r.Category == CategorySettlements).ToList();
        var placed = list.Where(r => r.Category == CategoryPlaced).ToList();

        int totalFreighters = _rows.Count(r => r.Category == CategoryFreighter);
        int totalPlanetaries = _rows.Count(r => r.Category == CategoryPlanetary);
        int totalStations = _rows.Count(r => r.Category == CategoryStations);
        int totalSettlements = _rows.Count(r => r.Category == CategorySettlements);
        int totalPlaced = _rows.Count(r => r.Category == CategoryPlaced);

        FreighterItemsList.ItemsSource = freighters;
        PlanetaryItemsList.ItemsSource = planetaries;
        StationsItemsList.ItemsSource = stations;
        SettlementsItemsList.ItemsSource = settlements;
        PlacedItemsList.ItemsSource = placed;

        FreighterCountTxt.Text = isSearching ? $"{freighters.Count} of {totalFreighters}" : $"{freighters.Count}";
        PlanetaryCountTxt.Text = isSearching ? $"{planetaries.Count} of {totalPlanetaries}" : $"{planetaries.Count}";
        StationsCountTxt.Text = isSearching ? $"{stations.Count} of {totalStations}" : $"{stations.Count}";
        SettlementsCountTxt.Text = isSearching ? $"{settlements.Count} of {totalSettlements}" : $"{settlements.Count}";
        PlacedCountTxt.Text = isSearching ? $"{placed.Count} of {totalPlaced}" : $"{placed.Count}";

        // Summaries
        int freighterParts = freighters.Sum(f => f.Base is JObject b ? (b["@ZJ"] as JArray)?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0 : 0);
        FreighterSummaryTxt.Text = freighters.Count > 0 ? $"Mobile capital ship base · {freighterParts:N0} parts placed" : "No capital ship base recorded";

        int planetParts = planetaries.Sum(p => p.Base is JObject b ? (b["@ZJ"] as JArray)?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0 : 0);
        PlanetarySummaryTxt.Text = planetaries.Count > 0 ? $"{planetParts:N0} parts placed across {planetaries.Count:N0} planet bases" : "No planetary bases recorded";

        int stationParts = stations.Sum(s => s.Base is JObject b ? (b["@ZJ"] as JArray)?.Count(o => o["r<7"]?.Value<string>() != "^BASE_FLAG") ?? 0 : 0);
        StationsSummaryTxt.Text = stations.Count > 0 ? $"{stations.Count:N0} claimed facilities · {stationParts:N0} parts total" : "No claimed stations in this save";

        SettlementsSummaryTxt.Text = settlements.Count > 0 ? $"{settlements.Count:N0} governed colonies" : "No settlements governed";

        PlacedSummaryTxt.Text = placed.Count > 0
            ? $"{placed.Count:N0} pieces of standalone equipment left on planets across the galaxy"
            : "No equipment left outside bases";

        // Visibility of empty messages
        FreighterEmptyTxt.Visibility = freighters.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        PlanetaryEmptyTxt.Visibility = planetaries.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        StationsEmptyTxt.Visibility = stations.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        SettlementsEmptyTxt.Visibility = settlements.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;
        PlacedEmptyTxt.Visibility = placed.Count == 0 && !isSearching ? Visibility.Visible : Visibility.Collapsed;

        // During search, auto-expand categories that have matches, and hide categories with 0 matches
        if (isSearching)
        {
            FreighterSectionCard.Visibility = freighters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PlanetarySectionCard.Visibility = planetaries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            StationsSectionCard.Visibility = stations.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SettlementsSectionCard.Visibility = settlements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PlacedSectionCard.Visibility = placed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (freighters.Count > 0) _freighterExpanded = true;
            if (planetaries.Count > 0) _planetaryExpanded = true;
            if (stations.Count > 0) _stationsExpanded = true;
            if (settlements.Count > 0) _settlementsExpanded = true;
            if (placed.Count > 0) _placedExpanded = true;
        }
        else
        {
            FreighterSectionCard.Visibility = Visibility.Visible;
            PlanetarySectionCard.Visibility = Visibility.Visible;
            StationsSectionCard.Visibility = Visibility.Visible;
            SettlementsSectionCard.Visibility = Visibility.Visible;
            PlacedSectionCard.Visibility = Visibility.Visible;
        }

        UpdateCategoryExpansions();

        CountTxt.Text = _rows.Count == 0 ? "No bases or equipment in this save"
            : $"{list.Count} of {_rows.Count} bases, settlements & equipment";
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
            case "Placed":
                _placedExpanded = !_placedExpanded;
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

        PlacedContentArea.Visibility = _placedExpanded ? Visibility.Visible : Visibility.Collapsed;
        PlacedChevron.Glyph = _placedExpanded ? "\uE70E" : "\uE70D";
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
            Frame.Navigate(typeof(ExplorePages.BaseDetailsPage), row);
        }
    }

    private void OpenCard_Click(object sender, RoutedEventArgs e)
    {
        var row = (sender as FrameworkElement)?.DataContext as BaseRowViewModel
            ?? (sender as FrameworkElement)?.Tag as BaseRowViewModel;
        if (row != null) Frame.Navigate(typeof(ExplorePages.BaseDetailsPage), row);
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
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

        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX bases and equipment",
            new[] { "Category", "Item / Base", "Location", "Portal code", "Distance", "Parts / Status" },
            list.Select(r => new[] { r.Category, r.Name, r.Detail, r.PortalCode, r.DistanceLabel, r.PartsLabel }));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Apply();

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rows is not null && FreighterItemsList is not null) Apply();
    }

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

    private static List<ImageSource> GlyphsFor(string code)
    {
        var images = code.Select(c => CatalogService.TryGet($"PORTALSYMBOL_{c}")?.Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }

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
