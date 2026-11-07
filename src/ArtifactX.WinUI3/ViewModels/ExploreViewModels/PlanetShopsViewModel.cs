using ArtifactX.Core.Collections;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One shop (Minor Settlement) with its coordinates, distance, visited status, and save marker.</summary>
public sealed partial class ShopSiteRowViewModel : ObservableObject
{
    private readonly PlanetShopSite _shop;

    public ShopSiteRowViewModel(PlanetShopSite shop, string fromLabel)
    {
        _shop = shop;
        Distance = $"{shop.DistanceKm:0.0} km {fromLabel}";
        IsCurrentLocation = shop.IsCurrentLocation;
        IsVisited = shop.IsVisited;
        VisitedBadgeText = shop.IsVisited ? "Visited" : "Unvisited";
        VisitedBadgeBrush = ThemeResources.Brush(shop.IsVisited ? "ThemeDoneBrush" : "ThemeTextDimBrush");
        Refresh();
    }

    public ulong Key => _shop.Key;
    public string Coordinates => _shop.Site.Coordinates;
    [ObservableProperty] public partial string Distance { get; private set; }
    public string AddMarkerName => $"Add a marker for the shop at {Coordinates} to my save";
    public string UndoMarkerName => $"Undo the marker for the shop at {Coordinates}";

    [ObservableProperty] public partial bool IsCurrentLocation { get; private set; }
    [ObservableProperty] public partial bool IsVisited { get; private set; }
    [ObservableProperty] public partial string VisitedBadgeText { get; private set; } = "Unvisited";
    [ObservableProperty] public partial Brush VisitedBadgeBrush { get; private set; } = ThemeResources.Brush("ThemeTextDimBrush");

    public string Features => "Trader · Multi-tool cabinet · Trade terminal · Landing pad";

    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Add marker";
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public Brush CardBorder => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : IsCurrentLocation ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Microsoft.UI.Xaml.Thickness CardBorderThickness => new(IsSelected ? 2 : IsCurrentLocation ? 2 : 1);
    public Brush CardBackground => ThemeResources.Brush(IsSelected ? "ThemeSelectedFillBrush" : "ThemeVoidBrush");

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorder));
        OnPropertyChanged(nameof(CardBorderThickness));
        OnPropertyChanged(nameof(CardBackground));
    }

    partial void OnIsCurrentLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorder));
        OnPropertyChanged(nameof(CardBorderThickness));
    }

    public void UpdateFrom(ShopSiteRowViewModel next)
    {
        // Preserve user's manual selection - do NOT overwrite IsSelected
        Distance = next.Distance;
        IsCurrentLocation = next.IsCurrentLocation;
        IsVisited = next.IsVisited;
        VisitedBadgeText = next.VisitedBadgeText;
        VisitedBadgeBrush = next.VisitedBadgeBrush;
        Refresh();
    }

    public void Refresh()
    {
        bool marked = ScanMarkerService.IsMarked(_shop.Key), unsaved = ScanMarkerService.IsUnsaved(_shop.Key);
        CanAddMarker = !unsaved && ScanMarkerService.CanAdd;
        CanUndoMarker = unsaved;
        AddMarkerText = marked && !unsaved ? "Mark again" : "Add marker";
        Status = unsaved ? "Marker added - save your changes."
            : marked ? "Marked in your save. Not showing in game? Mark it again."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a marker."
            : "";
    }

    [RelayCommand]
    private void AddMarker()
    {
        ScanMarkerService.Add(_shop.Site.PlanetAddress, _shop.Key, _shop.Site.X, _shop.Site.Y, _shop.Site.Z, 1, "SHOP", "Shop");
        Refresh();
    }

    [RelayCommand]
    private void UndoMarker()
    {
        ScanMarkerService.Remove(_shop.Key);
        Refresh();
    }
}

/// <summary>
/// The shops (Minor Settlements) on this planet, with visited/unvisited status, distance range filters,
/// "You are here" detection, visor coordinates, and save markers so the player can fly straight there.
/// </summary>
public sealed partial class PlanetShopsViewModel : ObservableObject
{
    private const string ShopVisitedKey = "PlanetDetailsShopVisited",
        ShopRangeKey = "PlanetDetailsShopRange",
        ShopCountKey = "PlanetDetailsShopCount";

    public IReadOnlyList<string> VisitedChoices { get; } = ["All shops", "Unvisited only", "Visited only"];

    [ObservableProperty]
    public partial int VisitedChoice { get; set; } = Remembered(ShopVisitedKey, 2, 0);

    public IReadOnlyList<string> RangeChoices { get; } = ["Any distance", "Within 10 km", "Within 25 km", "Within 50 km", "Within 100 km"];
    private static readonly double[] RangeDistances = [double.MaxValue, 10.0, 25.0, 50.0, 100.0];

    [ObservableProperty]
    public partial int RangeChoice { get; set; } = Remembered(ShopRangeKey, RangeDistances.Length - 1, 0);
    private double MaxRangeKm => RangeDistances[Math.Clamp(RangeChoice, 0, RangeDistances.Length - 1)];

    public IReadOnlyList<string> ShopCountChoices { get; } = ["10", "25", "50", "100", "All"];
    private static readonly int[] ShopCounts = [10, 25, 50, 100, int.MaxValue];

    [ObservableProperty]
    public partial int ShopCountChoice { get; set; } = Remembered(ShopCountKey, ShopCounts.Length - 1, 0);
    private int ShownShops => ShopCounts[Math.Clamp(ShopCountChoice, 0, ShopCounts.Length - 1)];

    public ObservableCollection<ShopSiteRowViewModel> Shops { get; } = [];
    [ObservableProperty] public partial bool HasShops { get; private set; }
    [ObservableProperty] public partial string Heading { get; private set; } = "SHOPS";
    [ObservableProperty] public partial string ShopsSummary { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<SystemMultiToolItemViewModel> Tools { get; private set; } = [];
    [ObservableProperty] public partial string ToolsLabel { get; private set; } = "";

    public string Hint => "Shops with a trader, multi-tool cabinet, trade terminal and landing pad (the game labels them \"Minor Settlement\"), with the coordinates your Analysis Visor shows there. Add a marker and the game leads you there next time you load.";

    private ulong _planetAddress;
    private int _body;
    private double _latitude;
    private double _longitude;
    private bool _fromYou;
    private string _toolsKey = "";
    private PlanetSnapshot? _snapshot;
    private ulong? _selectedShopKey;

    private static int Remembered(string key, int max, int fallback) =>
        int.TryParse(SettingsStore.Get(SettingsStore.Scope.Profile, key), out int i) && i >= 0 && i <= max ? i : fallback;

    partial void OnVisitedChoiceChanged(int value)
    {
        if (value < 0) return;
        SettingsStore.Set(SettingsStore.Scope.Profile, ShopVisitedKey, value.ToString(CultureInfo.InvariantCulture));
        if (_planetAddress != 0) _ = LoadFilteredShopsAsync();
    }

    partial void OnRangeChoiceChanged(int value)
    {
        if (value < 0) return;
        SettingsStore.Set(SettingsStore.Scope.Profile, ShopRangeKey, value.ToString(CultureInfo.InvariantCulture));
        if (_planetAddress != 0) _ = LoadFilteredShopsAsync();
    }

    partial void OnShopCountChoiceChanged(int value)
    {
        if (value < 0) return;
        SettingsStore.Set(SettingsStore.Scope.Profile, ShopCountKey, value.ToString(CultureInfo.InvariantCulture));
        if (_planetAddress != 0) _ = LoadFilteredShopsAsync();
    }

    public async Task LoadAsync(ulong planetAddress, double latitude, double longitude, bool fromYou, int? savedBody, PlanetSnapshot? snapshot = null)
    {
        int index = (int)((planetAddress >> 52) & 0xF);
        int body = savedBody ?? (index >= 1 ? index : 0);

        _planetAddress = planetAddress;
        _body = body;
        _latitude = latitude;
        _longitude = longitude;
        _fromYou = fromYou;
        _snapshot = snapshot;

        Heading = fromYou ? "SHOPS NEAR YOU" : "SHOPS NEAR THE PORTAL";

        var pool = await CabinetPoolService.ForAsync(planetAddress, body);
        if (_planetAddress != planetAddress || _body != body) return;

        string where = body == 0 ? "at the space station" : $"on {UniverseInfo.PlanetName(planetAddress & ~(0xFUL << 52), body)}";
        ToolsLabel = (savedBody is null ? "After warping in, cabinets in these shops draw from these multi-tools."
                : $"Your save loads {where}, so cabinets in these shops draw from these multi-tools.")
            + " Reloading somewhere else in the system changes the multi-tools available in cabinets (see Current System).";

        string toolsKey = string.Join(",", pool.Select(t => t.Seed));
        if (toolsKey != _toolsKey)
        {
            _toolsKey = toolsKey;
            var tools = pool.Select(t => new SystemMultiToolItemViewModel(t)).ToArray();
            Tools = tools;
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            foreach (var tool in tools) _ = tool.ResolveKindAsync(dispatcher);
        }

        await LoadFilteredShopsAsync();
    }

    public async Task LoadFilteredShopsAsync()
    {
        ulong address = _planetAddress;
        double lat = _latitude, lon = _longitude;
        bool fromYou = _fromYou;
        int visitedFilter = VisitedChoice;
        double maxDist = MaxRangeKm;
        int count = ShownShops;
        var snapshot = _snapshot;

        var found = await PlanetShopService.SearchAsync(address, lat, lon, fromYou, visitedFilter, maxDist, count, snapshot);
        if (_planetAddress != address) return;

        if (found is null || found.Nearest.Count == 0)
        {
            Shops.Clear();
            HasShops = false;
            ShopsSummary = found is null ? "No shops available on this planet."
                : visitedFilter == 1 ? "No unvisited shops matching the selected range."
                : visitedFilter == 2 ? "No visited shops matching the selected range."
                : "No shops matching the selected range.";
            return;
        }

        string fromLabel = fromYou ? "away" : "from the portal";
        var rows = found.Nearest.Select(s => new ShopSiteRowViewModel(s, fromLabel)).ToArray();

        ObservableCollectionSync.Apply(Shops, rows, row => row.Key, (_, _) => true, (row, next) => row.UpdateFrom(next));

        // Restore active selection highlight if any
        if (_selectedShopKey is { } selectedKey)
        {
            foreach (var row in Shops)
            {
                row.IsSelected = row.Key == selectedKey;
            }
        }

        HasShops = true;

        string visitedFilterText = visitedFilter switch
        {
            1 => "unvisited ",
            2 => "visited ",
            _ => "",
        };

        string rangeText = maxDist < 1000.0 ? $" within {maxDist:0} km" : "";
        string referencePoint = fromYou ? "you" : "the portal";

        ShopsSummary = found.TotalMatching == 0
            ? $"No {visitedFilterText}shops{rangeText} on this planet."
            : found.TotalMatching == 1
                ? $"The only matching {visitedFilterText}shop{rangeText} on this planet."
                : found.TotalMatching <= count
                    ? $"All {found.TotalMatching:N0} matching {visitedFilterText}shops{rangeText} on this planet, nearest {referencePoint} first. ({found.TotalVisited:N0} visited · {found.TotalUnvisited:N0} unvisited)"
                    : $"The nearest {count} of {found.TotalMatching:N0} matching {visitedFilterText}shops{rangeText} on this planet. ({found.TotalVisited:N0} visited · {found.TotalUnvisited:N0} unvisited)";
    }

    public void ToggleSelection(ShopSiteRowViewModel shop)
    {
        bool makeSelected = !shop.IsSelected;
        foreach (var s in Shops) s.IsSelected = false;
        shop.IsSelected = makeSelected;
        _selectedShopKey = makeSelected ? shop.Key : null;
    }

    public void RefreshMarkers()
    {
        foreach (var shop in Shops) shop.Refresh();
    }
}
