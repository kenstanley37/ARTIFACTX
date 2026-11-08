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
/// State and operations for the Planetary Hotspots locator panel.
/// </summary>
public sealed partial class PlanetHotspotsViewModel : ObservableObject
{
    private readonly List<PlanetHotspotItemViewModel> _all = new();
    private ulong _currentPlanetAddress;
    private double _currentLat;
    private double _currentLon;

    [ObservableProperty] public partial ObservableCollection<PlanetHotspotItemViewModel> Hotspots { get; private set; } = new();

    [ObservableProperty] public partial bool IsLoading { get; private set; }
    [ObservableProperty] public partial bool HasHotspots { get; private set; }
    [ObservableProperty] public partial string ReferenceLocationLabel { get; private set; } = "";
    [ObservableProperty] public partial string TotalCountLabel { get; private set; } = "";

    [ObservableProperty] public partial int SelectedFilterIndex { get; set; } = 0;

    [ObservableProperty] public partial IReadOnlyList<string> FilterChoices { get; private set; } = ["All", "⚡ Power", "⛏ Mineral", "☁ Gas", "★ S-Class"];

    [ObservableProperty] public partial string AllFilterLabel { get; private set; } = "All";
    [ObservableProperty] public partial string PowerFilterLabel { get; private set; } = "⚡ Power";
    [ObservableProperty] public partial string MineralFilterLabel { get; private set; } = "⛏ Mineral";
    [ObservableProperty] public partial string GasFilterLabel { get; private set; } = "☁ Gas";
    [ObservableProperty] public partial string SClassFilterLabel { get; private set; } = "★ S-Class";

    partial void OnSelectedFilterIndexChanged(int value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Loads predicted hotspots for the specified planet centered around the given coordinates.
    /// </summary>
    public async Task LoadAsync(ulong planetAddress, double centerLat, double centerLon, bool isHere)
    {
        // Avoid reload if already on the same planet and coordinates haven't shifted significantly (< 50 meters)
        if (_currentPlanetAddress == planetAddress
            && _all.Count > 0
            && Math.Abs(_currentLat - centerLat) < 0.0005
            && Math.Abs(_currentLon - centerLon) < 0.0005)
        {
            RefreshMarkers();
            return;
        }

        _currentPlanetAddress = planetAddress;
        _currentLat = centerLat;
        _currentLon = centerLon;

        IsLoading = true;
        ReferenceLocationLabel = isHere
            ? $"Showing deep-level hotspots within survey range of your position ({NmsPlanetCoordinates.Format(centerLat, centerLon)})."
            : $"Showing deep-level hotspots within survey range of the portal ({NmsPlanetCoordinates.Format(centerLat, centerLon)}).";

        try
        {
            var detected = await PlanetHotspotService.GetHotspotsAsync(planetAddress, centerLat, centerLon, expandSearch: true);
            if (_currentPlanetAddress != planetAddress) return; // Switched planets meanwhile

            _all.Clear();
            foreach (var h in detected)
            {
                _all.Add(new PlanetHotspotItemViewModel(h));
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

            FilterChoices = [AllFilterLabel, PowerFilterLabel, MineralFilterLabel, GasFilterLabel, SClassFilterLabel];

            TotalCountLabel = $"{allCount} hotspots detected within survey range.";
            HasHotspots = allCount > 0;

            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlanetHotspotsViewModel] Error loading hotspots: {ex.Message}");
            HasHotspots = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<PlanetHotspotItemViewModel> query = SelectedFilterIndex switch
        {
            1 => _all.Where(h => h.Kind == "Power"),
            2 => _all.Where(h => h.Kind == "Mineral"),
            3 => _all.Where(h => h.Kind == "Gas"),
            4 => _all.Where(h => h.Class == "S"),
            _ => _all
        };

        Hotspots.Clear();
        foreach (var item in query)
        {
            Hotspots.Add(item);
        }
    }

    /// <summary>Refreshes the marked/unsaved status on all hotspot cards.</summary>
    public void RefreshMarkers()
    {
        foreach (var item in _all)
        {
            item.Refresh();
        }
    }
}
