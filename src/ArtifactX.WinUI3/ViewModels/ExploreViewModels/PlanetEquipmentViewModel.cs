using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public async Task LoadAsync(KnownPlanet planet, PlanetSnapshot snapshot)
    {
        _centre = await PlanetCentreService.GetAsync(planet.Address);
        UpdateLocations(planet, snapshot);
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
