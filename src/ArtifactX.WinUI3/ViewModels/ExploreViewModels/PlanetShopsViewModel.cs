using ArtifactX.Core.Collections;
using ArtifactX.WinUI3.Services;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One shop (a trader and a multi-tool cabinet; the game labels it "Minor Settlement", not to be confused
/// with the settlements a player runs) and its optional save marker.</summary>
public sealed partial class ShopSiteRowViewModel : ObservableObject
{
    private readonly BuildingSiteLocation _site;

    public ShopSiteRowViewModel(BuildingSiteLocation site, string fromLabel)
    {
        _site = site;
        Distance = $"{site.DistanceKm:0.0} km {fromLabel}";
        Refresh();
    }

    public ulong Key => _site.Key;
    public string Coordinates => _site.Coordinates;
    public string Distance { get; }
    public string AddMarkerName => $"Add a marker for the shop at {Coordinates} to my save";
    public string UndoMarkerName => $"Undo the marker for the shop at {Coordinates}";

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
    /// at the site on the planet radius - confirmed 2026-11-05 on Kaim 30/19, every marker led to its shop.</summary>
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
/// (BuildingSiteService), each with a save marker so the player can fly straight there, and - once for all of them -
/// the multi-tools their cabinets can sell under the current load list (CabinetPoolService): which one a given shop
/// has depends on where its cabinet stands, which isn't worked out yet.</summary>
public sealed partial class PlanetShopsViewModel : ObservableObject
{
    private const int BuildingClassShop = 5;   // GcBuildingClassification.Shop
    private const int ShownShops = 5;
    private ulong _planetAddress;
    private string _toolsKey = "";

    public ObservableCollection<ShopSiteRowViewModel> Shops { get; } = [];
    [ObservableProperty] public partial bool HasShops { get; private set; }
    [ObservableProperty] public partial string Heading { get; private set; } = "SHOPS";
    [ObservableProperty] public partial IReadOnlyList<SystemMultiToolItemViewModel> Tools { get; private set; } = [];
    [ObservableProperty] public partial string ToolsLabel { get; private set; } = "";

    public string Hint => "Shops with a trader and a multi-tool cabinet (the game labels them \"Minor Settlement\"), worked out from the game's own building rules, with the coordinates your Analysis Visor shows there. Add a marker and the game leads you there next time you load.";

    /// <param name="savedBody">Where the save loads in this system (0 = space or the station), or null when it was
    /// saved elsewhere - then the station's list, which is what a player sees after warping in.</param>
    public async Task LoadAsync(ulong planetAddress, double latitude, double longitude, bool fromYou, int? savedBody)
    {
        if (_planetAddress != planetAddress)
        {
            Shops.Clear();
            HasShops = false;
        }
        _planetAddress = planetAddress;
        var sites = await BuildingSiteService.NearAsync(planetAddress, BuildingClassShop, latitude, longitude, ShownShops);
        int body = savedBody ?? 0;
        var pool = await CabinetPoolService.ForAsync(planetAddress, body);
        if (_planetAddress != planetAddress) return;

        var rows = sites.Select(s => new ShopSiteRowViewModel(s, fromYou ? "away" : "from the portal")).ToArray();
        ObservableCollectionSync.Apply(Shops, rows, row => row.Key, (current, next) => current.Distance == next.Distance);
        Heading = fromYou ? "SHOPS NEAR YOU" : "SHOPS NEAR THE PORTAL";
        HasShops = rows.Length > 0;

        string where = body == 0 ? "at the space station" : $"on {UniverseInfo.PlanetName(planetAddress & ~(0xFUL << 52), body)}";
        ToolsLabel = (savedBody is null ? "After warping in, each of these shops sells one of these multi-tools."
                : $"Your save loads {where}, so each of these shops sells one of these multi-tools.")
            + " Which one a shop has, and its class, depends on where its cabinet stands. Reloading somewhere else in the system changes the list (see Current System).";
        string toolsKey = string.Join(",", pool.Select(t => t.Seed));
        if (toolsKey != _toolsKey)
        {
            _toolsKey = toolsKey;
            var tools = pool.Select(t => new SystemMultiToolItemViewModel(t)).ToArray();
            Tools = tools;
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            foreach (var tool in tools) _ = tool.ResolveKindAsync(dispatcher);
        }
    }

    public void RefreshMarkers()
    {
        foreach (var shop in Shops) shop.Refresh();
    }
}
