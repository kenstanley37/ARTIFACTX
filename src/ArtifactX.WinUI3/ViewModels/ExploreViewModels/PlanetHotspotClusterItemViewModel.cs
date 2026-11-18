using ArtifactX.Core.Recipes;
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

    /// <summary>"OPTION 2 · PORTAL + POWER": the cards are alternative spots for one base, numbered in ranking order.</summary>
    public string Title => OptionNumber > 0 ? $"OPTION {OptionNumber} · {Cluster.Title}" : Cluster.Title;

    /// <summary>The card's place in the ranking (1 = best), set when the list loads.</summary>
    public int OptionNumber { get; set; }

    /// <summary>Another card on this page already has the Save Beacon: one base, one beacon, so this one can't add its own
    /// until that one is removed (owner request 2026-11-17).</summary>
    public bool BlockedByOther
    {
        get => _blockedByOther;
        set { if (_blockedByOther != value) { _blockedByOther = value; Refresh(); } }
    }
    private bool _blockedByOther;
    public HotspotComboType ComboType => Cluster.ComboType;
    public IReadOnlyList<PlanetHotspotItemViewModel> Hotspots { get; }

    /// <summary>The card's hotspot rows: each with its distance from this card's base computer and from the portal.</summary>
    public IReadOnlyList<ClusterHotspotRow> Rows { get; }

    /// <summary>The planet's portal is known, so the From Portal column and the base computer's distance show.</summary>
    public bool HasPortalColumn { get; }

    /// <summary>How forgiving the spot is: the farthest thing it must reach against a new base's boundary.</summary>
    public string SpareText { get; }

    /// <summary>Green with room to spare, yellow when tight - a base computer placed a little off could miss something.</summary>
    public Brush SpareBrush { get; }

    /// <summary>Near Portal cards also mark the portal with the beacon (the visor finds the hotspots from there).</summary>
    public bool MarksPortal => _portal is not null && Cluster.IncludesPortal;
    public double MaxRadius => Cluster.MaxRadius;
    public double MaxSpan => Cluster.MaxSpan;
    public string BaseCoordinates => Cluster.BaseCoordinates;
    public string ReachDescription => Cluster.ReachDescription;
    public string? PortalDistanceLabel => Cluster.PortalDistance is { } portal ? $"{HotspotPlanner.FormatDistance(portal)} from the portal" : null;
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

    /// <summary>Also the command's CanExecute: a Button's Command decides whether it can be clicked, so the IsEnabled
    /// binding alone let a locked card place a second beacon (owner test 2026-11-17).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddMarkerCommand))]
    public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }
    [ObservableProperty] public partial bool CanRemoveMarker { get; private set; }
    [ObservableProperty] public partial string RemoveButtonText { get; private set; } = "Undo";
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Place Save Beacon at site";
    [ObservableProperty] public partial string MarkerStatus { get; private set; } = "";
    [ObservableProperty] public partial bool IsMarked { get; private set; }
    [ObservableProperty] public partial bool IsCopied { get; private set; }

    private readonly PortalLocation? _portal;
    private bool _addedPortalMarker;

    public PlanetHotspotClusterItemViewModel(PlanetHotspotCluster cluster, Action<PlanetHotspotClusterItemViewModel>? onPinnedChanged = null,
        float baseReach = 0f, PortalLocation? portal = null)
    {
        Cluster = cluster;
        _onPinnedChanged = onPinnedChanged;
        _portal = portal;
        Hotspots = cluster.Hotspots.Select(h => new PlanetHotspotItemViewModel(h)).ToList();
        // The wording is shared with the mobile app (PortalSurvey).
        Rows = Hotspots.Zip(PortalSurvey.Lines(cluster), (h, line) => new ClusterHotspotRow(h, line.Amount, line.FromComputer, line.FromPortal,
            $"{line.Description}: {h.YieldLabel}")).ToList();
        HasPortalColumn = cluster.PortalDistance is not null;
        var (spareText, tight) = PortalSurvey.Spare(cluster, baseReach);
        SpareText = spareText;
        SpareBrush = ThemeResources.Brush(tight ? "ThemeWarningBrush" : "ThemeDoneBrush");
        Refresh();
    }

    /// <summary>Updates marker button states from the save session.</summary>
    public void Refresh()
    {
        bool marked = SaveBeaconService.IsPlaced(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        bool unsaved = SaveBeaconService.IsUnsaved(Cluster.PlanetAddress, Cluster.BaseLocalPosition);

        IsMarked = marked;
        CanAddMarker = !marked && !BlockedByOther && SaveBeaconService.CanAdd;
        CanUndoMarker = marked;
        CanRemoveMarker = SaveBeaconService.CanRemove;
        RemoveButtonText = unsaved ? "Undo" : "Remove";
        AddMarkerText = marked ? (MarksPortal ? "Beacon placed, portal marked ✓" : "Save Beacon placed ✓")
            : BlockedByOther ? "Another option has the beacon"
            : MarksPortal ? "Place Save Beacon and mark the portal" : "Place Save Beacon at site";

        MarkerStatus = unsaved
            ? (MarksPortal ? "Save Beacon placed where the base computer goes, and the portal marked. Save your changes to write them to your save."
                : "Save Beacon placed at optimal site. Save your changes to write to your save.")
            : !SaveSessionManager.IsSaveLoaded ? "Load a save to place a Save Beacon."
            : marked && GameProcessMonitorService.IsGameRunning
                ? "Save Beacon placed in save. Close No Man's Sky to remove."
            : marked ? "Save Beacon placed in save. Click Remove to take it out."
            : BlockedByOther ? "Another option on this page has the Save Beacon. These are different spots for the same base - remove that beacon first to use this spot instead."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to place a Save Beacon."
            : MarksPortal ? "Places a Save Beacon where the base computer goes and marks the portal. Use your visor to find the hotspots from there."
            : "Places an in-game Save Beacon at the optimal Base Computer location.";
    }

    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
        _onPinnedChanged?.Invoke(this);
    }

    [RelayCommand(CanExecute = nameof(CanAddMarker))]
    public void AddMarker()
    {
        Refresh();
        if (!CanAddMarker) return; // locked by another option, already placed, or the game is running
        SaveBeaconService.Add(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        // The same portal marker Planet Details adds (ScanMarkerService, table 3) - unless one is already waiting to save.
        if (MarksPortal && _portal is { } portal && !ScanMarkerService.IsUnsaved(portal.Key))
        {
            ScanMarkerService.Add(portal.PlanetAddress, portal.Key, portal.X, portal.Y, portal.Z, 3, "PORTAL", "Portal");
            _addedPortalMarker = true;
        }
        IsPinned = true;
        _onPinnedChanged?.Invoke(this);
        Refresh();
    }

    [RelayCommand]
    public void UndoMarker()
    {
        SaveBeaconService.Remove(Cluster.PlanetAddress, Cluster.BaseLocalPosition);
        // Only the portal marker this card added, and only while it's still unsaved (Undo puts back what it replaced).
        if (_addedPortalMarker && _portal is { } portal && ScanMarkerService.IsUnsaved(portal.Key)) ScanMarkerService.Remove(portal.Key);
        _addedPortalMarker = false;
        Refresh();
        _onPinnedChanged?.Invoke(this); // the page re-checks which card holds the beacon
    }

}
