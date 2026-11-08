using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// A cluster of 2 or 3 resource hotspots within single base building perimeter.
/// Provides optimal Base Computer coordinates and one-click in-game HUD marker staging.
/// </summary>
public sealed partial class PlanetHotspotClusterItemViewModel : ObservableObject
{
    public PlanetHotspotCluster Cluster { get; }

    public string Title => Cluster.Title;
    public HotspotComboType ComboType => Cluster.ComboType;
    public IReadOnlyList<PlanetHotspotItemViewModel> Hotspots { get; }
    public double MaxRadius => Cluster.MaxRadius;
    public double MaxSpan => Cluster.MaxSpan;
    public string BaseCoordinates => Cluster.BaseCoordinates;
    public string ReachDescription => Cluster.ReachDescription;
    public string? PortalDistanceLabel => Cluster.PortalDistanceLabel;
    public string ComboGlyph => Cluster.ComboGlyph;

    public Brush BadgeBrush => ThemeResources.Brush(Cluster.ComboBadgeKey);
    public Brush CardBorderBrush => ThemeResources.Brush(IsTrifecta ? "ThemeWarningBrush" : "ThemeLineBrush");
    public Microsoft.UI.Xaml.Thickness CardBorderThickness => new(IsTrifecta ? 2 : 1);

    public bool IsTrifecta => ComboType == HotspotComboType.Trifecta;
    public bool HasPortalDistance => !string.IsNullOrEmpty(PortalDistanceLabel);

    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Add base marker to save";
    [ObservableProperty] public partial string MarkerStatus { get; private set; } = "";
    [ObservableProperty] public partial bool IsCopied { get; private set; }

    public PlanetHotspotClusterItemViewModel(PlanetHotspotCluster cluster)
    {
        Cluster = cluster;
        Hotspots = cluster.Hotspots.Select(h => new PlanetHotspotItemViewModel(h)).ToList();
        Refresh();
    }

    /// <summary>Updates marker button states from the save session.</summary>
    public void Refresh()
    {
        bool marked = SaveBeaconService.IsPlaced(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        bool unsaved = SaveBeaconService.IsUnsaved(Cluster.PlanetAddress, Cluster.BaseLocalPosition);

        CanUndoMarker = unsaved;
        CanAddMarker = !unsaved && SaveBeaconService.CanAdd;
        AddMarkerText = marked && !unsaved ? "Save Beacon placed ✓" : "Place Save Beacon at site";

        MarkerStatus = unsaved ? "Save Beacon placed at optimal site. Save your changes to write to your save."
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to place a Save Beacon."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "Save Beacon placed in save. Close No Man's Sky to modify."
            : marked ? "Save Beacon placed in save. Permanent star waypoint active."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to place a Save Beacon."
            : "Places a physical in-game Save Beacon at the optimal Base Computer location.";
    }

    [RelayCommand]
    public void AddMarker()
    {
        SaveBeaconService.Add(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        Refresh();
    }

    [RelayCommand]
    public void UndoMarker()
    {
        SaveBeaconService.Remove(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        Refresh();
    }
}
