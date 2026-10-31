using System;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One predicted crashed ship on Planet Details: which ship, where it is, how far, and an optional save marker.</summary>
public sealed partial class CrashedShipRowViewModel : ObservableObject
{
    private CrashedShipSite _site;
    private readonly bool _savedOnPlanet;

    /// <param name="savedOnPlanet">Whether the save was made on this planet (kept for when the marker rule is known).
    /// The game sometimes leads a crashed-ship marker to a different wreck: twice from a save in space (Nugepan III),
    /// and once from a save on the planet with the site 177 km away (Mephar VI, S-class Skylark of Gravity marker led
    /// to a B-class Josonix's Defeat BO3, 2026-10-29). Near markers led true. Not traced yet, so the row warns.</param>
    /// <param name="from">What the distance is measured from: "away" (you), "from the portal".</param>
    public CrashedShipRowViewModel(CrashedShipSite site, int number, bool savedOnPlanet, string from = "away")
    {
        _site = site;
        _savedOnPlanet = savedOnPlanet;
        // Crashed Sentinel ships have names of their own ("Crimson-σ", "Reforged-ιζ"), not the system's Sentinel ship's
        // (Nionne Sigma, 2026-10-29) - not traced yet, so no name.
        Title = site.Sentinel ? "Crashed Sentinel ship"
            : site.Ship?.DefaultName is { Length: > 0 } name ? name : $"Crashed ship {number}";
        Coordinates = site.Coordinates;
        string distance = site.DistanceKm < 1
            ? $"{site.DistanceKm * 1000:0} m {from}"
            : $"{site.DistanceKm.ToString("0.0", CultureInfo.CurrentCulture)} km {from}";
        string type = site.Ship is { } ship
            ? site.ShownClass is int cls ? $"{"CBAS"[cls]}-class {ship.TypeLabel}" : ship.TypeLabel   // an Exotic is always S
            : "";
        if (site.Underwater)
        {
            string water = site.DepthMetres is double depth ? $"about {Math.Round(depth / 10) * 10:N0} m deep" : "underwater";
            type = type.Length > 0 ? $"{type} · {water}" : water;
        }
        if (site.Inventory is { } inv)
        {
            string unitText = $"{inv.UnitValue:N0} units";
            type = type.Length > 0 ? $"{type} · {unitText}" : unitText;
            DamageText = $"+{inv.DamageBonus:F1}%";
            ShieldText = $"+{inv.ShieldBonus:F1}%";
            HyperdriveText = $"+{inv.HyperdriveBonus:F1}%";
            ManeuverabilityText = $"+{inv.ManeuverabilityBonus:F1}%";
            DamageBar = Math.Clamp(inv.DamageBonus / 90.0 * 100.0, 0.0, 100.0);
            ShieldBar = Math.Clamp(inv.ShieldBonus / 125.0 * 100.0, 0.0, 100.0);
            HyperdriveBar = Math.Clamp(inv.HyperdriveBonus / 120.0 * 100.0, 0.0, 100.0);
            ManeuverabilityBar = Math.Clamp(inv.ManeuverabilityBonus / 50.0 * 100.0, 0.0, 100.0);
            HasStats = true;
        }
        if (site.DisplayedStats is { } stats)
        {
            DamageText = stats.Damage.ToString("F1", CultureInfo.CurrentCulture);
            ShieldText = stats.Shield.ToString("F1", CultureInfo.CurrentCulture);
            HyperdriveText = stats.Hyperdrive.ToString("F1", CultureInfo.CurrentCulture);
            ManeuverabilityText = stats.Maneuverability.ToString("F1", CultureInfo.CurrentCulture);
            // Card bars are visual guides; the numerical values are the comparison result.
            DamageBar = Math.Clamp(stats.Damage / 600.0 * 100, 0, 100);
            ShieldBar = Math.Clamp(stats.Shield / 500.0 * 100, 0, 100);
            HyperdriveBar = Math.Clamp(stats.Hyperdrive / 2500.0 * 100, 0, 100);
            ManeuverabilityBar = Math.Clamp(stats.Maneuverability / 1500.0 * 100, 0, 100);
            HasStats = true;
        }
        Distance = type.Length > 0 ? $"{type} · {distance}" : distance;
        Refresh();
    }

    public ulong Key => _site.Key;
    public string DamageLabel => _site.DisplayedStats is null ? "Damage bonus" : "Damage Potential";
    public string ShieldLabel => _site.DisplayedStats is null ? "Shield bonus" : "Shield Strength";
    public string HyperdriveLabel => _site.DisplayedStats is null ? "Hyperdrive bonus" : "Hyperdrive Range";
    public string ManeuverabilityLabel => _site.DisplayedStats is null ? "Maneuverability bonus" : "Maneuverability";

    public void UpdateFrom(CrashedShipRowViewModel next)
    {
        _site = next._site;
        Title = next.Title;
        Coordinates = next.Coordinates;
        Distance = next.Distance;
        HasStats = next.HasStats;
        DamageText = next.DamageText; ShieldText = next.ShieldText;
        HyperdriveText = next.HyperdriveText; ManeuverabilityText = next.ManeuverabilityText;
        DamageBar = next.DamageBar; ShieldBar = next.ShieldBar;
        HyperdriveBar = next.HyperdriveBar; ManeuverabilityBar = next.ManeuverabilityBar;
        OnPropertyChanged(nameof(DamageLabel)); OnPropertyChanged(nameof(ShieldLabel));
        OnPropertyChanged(nameof(HyperdriveLabel)); OnPropertyChanged(nameof(ManeuverabilityLabel));
        OnPropertyChanged(nameof(AddMarkerName)); OnPropertyChanged(nameof(UndoMarkerName));
        Refresh();
    }

    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string Coordinates { get; private set; } = "";
    [ObservableProperty] public partial string Distance { get; private set; } = "";
    [ObservableProperty] public partial bool HasStats { get; private set; }
    [ObservableProperty] public partial string DamageText { get; private set; } = "";
    [ObservableProperty] public partial string ShieldText { get; private set; } = "";
    [ObservableProperty] public partial string HyperdriveText { get; private set; } = "";
    [ObservableProperty] public partial string ManeuverabilityText { get; private set; } = "";
    [ObservableProperty] public partial double DamageBar { get; private set; }
    [ObservableProperty] public partial double ShieldBar { get; private set; }
    [ObservableProperty] public partial double HyperdriveBar { get; private set; }
    [ObservableProperty] public partial double ManeuverabilityBar { get; private set; }
    public string AddMarkerName => $"Add a marker for the crashed ship {Title} to my save";
    public string UndoMarkerName => $"Undo the marker for the crashed ship {Title}";

    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Add marker";
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }

    public void Refresh()
    {
        bool marked = ScanMarkerService.IsMarked(_site.Key), unsaved = ScanMarkerService.IsUnsaved(_site.Key);
        CanAddMarker = !unsaved && ScanMarkerService.CanAdd;
        CanUndoMarker = unsaved;
        AddMarkerText = marked && !unsaved ? "Mark again" : "Add marker";
        Status = unsaved ? "Marker added - save your changes."
            : marked ? "Marked in your save. Not showing in game? Mark it again."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a marker."
            : _site.Sentinel ? "Each crashed Sentinel ship has its own name - not known yet."
            : _site.DistanceKm > 25 ? "The game may lead a far marker to a different wreck. Check the coordinates on arrival."
            : "";
    }

    /// <summary>The game's own planetary event for crashed ships ("DISTRESS", scaneventtableplanet.mbin: searches
    /// for the nearest DistressSignal building), at the site on the planet radius like the confirmed Observatory test.</summary>
    [RelayCommand]
    private void AddMarker()
    {
        // Underwater wrecks are the WaterDistressSignal building; the game's charts have no event of their own
        // (scaneventtableplanet.mbin: DISTRESS, DISTRESS_NPC), so the marker is the DISTRESS event naming that class -
        // confirmed 2026-10-29 (Waterworld: the marker led to Rozawa Guardian III at +29.40, +2.72).
        ScanMarkerService.Add(_site.PlanetAddress, _site.Key, _site.X, _site.Y, _site.Z, 1, "DISTRESS",
            _site.Underwater ? "WaterDistressSignal" : _site.Sentinel ? "SentinelDistressSignal" : "DistressSignal");
        Refresh();
    }

    [RelayCommand]
    private void UndoMarker()
    {
        ScanMarkerService.Remove(_site.Key);
        Refresh();
    }
}
