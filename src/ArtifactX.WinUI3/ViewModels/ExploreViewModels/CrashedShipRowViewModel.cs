using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One predicted crashed ship on Planet Details: which ship, where it is, how far, and an optional save marker.</summary>
public sealed partial class CrashedShipRowViewModel : ObservableObject
{
    private readonly CrashedShipSite _site;
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
        Title = site.Ship?.DefaultName is { Length: > 0 } name ? name : $"Crashed ship {number}";
        Coordinates = site.Coordinates;
        string distance = site.DistanceKm < 1
            ? $"{site.DistanceKm * 1000:0} m {from}"
            : $"{site.DistanceKm.ToString("0.0", CultureInfo.CurrentCulture)} km {from}";
        string type = site.Ship is { } ship
            ? site.ShipClass is int cls ? $"{"CBAS"[cls]}-class {ship.TypeLabel}" : ship.TypeLabel
            : "";
        Distance = type.Length > 0 ? $"{type} · {distance}" : distance;
        Refresh();
    }

    public string Title { get; }
    public string Coordinates { get; }
    public string Distance { get; }
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
            : _site.DistanceKm > 25 ? "The game may lead a far marker to a different wreck. Check the coordinates on arrival."
            : "";
    }

    /// <summary>The game's own planetary event for crashed ships ("DISTRESS", scaneventtableplanet.mbin: searches
    /// for the nearest DistressSignal building), at the site on the planet radius like the confirmed Observatory test.</summary>
    [RelayCommand]
    private void AddMarker()
    {
        ScanMarkerService.Add(_site.PlanetAddress, _site.Key, _site.X, _site.Y, _site.Z, 1, "DISTRESS", "DistressSignal");
        Refresh();
    }

    [RelayCommand]
    private void UndoMarker()
    {
        ScanMarkerService.Remove(_site.Key);
        Refresh();
    }
}
