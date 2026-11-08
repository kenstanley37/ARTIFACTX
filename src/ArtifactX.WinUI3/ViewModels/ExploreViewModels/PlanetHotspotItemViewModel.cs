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
    [ObservableProperty] public partial bool CanRemoveMarker { get; private set; }
    [ObservableProperty] public partial string RemoveButtonText { get; private set; } = "Undo";
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Place Save Beacon";
    [ObservableProperty] public partial string MarkerStatus { get; private set; } = "";
    [ObservableProperty] public partial bool IsMarked { get; private set; }
    [ObservableProperty] public partial bool IsCopied { get; private set; }

    public PlanetHotspotItemViewModel(PlanetHotspot hotspot)
    {
        Hotspot = hotspot;
        Refresh();
    }

    /// <summary>Updates marker button states from the save session.</summary>
    public void Refresh()
    {
        bool marked = SaveBeaconService.IsPlaced(Hotspot.PlanetAddress, Hotspot.LocalPosition);
        bool unsaved = SaveBeaconService.IsUnsaved(Hotspot.PlanetAddress, Hotspot.LocalPosition);

        IsMarked = marked;
        CanAddMarker = !marked && SaveBeaconService.CanAdd;
        CanUndoMarker = marked;
        CanRemoveMarker = SaveBeaconService.CanRemove;
        RemoveButtonText = unsaved ? "Undo" : "Remove";
        AddMarkerText = marked ? "Save Beacon placed ✓" : "Place Save Beacon";

        MarkerStatus = unsaved ? "Save Beacon placed. Save your changes to write to your save."
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to place a Save Beacon."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "Save Beacon placed in save. Close No Man's Sky to remove."
            : marked ? "Save Beacon placed in save. Click Remove to take it out."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to place a Save Beacon."
            : "Places an in-game Save Beacon at this extraction hotspot.";
    }

    [RelayCommand]
    public void AddMarker()
    {
        SaveBeaconService.Add(Hotspot.PlanetAddress, Hotspot.LocalPosition);
        Refresh();
    }

    [RelayCommand]
    public void UndoMarker()
    {
        SaveBeaconService.Remove(Hotspot.PlanetAddress, Hotspot.LocalPosition);
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
