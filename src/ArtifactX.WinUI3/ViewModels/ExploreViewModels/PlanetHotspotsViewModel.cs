using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// State and operations for the Planetary Hotspots and Base Cluster locator panel.
/// </summary>
public sealed partial class PlanetHotspotsViewModel : ObservableObject
{
    private readonly List<PlanetHotspotItemViewModel> _all = new();
    private readonly List<PlanetHotspotClusterItemViewModel> _allClusters = new();
    private readonly HashSet<ulong> _pinnedClusterKeys = new();
    private readonly HashSet<ulong> _pinnedHotspotKeys = new();

    private ulong _currentPlanetAddress;
    private double _currentLat;
    private double _currentLon;
    private double? _portalLat;
    private double? _portalLon;

    [ObservableProperty] public partial ObservableCollection<PlanetHotspotItemViewModel> Hotspots { get; private set; } = new();
    [ObservableProperty] public partial ObservableCollection<PlanetHotspotClusterItemViewModel> Clusters { get; private set; } = new();

    [ObservableProperty] public partial bool IsLoading { get; private set; }
    [ObservableProperty] public partial bool HasHotspots { get; private set; }
    [ObservableProperty] public partial bool HasClusters { get; private set; }
    [ObservableProperty] public partial string ReferenceLocationLabel { get; private set; } = "";
    [ObservableProperty] public partial string TotalCountLabel { get; private set; } = "";
    [ObservableProperty] public partial string ClusterCountLabel { get; private set; } = "";

    private int _activeViewModeIndex = 0;
    private int _activeFilterIndex = 0;

    // View Mode: 0 = All Sites, 1 = Base Clusters & Combos
    [ObservableProperty] public partial int SelectedViewModeIndex { get; set; } = 0;
    [ObservableProperty] public partial bool IsSingleSitesView { get; private set; } = true;
    [ObservableProperty] public partial bool IsClustersView { get; private set; } = false;
    public ObservableCollection<string> ViewModeChoices { get; } = new(["All Sites", "Base Combos & Clusters"]);

    // Search and Sort
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial int SelectedFilterIndex { get; set; } = 0;
    [ObservableProperty] public partial int SelectedSortIndex { get; set; } = 0;

    [ObservableProperty] public partial IReadOnlyList<string> SortChoices { get; private set; } =
        ["Distance: Nearest First", "Portal: Closest to Portal", "Quality: Class (S → C)", "Resource: Name (A-Z)"];

    public ObservableCollection<string> FilterChoices { get; } = new(["All", "⚡ Power", "⛏ Mineral", "☁ Gas", "★ S-Class"]);

    [ObservableProperty] public partial string AllFilterLabel { get; private set; } = "All";
    [ObservableProperty] public partial string PowerFilterLabel { get; private set; } = "⚡ Power";
    [ObservableProperty] public partial string MineralFilterLabel { get; private set; } = "⛏ Mineral";
    [ObservableProperty] public partial string GasFilterLabel { get; private set; } = "☁ Gas";
    [ObservableProperty] public partial string SClassFilterLabel { get; private set; } = "★ S-Class";

    partial void OnSelectedViewModeIndexChanged(int value)
    {
        if (value < 0)
        {
            if (SelectedViewModeIndex != _activeViewModeIndex)
                SelectedViewModeIndex = _activeViewModeIndex;
            return;
        }
        _activeViewModeIndex = value;
        IsSingleSitesView = value == 0;
        IsClustersView = value == 1;
        ApplyFiltersAndSorting();
    }

    partial void OnSelectedFilterIndexChanged(int value)
    {
        if (value < 0)
        {
            if (SelectedFilterIndex != _activeFilterIndex)
                SelectedFilterIndex = _activeFilterIndex;
            return;
        }
        _activeFilterIndex = value;
        ApplyFiltersAndSorting();
    }

    partial void OnSelectedSortIndexChanged(int value)
    {
        ApplyFiltersAndSorting();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFiltersAndSorting();
    }

    private void OnClusterPinnedChanged(PlanetHotspotClusterItemViewModel item)
    {
        if (item.IsPinned)
            _pinnedClusterKeys.Add(item.Cluster.ClusterKey);
        else
            _pinnedClusterKeys.Remove(item.Cluster.ClusterKey);

        ApplyFiltersAndSorting();
    }

    private void OnHotspotPinnedChanged(PlanetHotspotItemViewModel item)
    {
        if (item.IsPinned)
            _pinnedHotspotKeys.Add(item.Hotspot.CandidateKey);
        else
            _pinnedHotspotKeys.Remove(item.Hotspot.CandidateKey);

        ApplyFiltersAndSorting();
    }

    /// <summary>
    /// Loads predicted hotspots and multi-hotspot clusters for the specified planet centered around the given coordinates.
    /// </summary>
    public async Task LoadAsync(
        ulong planetAddress,
        double centerLat,
        double centerLon,
        bool isHere,
        double? portalLat = null,
        double? portalLon = null)
    {
        // Calculate distance shifted to avoid scrambling cards while walking locally (< 300 meters)
        double movedMeters = double.MaxValue;
        if (_currentPlanetAddress == planetAddress && _all.Count > 0)
        {
            double lat1 = _currentLat * Math.PI / 180.0;
            double lon1 = _currentLon * Math.PI / 180.0;
            double lat2 = centerLat * Math.PI / 180.0;
            double lon2 = centerLon * Math.PI / 180.0;
            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            movedMeters = c * 98304.0;
        }

        if (_currentPlanetAddress == planetAddress && _all.Count > 0 && movedMeters < 300.0)
        {
            RefreshMarkers();
            return;
        }

        _currentPlanetAddress = planetAddress;
        _currentLat = centerLat;
        _currentLon = centerLon;
        _portalLat = portalLat;
        _portalLon = portalLon;

        IsLoading = true;
        ReferenceLocationLabel = isHere
            ? $"Showing deep-level hotspots within survey range of your position ({NmsPlanetCoordinates.Format(centerLat, centerLon)})."
            : $"Showing deep-level hotspots within survey range of the portal ({NmsPlanetCoordinates.Format(centerLat, centerLon)}).";

        try
        {
            var data = await PlanetHotspotService.GetHotspotDataAsync(planetAddress, centerLat, centerLon, expandSearch: true, portalLat, portalLon);
            if (_currentPlanetAddress != planetAddress) return; // Switched planets meanwhile

            _all.Clear();
            foreach (var h in data.Hotspots)
            {
                var item = new PlanetHotspotItemViewModel(h, OnHotspotPinnedChanged)
                {
                    IsPinned = _pinnedHotspotKeys.Contains(h.CandidateKey)
                };
                _all.Add(item);
            }

            int allCount = _all.Count;
            int powerCount = _all.Count(h => h.Kind == "Power");
            int minCount = _all.Count(h => h.Kind == "Mineral");
            int gasCount = _all.Count(h => h.Kind == "Gas");
            int sCount = _all.Count(h => h.Class == "S");

            AllFilterLabel = $"All ({allCount})";
            PowerFilterLabel = $"⚡ Power ({powerCount})";
            MineralFilterLabel = $"⛏ Mineral ({minCount})";
            GasFilterLabel = $"☁ Gas ({gasCount})";
            SClassFilterLabel = $"★ S-Class ({sCount})";

            if (FilterChoices.Count >= 5)
            {
                FilterChoices[0] = AllFilterLabel;
                FilterChoices[1] = PowerFilterLabel;
                FilterChoices[2] = MineralFilterLabel;
                FilterChoices[3] = GasFilterLabel;
                FilterChoices[4] = SClassFilterLabel;
            }

            // Populate multi-hotspot clusters / combos within base reach
            _allClusters.Clear();
            foreach (var c in data.Clusters)
            {
                var item = new PlanetHotspotClusterItemViewModel(c, OnClusterPinnedChanged)
                {
                    IsPinned = _pinnedClusterKeys.Contains(c.ClusterKey)
                };
                _allClusters.Add(item);
            }

            int clusterCount = _allClusters.Count;
            int trifectaCount = _allClusters.Count(c => c.ComboType == HotspotComboType.Trifecta);

            string mode0 = $"All Sites ({allCount})";
            string mode1 = trifectaCount > 0 ? $"Base Combos ({clusterCount} · {trifectaCount} Trifecta)" : $"Base Combos ({clusterCount})";
            if (ViewModeChoices.Count >= 2)
            {
                ViewModeChoices[0] = mode0;
                ViewModeChoices[1] = mode1;
            }

            TotalCountLabel = $"{allCount} hotspots detected within survey range.";
            ClusterCountLabel = clusterCount > 0
                ? $"{clusterCount} multi-hotspot base clusters found within single Base Computer boundary radius (≤ 375u)."
                : "No multi-hotspot clusters within 375u base radius found in this area.";

            HasHotspots = allCount > 0;
            HasClusters = clusterCount > 0;

            // Ensure view mode is applied to single sites or clusters
            IsSingleSitesView = SelectedViewModeIndex == 0;
            IsClustersView = SelectedViewModeIndex == 1;

            ApplyFiltersAndSorting();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlanetHotspotsViewModel] Error loading hotspots: {ex.Message}");
            HasHotspots = false;
            HasClusters = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFiltersAndSorting()
    {
        string filterText = SearchText?.Trim() ?? "";

        // 1. Single Hotspots
        IEnumerable<PlanetHotspotItemViewModel> query = SelectedFilterIndex switch
        {
            1 => _all.Where(h => h.Kind == "Power"),
            2 => _all.Where(h => h.Kind == "Mineral"),
            3 => _all.Where(h => h.Kind == "Gas"),
            4 => _all.Where(h => h.Class == "S"),
            _ => _all
        };

        if (!string.IsNullOrEmpty(filterText))
        {
            query = query.Where(h =>
                h.ResourceName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                h.Kind.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                h.Class.Equals(filterText, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedSortIndex switch
        {
            1 => query.OrderBy(h => h.IsPinned ? 0 : (h.IsMarked ? 1 : 2))
                      .ThenBy(h => h.Hotspot.PortalDistance ?? double.MaxValue)
                      .ThenBy(h => h.Distance),
            2 => query.OrderBy(h => h.IsPinned ? 0 : (h.IsMarked ? 1 : 2))
                      .ThenByDescending(h => h.ClassGrade)
                      .ThenByDescending(h => h.YieldPercent)
                      .ThenBy(h => h.Distance),
            3 => query.OrderBy(h => h.IsPinned ? 0 : (h.IsMarked ? 1 : 2))
                      .ThenBy(h => h.ResourceName)
                      .ThenBy(h => h.Distance),
            _ => query.OrderBy(h => h.IsPinned ? 0 : (h.IsMarked ? 1 : 2))
                      .ThenBy(h => h.Distance)
        };

        Hotspots.Clear();
        foreach (var item in query)
        {
            Hotspots.Add(item);
        }

        // 2. Base Clusters & Combos
        IEnumerable<PlanetHotspotClusterItemViewModel> clusterQuery = _allClusters;
        if (!string.IsNullOrEmpty(filterText))
        {
            clusterQuery = clusterQuery.Where(c =>
                c.Title.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                c.Hotspots.Any(h => h.ResourceName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                                   h.Kind.Contains(filterText, StringComparison.OrdinalIgnoreCase)));
        }

        clusterQuery = SelectedSortIndex switch
        {
            1 => clusterQuery.OrderBy(c => c.IsPinned ? 0 : (c.IsMarked ? 1 : 2))
                             .ThenBy(c => c.Cluster.PortalDistance ?? double.MaxValue)
                             .ThenBy(c => c.MaxRadius),
            2 => clusterQuery.OrderBy(c => c.IsPinned ? 0 : (c.IsMarked ? 1 : 2))
                             .ThenByDescending(c => c.Hotspots.Sum(h => h.ClassGrade))
                             .ThenBy(c => c.MaxRadius),
            3 => clusterQuery.OrderBy(c => c.IsPinned ? 0 : (c.IsMarked ? 1 : 2))
                             .ThenBy(c => c.Title)
                             .ThenBy(c => c.MaxRadius),
            _ => clusterQuery.OrderBy(c => c.IsPinned ? 0 : (c.IsMarked ? 1 : 2))
                             .ThenBy(c => c.IsTrifecta ? 0 : 1)
                             .ThenBy(c => c.MaxRadius)
        };

        Clusters.Clear();
        foreach (var clusterItem in clusterQuery)
        {
            Clusters.Add(clusterItem);
        }
    }

    /// <summary>Refreshes the marked/unsaved status on all hotspot and cluster cards.</summary>
    public void RefreshMarkers()
    {
        foreach (var item in _all)
        {
            item.Refresh();
        }
        foreach (var cluster in _allClusters)
        {
            cluster.Refresh();
        }
    }
}
