using System.Collections.ObjectModel;
using ArtifactX.Core.Collections;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// An active located building, crashed ship, or multi-tool site on this planet from the save's
/// active scan events (vLc.6f=.A1f), settlements, markers, or player Finds.
/// </summary>
public sealed record PlanetLocatedSite(
    string CategoryBadge,
    string Title,
    string Coordinates,
    string Details,
    bool IsHighlight)
{
    public Brush BadgeBackground => ThemeResources.Brush(IsHighlight ? "ThemeDoneBrush" : "ThemeAccentBrush");
    public Brush BadgeForeground => ThemeResources.Brush("ThemeOnAccentBrush");
}

/// <summary>
/// Displays saved planetary locations. Station equipment belongs to Current System.
/// </summary>
/// <summary>One choice of the crashed-ship type filter: "Fighter (148)", or any type when <see cref="Type"/> is null.</summary>
public sealed record CrashedShipTypeOption(string Label, SystemStarshipType? Type)
{
    public override string ToString() => Label;
}

public sealed partial class PlanetEquipmentViewModel : ObservableObject
{
    [ObservableProperty] public partial string LocatedSitesHeading { get; private set; } = "LOCATED CRASHED SHIPS, CABINETS & BUILDINGS ON THIS PLANET";
    [ObservableProperty] public partial string LocatedSitesHint { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<PlanetLocatedSite> LocatedSites { get; private set; } = [];
    [ObservableProperty] public partial bool HasLocatedSites { get; private set; }
    [ObservableProperty] public partial bool NoLocatedSites { get; private set; } = true;

    partial void OnLocatedSitesChanged(IReadOnlyList<PlanetLocatedSite> value)
    {
        HasLocatedSites = value.Count > 0;
        NoLocatedSites = value.Count == 0;
    }

    /// <summary>The planet's exact centre (markers are in system space), or null when it can't be generated.</summary>
    private (double X, double Y, double Z)? _centre;

    // The portal, from the game's own placement rules (PortalLocationService), and its save marker.
    [ObservableProperty] public partial bool HasPortal { get; private set; }
    [ObservableProperty] public partial string PortalCoordinates { get; private set; } = "";
    [ObservableProperty] public partial string PortalStatus { get; private set; } = "";
    [ObservableProperty] public partial bool CanAddPortalMarker { get; private set; }
    [ObservableProperty] public partial string AddPortalMarkerText { get; private set; } = "Add a marker to my save";
    [ObservableProperty] public partial bool CanUndoPortalMarker { get; private set; }
    private PortalLocation? _portal;
    private ulong _planetAddress;

    // Crashed ships nearest you (on this planet) or the portal, filtered by class and type (CrashedShipService).
    private const string ShipClassKey = "PlanetDetailsCrashedShipClass", ShipTypeKey = "PlanetDetailsCrashedShipType",
        ShipCountKey = "PlanetDetailsCrashedShipCount";

    /// <summary>How many matches to list (index into <see cref="ShipCounts"/>); 10 by default, remembered. "All" is
    /// every wreck of the planet (about 2,000 on a Large planet).</summary>
    public IReadOnlyList<string> ShipCountChoices { get; } = ["5", "10", "25", "50", "100", "250", "500", "All"];
    private static readonly int[] ShipCounts = [5, 10, 25, 50, 100, 250, 500, int.MaxValue];
    [ObservableProperty]
    public partial int ShipCountChoice { get; set; } = Remembered(ShipCountKey, ShipCounts.Length - 1, 1);
    private int ShownShips => ShipCounts[System.Math.Clamp(ShipCountChoice, 0, ShipCounts.Length - 1)];

    /// <summary>The class filter's choices, in the order of the buttons (index 0 = any class).</summary>
    public IReadOnlyList<string> ShipClassChoices { get; } = ["Any class", "S-class", "A-class", "B-class", "C-class"];

    /// <summary>The class filter: 0 = any, 1 = S (the default) .. 4 = C; remembered across sessions.</summary>
    [ObservableProperty]
    public partial int ShipClassChoice { get; set; } = Remembered(ShipClassKey, 4, 1);

    /// <summary>The type filter's choices: only the types this planet has wrecks of in the chosen class, with counts
    /// (the game draws a wreck's type and class from the same number, so each class comes with only some types).</summary>
    [ObservableProperty] public partial IReadOnlyList<CrashedShipTypeOption> ShipTypeOptions { get; private set; } = [];
    [ObservableProperty] public partial CrashedShipTypeOption? SelectedShipType { get; set; }

    /// <summary>The type the player last picked ("" = any), kept even while the chosen class doesn't have it.</summary>
    private string _wantedType = SettingsStore.Get(SettingsStore.Scope.Profile, ShipTypeKey) ?? "";
    private bool _rebuildingTypes;

    public ObservableCollection<CrashedShipRowViewModel> CrashedShips { get; } = [];
    [ObservableProperty] public partial bool HasCrashedShips { get; private set; }
    [ObservableProperty] public partial string CrashedShipsHeading { get; private set; } = "CRASHED SHIPS";
    [ObservableProperty] public partial string CrashedShipsHint { get; private set; } = "";
    [ObservableProperty] public partial string CrashedShipsSummary { get; private set; } = "";
    private KnownPlanet? _planet;
    private PlanetSnapshot? _snapshot;

    private static int Remembered(string key, int max, int fallback) =>
        int.TryParse(SettingsStore.Get(SettingsStore.Scope.Profile, key), out int i) && i >= 0 && i <= max ? i : fallback;

    partial void OnShipClassChoiceChanged(int value)
    {
        if (value < 0) return;
        SettingsStore.Set(SettingsStore.Scope.Profile, ShipClassKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (_planet is { } planet && _snapshot is { } snapshot) _ = ReloadAsync(planet, snapshot);
    }

    partial void OnSelectedShipTypeChanged(CrashedShipTypeOption? value)
    {
        if (_rebuildingTypes || value is null) return;   // the list was just replaced
        _wantedType = value.Type?.ToString() ?? "";
        SettingsStore.Set(SettingsStore.Scope.Profile, ShipTypeKey, _wantedType);
        if (_planet is { } planet && _snapshot is { } snapshot) _ = LoadCrashedShipsAsync(planet, snapshot);
    }

    partial void OnShipCountChoiceChanged(int value)
    {
        if (value < 0) return;
        SettingsStore.Set(SettingsStore.Scope.Profile, ShipCountKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (_planet is { } planet && _snapshot is { } snapshot) _ = LoadCrashedShipsAsync(planet, snapshot);
    }

    private bool ClassMatches(CrashedShipSite site) => ShipClassChoice <= 0 || site.ShownClass == 4 - ShipClassChoice;

    private bool Matches(CrashedShipSite site) => ClassMatches(site) && (SelectedShipType?.Type is not { } type || site.Ship?.Type == type);

    /// <summary>Rebuilds the type list for the chosen class (keeping the player's type when the class has it), then the ships.</summary>
    private async Task ReloadAsync(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        var counts = await CrashedShipService.CountAsync(planet.Address, ClassMatches, s => s.Ship?.Type.ToString() ?? "");
        if (_planetAddress != planet.Address) return;
        var options = new List<CrashedShipTypeOption> { new($"Any type ({counts.Sum(c => c.Count):N0})", null) };
        foreach (var (group, count) in counts)
            if (System.Enum.TryParse<SystemStarshipType>(group, out var type))
                options.Add(new($"{(type == SystemStarshipType.SentinelInterceptor ? "Sentinel" : group)} ({count:N0})", type));
        _rebuildingTypes = true;
        ShipTypeOptions = options;
        SelectedShipType = options.FirstOrDefault(o => o.Type?.ToString() == _wantedType) ?? options[0];
        _rebuildingTypes = false;
        await LoadCrashedShipsAsync(planet, snapshot);
    }

    public async Task LoadAsync(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        _portal = null;
        HasPortal = false;
        if (_planetAddress != planet.Address)
        {
            CrashedShips.Clear();
            HasCrashedShips = false;
        }
        _planetAddress = planet.Address;
        _centre = await PlanetCentreService.GetAsync(planet.Address);
        UpdateLocations(planet, snapshot);
        var portal = await PortalLocationService.GetAsync(planet.Address);
        if (_planetAddress != planet.Address) return;   // moved on to another planet meanwhile
        _portal = portal;
        RefreshMarkers();
        _planet = planet;
        _snapshot = snapshot;
        await ReloadAsync(planet, snapshot);
    }

    /// <summary>The crashed ships (as many as chosen) matching the class and type filters nearest you when you're on this planet, else
    /// nearest its portal, searched over the whole planet.</summary>
    private async Task LoadCrashedShipsAsync(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        _planet = planet;
        _snapshot = snapshot;
        bool here = S.ReadSystemAddress(snapshot.Universe) == planet.SystemAddress
            && snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() == planet.PlanetIndex && snapshot.Coordinates is not null;
        (double Lat, double Lon)? from = here ? snapshot.Coordinates : _portal is { } p ? (p.Latitude, p.Longitude) : null;
        if (from is not { } at) return;
        var found = await CrashedShipService.SearchAsync(planet.Address, Matches, at.Lat, at.Lon, ShownShips);
        if (_planetAddress != planet.Address || found is null) return;
        var sites = found.Nearest;
        var cards = sites.Select((s, i) => new CrashedShipRowViewModel(s, i + 1, here, here ? "away" : "from the portal")).ToArray();
        ObservableCollectionSync.Apply(CrashedShips, cards, row => row.Key, (_, _) => true,
            (row, next) => row.UpdateFrom(next));
        HasCrashedShips = true;
        CrashedShipsSummary = found.Total == 0 ? $"No {(ShipClassChoice > 0 ? ShipClassChoices[ShipClassChoice] + " " : "")}crashed ships on this planet."
            : found.Total <= ShownShips ? $"All {found.Total:N0} matching crashed ships on this planet, nearest first."
            : $"The nearest {ShownShips} of {found.Total:N0} matching crashed ships on this planet.";
        if (found.Unverified) CrashedShipsSummary += " Not yet confirmed in game for systems without a race.";
        CrashedShipsHeading = here ? "CRASHED SHIPS NEAR YOU" : "CRASHED SHIPS NEAR THE PORTAL";
        CrashedShipsHint = sites.Any(s => s.Ship is null || s.Inventory is null)
            ? "Where crashed ships lie, worked out from the game's own rules. Update game data to see each wreck's ship, slots, value and stats. Now and then the ground leaves a spot empty."
            : "Where crashed ships lie, which ship is there, and its class and value. Supported ships show comparison stats for your saved active ship; other cards show intrinsic bonuses.";
    }

    /// <summary>Shows whether the portal and crashed ships are marked in the save, and whether markers can be added now.</summary>
    public void RefreshMarkers()
    {
        foreach (var ship in CrashedShips) ship.Refresh();
        HasPortal = _portal is not null;
        if (_portal is not { } portal) return;
        PortalCoordinates = portal.Coordinates;
        bool marked = ScanMarkerService.IsMarked(portal.Key), unsaved = ScanMarkerService.IsUnsaved(portal.Key);
        CanUndoPortalMarker = unsaved;
        CanAddPortalMarker = !unsaved && ScanMarkerService.CanAdd;
        AddPortalMarkerText = marked && !unsaved ? "Mark it again" : "Add a marker to my save";
        PortalStatus = unsaved ? "Marker added. Save your changes to write it to your save."
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to add a marker."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "This portal is marked in your save. If the game no longer shows the marker, close No Man's Sky to mark it again."
            : marked ? "This portal is marked in your save. If the game no longer shows the marker, mark it again."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a marker to your save."
            : "Adds a marker to your save that leads you to this portal in game.";
    }

    /// <summary>The portal marker, in the format the game saves portal markers in (table 3).</summary>
    [RelayCommand]
    private void AddPortalMarker()
    {
        if (_portal is not { } portal) return;
        ScanMarkerService.Add(portal.PlanetAddress, portal.Key, portal.X, portal.Y, portal.Z, 3, "PORTAL", "Portal");
        RefreshMarkers();
    }

    [RelayCommand]
    private void UndoPortalMarker()
    {
        if (_portal is { } portal) ScanMarkerService.Remove(portal.Key);
        RefreshMarkers();
    }

    /// <summary>
    /// Reads active scan events (A1f), settlements (GQA), bases, and player Finds for this planet
    /// to show exact [Lat, Lon] coordinates of located crashed ships, multi-tool cabinets, and buildings.
    /// </summary>
    public void UpdateLocations(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        var sites = ReadLocatedSites(planet, snapshot, _centre);

        LocatedSites = sites;
        LocatedSitesHeading = $"LOCATED SITES ON THIS PLANET ({sites.Count})";
        LocatedSitesHint = sites.Count > 0
            ? "Exact planetary coordinates (+Lat, +Lon) read directly from active markers/charts in your save and your logged Finds."
            : "No active Crashed Ship, Minor Settlement, or Sentinel Pillar markers are currently open on this planet in your save. Use a Planetary Chart, Sentinel Boundary Map, Echo Locator, or Signal Booster in game and save to see its exact +Lat, +Lon coordinates here automatically.";
    }

    /// <summary>Reads active markers and player-recorded Finds with exact locations for one planet.</summary>
    public static IReadOnlyList<PlanetLocatedSite> ReadLocatedSites(KnownPlanet planet, PlanetSnapshot snapshot,
        (double X, double Y, double Z)? planetCentre)
    {
        var sites = new List<PlanetLocatedSite>();

        // 1. Active planetary scan events from save (vLc.6f=.A1f)
        foreach (var ev in (snapshot.ScanEvents ?? new JArray()).OfType<JObject>())
        {
            if (S.ParsePackedAddress(ev["oZw"]) != planet.Address) continue;

            if (ev["TWn"] is not JArray { Count: >= 3 } at || (at[0].Value<double>() == 0 && at[1].Value<double>() == 0 && at[2].Value<double>() == 0))
                continue;
            // No exact centre (gas giant or prime body): list the site without coordinates rather than guess them.
            var coords = planetCentre is { } centre ? NmsPlanetCoordinates.OfScanEvent(ev, centre) : null;
            string coordText = coords is { } c0 ? NmsPlanetCoordinates.Format(c0.Latitude, c0.Longitude) : "Marked in game";
            string bldClass = ev["iqv"]?["iqv"]?.Value<string>() ?? "Unknown";
            string eventId = (ev["FN5"]?.Value<string>() ?? "").TrimStart('^');

            var (badge, title, details, highlight) = DescribeScanEvent(bldClass, eventId);
            sites.Add(new PlanetLocatedSite(badge, title, coordText, details, highlight));
        }

        // 2. Planetary Settlements on this planet (vLc.6f=.GQA)
        foreach (var st in (snapshot.Settlements ?? new JArray()).OfType<JObject>())
        {
            if (S.ParsePackedAddress(st["oZw"]) != planet.Address) continue;

            string name = st["NKm"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            var coords = NmsPlanetCoordinates.OfBase(st);
            string coordText = coords is { } c ? NmsPlanetCoordinates.Format(c.Latitude, c.Longitude) : "Surface";
            sites.Add(new PlanetLocatedSite(
                "SETTLEMENT",
                name,
                coordText,
                "Planetary Settlement recorded in your save.",
                false));
        }

        // 3. Player Finds recorded in ArtifactX for this planet
        foreach (var find in FindsService.All.Where(f => f.PortalCode == planet.PortalCode && f.Galaxy == planet.Galaxy))
        {
            string badge = string.IsNullOrEmpty(find.Type) ? "FIND" : find.Type.ToUpperInvariant();
            string title = !string.IsNullOrEmpty(find.Title) ? find.Title : find.Type;
            if (!string.IsNullOrEmpty(find.Class))
                title += $" ({find.Class}-Class)";
            string coords = !string.IsNullOrEmpty(find.Location) ? find.Location : "Recorded find";
            string details = !string.IsNullOrEmpty(find.Notes) ? find.Notes : "Saved in your Finds log for this planet.";
            sites.Add(new PlanetLocatedSite(badge, title, coords, details, true));
        }

        return sites;
    }

    private static (string Badge, string Title, string Details, bool IsHighlight) DescribeScanEvent(string buildingClass, string eventId)
    {
        return buildingClass switch
        {
            "DistressSignal" or "WaterDistressSignal" or "AbandonedShip" => (
                "CRASHED SHIP",
                "Distress Signal (Crashed Starship)",
                "Active crash site marker in your save. Fly to these coordinates to claim or salvage the crashed starship.",
                true),
            "SentinelDistressSignal" => (
                "SENTINEL SHIP",
                "Dissonance Spike (Crashed Sentinel Interceptor)",
                "Active crashed Sentinel Interceptor marker in your save. Repair the Hyaline Brain to claim the S/A/B/C Interceptor.",
                true),
            "Shop" => (
                "MULTI-TOOL CABINET",
                "Minor Settlement (Weapon Cabinet)",
                "Minor Settlement marker recorded in your save. The cabinet item and its stats have not been identified.",
                true),
            "DroneHive" or "DroneHiveDisabled" => (
                "EXOTIC MULTI-TOOL",
                "Sentinel Pillar (Royal Multi-Tool Terminal)",
                "Sentinel Pillar marker recorded in your save. The terminal item and its stats have not been identified.",
                true),
            "AbandonedRobotCamp" => (
                "SENTINEL CABINET",
                "Harmonic Camp (Sentinel Multi-Tool & Ship Locator)",
                "Solve the math glyphs at the terminal to unlock the Sentinel Multi-Tool cabinet and locate Crashed Sentinel Interceptors.",
                true),
            "Outpost" => (
                "TRADING POST",
                "Trading Post (Starship Waves)",
                "Trading Post marker recorded in your save.",
                true),
            "Monolith" => (
                "MONOLITH",
                "Alien Monolith (Portal Locator / Discordant Interface)",
                "Answers lore riddles to locate the planet's Portal; on Dissonant Korvax worlds, its side terminal contains an Atlantid Multi-Tool.",
                false),
            "Portal" => (
                "PORTAL",
                "Planetary Portal",
                "Ancient glyph portal for interstellar travel.",
                false),
            "ScrapYard" => (
                "CRASHED FREIGHTER",
                "Crashed Freighter",
                "Contains 6 buried cargo pods with Salvaged Frigate Modules, Storage Augmentations, and loot.",
                false),
            "Observatory" or "RadioTower" => (
                "SCANNER BUILDING",
                buildingClass == "RadioTower" ? "Transmission Tower (Reveals Crashed Ship)" : "Observatory (Reveals Ruin)",
                buildingClass == "RadioTower"
                    ? "Solve the terminal puzzle inside to mark a Crashed Starship (Distress Signal) or Crashed Freighter on this planet."
                    : "Solve the terminal puzzle inside to mark an Ancient Ruin.",
                buildingClass == "RadioTower"),
            "Factory" or "Harvester" => (
                "FACILITY",
                buildingClass == "Factory" ? "Manufacturing Facility" : "Operations Centre",
                "Secured facility — solve the terminal override to learn crafting recipes or earn Multi-Tool Expansion Slots.",
                false),
            "Plaque" or "Ruin" or "TreasureRuins" => (
                "ANCIENT RUIN",
                buildingClass == "Plaque" ? "Ancient Plaque" : "Ancient Ruin",
                "Historical monument and artifact site.",
                false),
            "Settlement_Hub" => (
                "SETTLEMENT",
                "Planetary Settlement",
                "Planetary settlement hub.",
                false),
            _ => (
                buildingClass.ToUpperInvariant(),
                $"Located {buildingClass} ({eventId})",
                "Active planetary scan marker in your save.",
                false),
        };
    }

}
