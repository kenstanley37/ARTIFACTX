using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One predicted crashed ship on Planet Details: where it is, how far, and an optional save marker.</summary>
public sealed partial class CrashedShipRowViewModel : ObservableObject
{
    private readonly CrashedShipSite _site;

    public CrashedShipRowViewModel(CrashedShipSite site, int number)
    {
        _site = site;
        Title = $"Crashed ship {number}";
        Coordinates = site.Coordinates;
        Distance = site.DistanceKm < 1
            ? $"{site.DistanceKm * 1000:0} m away"
            : $"{site.DistanceKm.ToString("0.0", CultureInfo.CurrentCulture)} km away";
        Refresh();
    }

    public string Title { get; }
    public string Coordinates { get; }
    public string Distance { get; }
    public string AddMarkerName => $"Add a marker for {Title.ToLowerInvariant()} to my save";
    public string UndoMarkerName => $"Undo the marker for {Title.ToLowerInvariant()}";

    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }

    public void Refresh()
    {
        bool marked = ScanMarkerService.IsMarked(_site.Key), unsaved = ScanMarkerService.IsUnsaved(_site.Key);
        CanAddMarker = !marked && ScanMarkerService.CanAdd;
        CanUndoMarker = unsaved;
        Status = unsaved ? "Marker added - save your changes."
            : marked ? "Marked in your save."
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
