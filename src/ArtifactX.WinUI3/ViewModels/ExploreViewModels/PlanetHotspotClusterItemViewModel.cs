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
        bool marked = ScanMarkerService.IsMarked(Cluster.ClusterKey);
        bool unsaved = ScanMarkerService.IsUnsaved(Cluster.ClusterKey);

        CanUndoMarker = unsaved;
        CanAddMarker = !unsaved && ScanMarkerService.CanAdd;
        AddMarkerText = marked && !unsaved ? "Mark base again" : "Add base marker to save";

        MarkerStatus = unsaved ? "Base marker added. Save your changes to write to your save."
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to add a base marker."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "Marked in save. Close No Man's Sky to mark again."
            : marked ? "Marked in save."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a base marker."
            : "Adds an in-game HUD marker at the optimal Base Computer location.";
    }

    [RelayCommand]
    public async Task AddMarkerAsync()
    {
        var centre = await PlanetCentreService.GetAsync(Cluster.PlanetAddress);
        if (centre is not { } c) return;

        double x = c.X + Cluster.BaseLocalPosition.X;
        double y = c.Y + Cluster.BaseLocalPosition.Y;
        double z = c.Z + Cluster.BaseLocalPosition.Z;

        ScanMarkerService.Add(Cluster.PlanetAddress, Cluster.ClusterKey, x, y, z, 1, "BASE", "Base");
        Refresh();
    }

    [RelayCommand]
    public void UndoMarker()
    {
        ScanMarkerService.Remove(Cluster.ClusterKey);
        Refresh();
    }
}
