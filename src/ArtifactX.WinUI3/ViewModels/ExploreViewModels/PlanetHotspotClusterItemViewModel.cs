using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// A cluster of 2 or 3 resource hotspots within single base building perimeter.
/// Provides optimal Base Computer coordinates, card pinning/highlighting, and one-click in-game HUD marker staging.
/// </summary>
public sealed partial class PlanetHotspotClusterItemViewModel : ObservableObject
{
    private readonly Action<PlanetHotspotClusterItemViewModel>? _onPinnedChanged;

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

    public Brush CardBorderBrush => IsPinned
        ? ThemeResources.Brush("ThemeAccentBrush")
        : ThemeResources.Brush(IsTrifecta ? "ThemeWarningBrush" : "ThemeLineBrush");

    public Microsoft.UI.Xaml.Thickness CardBorderThickness => new(IsPinned ? 2 : (IsTrifecta ? 2 : 1));

    public Brush CardBackground => IsPinned
        ? ThemeResources.Brush("ThemePanelAltBrush")
        : ThemeResources.Brush("ThemeVoidBrush");

    public string PinButtonText => IsPinned ? "Pinned" : "Pin";
    public Brush PinForeground => IsPinned ? ThemeResources.Brush("ThemeAccentBrush") : ThemeResources.Brush("ThemeTextDimBrush");
    public Brush PinButtonBackground => IsPinned ? ThemeResources.Brush("ThemeVoidBrush") : ThemeResources.Brush("ThemePanelAltBrush");
    public Brush PinButtonBorderBrush => IsPinned ? ThemeResources.Brush("ThemeAccentBrush") : ThemeResources.Brush("ThemeLineBrush");
    public string PinTooltip => IsPinned ? "Unpin card (allow normal sorting)" : "Pin card to top (keeps visible as you move)";

    public bool IsTrifecta => ComboType == HotspotComboType.Trifecta;
    public bool HasPortalDistance => !string.IsNullOrEmpty(PortalDistanceLabel);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardBorderBrush))]
    [NotifyPropertyChangedFor(nameof(CardBorderThickness))]
    [NotifyPropertyChangedFor(nameof(CardBackground))]
    [NotifyPropertyChangedFor(nameof(PinButtonText))]
    [NotifyPropertyChangedFor(nameof(PinForeground))]
    [NotifyPropertyChangedFor(nameof(PinButtonBackground))]
    [NotifyPropertyChangedFor(nameof(PinButtonBorderBrush))]
    [NotifyPropertyChangedFor(nameof(PinTooltip))]
    public partial bool IsPinned { get; set; }

    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }
    [ObservableProperty] public partial bool CanRemoveMarker { get; private set; }
    [ObservableProperty] public partial string RemoveButtonText { get; private set; } = "Undo";
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Place Save Beacon at site";
    [ObservableProperty] public partial string MarkerStatus { get; private set; } = "";
    [ObservableProperty] public partial bool IsMarked { get; private set; }
    [ObservableProperty] public partial bool IsCopied { get; private set; }

    public PlanetHotspotClusterItemViewModel(PlanetHotspotCluster cluster, Action<PlanetHotspotClusterItemViewModel>? onPinnedChanged = null)
    {
        Cluster = cluster;
        _onPinnedChanged = onPinnedChanged;
        Hotspots = cluster.Hotspots.Select(h => new PlanetHotspotItemViewModel(h)).ToList();
        Refresh();
    }

    /// <summary>Updates marker button states from the save session.</summary>
    public void Refresh()
    {
        bool marked = SaveBeaconService.IsPlaced(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        bool unsaved = SaveBeaconService.IsUnsaved(Cluster.PlanetAddress, Cluster.BaseLocalPosition);

        IsMarked = marked;
        CanAddMarker = !marked && SaveBeaconService.CanAdd;
        CanUndoMarker = marked;
        CanRemoveMarker = SaveBeaconService.CanRemove;
        RemoveButtonText = unsaved ? "Undo" : "Remove";
        AddMarkerText = marked ? "Save Beacon placed ✓" : "Place Save Beacon at site";

        MarkerStatus = unsaved ? "Save Beacon placed at optimal site. Save your changes to write to your save."
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to place a Save Beacon."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "Save Beacon placed in save. Close No Man's Sky to remove."
            : marked ? "Save Beacon placed in save. Click Remove to take it out."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to place a Save Beacon."
            : "Places an in-game Save Beacon at the optimal Base Computer location.";
    }

    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
        _onPinnedChanged?.Invoke(this);
    }

    [RelayCommand]
    public void AddMarker()
    {
        SaveBeaconService.Add(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        IsPinned = true;
        _onPinnedChanged?.Invoke(this);
        Refresh();
    }

    [RelayCommand]
    public void UndoMarker()
    {
        SaveBeaconService.Remove(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        Refresh();
    }
}
