using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// A single planetary deep-level resource hotspot card (Power, Mineral, Gas).
/// Bound to XAML in PlanetHotspotsPanel.
/// </summary>
public sealed partial class PlanetHotspotItemViewModel : ObservableObject
{
    public PlanetHotspot Hotspot { get; }

    public string Kind => Hotspot.Kind;
    public string ResourceId => Hotspot.ResourceId;
    public string ResourceName => Hotspot.ResourceName;
    public string Class => Hotspot.Class;
    public int ClassGrade => Hotspot.ClassGrade;
    public float Strength => Hotspot.Strength;
    public int YieldPercent => Hotspot.YieldPercent;
    public float Range => Hotspot.Range;
    public double Latitude => Hotspot.Latitude;
    public double Longitude => Hotspot.Longitude;
    public string Coordinates => Hotspot.Coordinates;
    public double Distance => Hotspot.Distance;
    public string DistanceLabel => Hotspot.DistanceLabel;
    public string ClassBadgeKey => Hotspot.ClassBadgeKey;
    public string KindBadgeKey => Hotspot.KindBadgeKey;
    public string KindGlyph => Hotspot.KindGlyph;

    public Brush ClassBrush => ThemeResources.ClassBrush(Class);
    public string ClassBadgeText => $"CLASS {Class}";

    public string FullDistanceLabel => Hotspot.PortalDistanceLabel is { Length: > 0 } pDist && Math.Abs(Distance - (Hotspot.PortalDistance ?? 0)) > 50
        ? $"{DistanceLabel} · {pDist}"
        : (Hotspot.PortalDistanceLabel ?? DistanceLabel);

    public string YieldLabel => Kind == "Power"
        ? $"Potential: {Math.Round(Strength)} kP ({YieldPercent}%) · {Math.Round(Range)}u range"
        : $"Density: {YieldPercent}% · {Math.Round(Range)}u range";

    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Add marker to save";
    [ObservableProperty] public partial string MarkerStatus { get; private set; } = "";
    [ObservableProperty] public partial bool IsCopied { get; private set; }

    public PlanetHotspotItemViewModel(PlanetHotspot hotspot)
    {
        Hotspot = hotspot;
        Refresh();
    }

    /// <summary>Updates marker button states from the save session.</summary>
    public void Refresh()
    {
        bool marked = ScanMarkerService.IsMarked(Hotspot.CandidateKey);
        bool unsaved = ScanMarkerService.IsUnsaved(Hotspot.CandidateKey);

        CanUndoMarker = unsaved;
        CanAddMarker = !unsaved && ScanMarkerService.CanAdd;
        AddMarkerText = marked && !unsaved ? "Mark again" : "Add marker to save";

        MarkerStatus = unsaved ? "Marker added. Save your changes to write to your save."
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to add a marker."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "Marked in save. Close No Man's Sky to mark again."
            : marked ? "Marked in save."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a marker."
            : "Adds an in-game HUD marker leading directly to this hotspot.";
    }

    [RelayCommand]
    public async Task AddMarkerAsync()
    {
        var centre = await PlanetCentreService.GetAsync(Hotspot.PlanetAddress);
        if (centre is not { } c) return;

        double x = c.X + Hotspot.LocalPosition.X;
        double y = c.Y + Hotspot.LocalPosition.Y;
        double z = c.Z + Hotspot.LocalPosition.Z;

        ScanMarkerService.Add(Hotspot.PlanetAddress, Hotspot.CandidateKey, x, y, z, 1, "HOTSPOT", "Hotspot");
        Refresh();
    }

    [RelayCommand]
    public void UndoMarker()
    {
        ScanMarkerService.Remove(Hotspot.CandidateKey);
        Refresh();
    }

    [RelayCommand]
    public async Task CopyCoordinatesAsync()
    {
        try
        {
            var package = new DataPackage();
            package.SetText(Coordinates);
            Clipboard.SetContent(package);
            IsCopied = true;
            await Task.Delay(1500);
            IsCopied = false;
        }
        catch
        {
            // Ignore clipboard errors
        }
    }
}
