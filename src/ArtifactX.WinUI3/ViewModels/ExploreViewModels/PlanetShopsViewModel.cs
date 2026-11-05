using ArtifactX.Core.Collections;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One shop (a Minor Settlement with a multi-tool cabinet and trader) and its optional save marker.</summary>
public sealed partial class ShopSiteRowViewModel : ObservableObject
{
    private readonly BuildingSiteLocation _site;

    public ShopSiteRowViewModel(BuildingSiteLocation site, int number, string fromLabel)
    {
        _site = site;
        Title = $"Shop {number}";
        Distance = $"{site.DistanceKm:0.0} km {fromLabel}";
        Refresh();
    }

    public ulong Key => _site.Key;
    public string Title { get; }
    public string Coordinates => _site.Coordinates;
    public string Distance { get; }
    public string AddMarkerName => $"Add a marker for {Title} at {Coordinates} to my save";
    public string UndoMarkerName => $"Undo the marker for {Title} at {Coordinates}";

    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial bool CanAddMarker { get; private set; }
    [ObservableProperty] public partial string AddMarkerText { get; private set; } = "Add marker";
    [ObservableProperty] public partial bool CanUndoMarker { get; private set; }

    public void Refresh()
    {
        bool marked = ScanMarkerService.IsMarked(_site.Key), unsaved = ScanMarkerService.IsUnsaved(_site.Key);
        CanAddMarker = !unsaved && ScanMarkerService.CanAdd;
        CanUndoMarker = unsaved;
        AddMarkerText = marked && !unsaved ? "Mark again" : "Add marker";
        Status = unsaved ? "Marker added - save your changes."
            : marked ? "Marked in your save. Not showing in game? Mark it again."
            : GameProcessMonitorService.IsGameRunning ? "Close No Man's Sky to add a marker."
            : "";
    }

    /// <summary>The game's own planetary event for shops ("SHOP", scaneventtableplanet.mbin: nearest Shop building),
    /// at the site on the planet radius - confirmed 2026-11-05 on Kaim 30/19, both markers led to their shops.</summary>
    [RelayCommand]
    private void AddMarker()
    {
        ScanMarkerService.Add(_site.PlanetAddress, _site.Key, _site.X, _site.Y, _site.Z, 1, "SHOP", "Shop");
        Refresh();
    }

    [RelayCommand]
    private void UndoMarker()
    {
        ScanMarkerService.Remove(_site.Key);
        Refresh();
    }
}

/// <summary>The shops nearest you (when you're on the planet) or nearest its portal, from the game's building grid
/// (BuildingSiteService), each with a save marker so the player can fly straight there.</summary>
public sealed partial class PlanetShopsViewModel : ObservableObject
{
    private const int BuildingClassShop = 5;   // GcBuildingClassification.Shop
    private const int ShownShops = 5;
    private ulong _planetAddress;

    public ObservableCollection<ShopSiteRowViewModel> Shops { get; } = [];
    [ObservableProperty] public partial bool HasShops { get; private set; }
    [ObservableProperty] public partial string Heading { get; private set; } = "SHOPS";

    public string Hint => "Shops (Minor Settlements with a multi-tool cabinet and a trader), worked out from the game's own building rules. Add a marker and the game leads you there next time you load.";

    public async Task LoadAsync(ulong planetAddress, double latitude, double longitude, bool fromYou)
    {
        if (_planetAddress != planetAddress)
        {
            Shops.Clear();
            HasShops = false;
        }
        _planetAddress = planetAddress;
        var sites = await BuildingSiteService.NearAsync(planetAddress, BuildingClassShop, latitude, longitude, ShownShops);
        if (_planetAddress != planetAddress) return;
        var rows = sites.Select((s, i) => new ShopSiteRowViewModel(s, i + 1, fromYou ? "away" : "from the portal")).ToArray();
        ObservableCollectionSync.Apply(Shops, rows, row => row.Key,
            (current, next) => current.Title == next.Title && current.Distance == next.Distance);
        Heading = fromYou ? "SHOPS NEAR YOU" : "SHOPS NEAR THE PORTAL";
        HasShops = rows.Length > 0;
    }

    public void RefreshMarkers()
    {
        foreach (var shop in Shops) shop.Refresh();
    }
}
