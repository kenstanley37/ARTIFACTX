using ArtifactX.Core.Collections;
using ArtifactX.WinUI3.Services;
using System.Collections.Generic;
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

    public ShopSiteRowViewModel(BuildingSiteLocation site, string fromLabel, string toolsLabel,
        IReadOnlyList<SystemMultiToolItemViewModel> tools)
    {
        _site = site;
        Distance = $"{site.DistanceKm:0.0} km {fromLabel}";
        ToolsLabel = toolsLabel;
        Tools = tools;
        Refresh();
    }

    public ulong Key => _site.Key;
    /// <summary>The game's own name for the building; the coordinates tell shops apart (the visor shows them too).</summary>
    public string Title => "Minor Settlement";
    public string Coordinates => _site.Coordinates;
    public string Distance { get; }
    public string ToolsLabel { get; }
    public IReadOnlyList<SystemMultiToolItemViewModel> Tools { get; }
    public bool HasTools => Tools.Count > 0;
    /// <summary>Visible content, for keeping unchanged cards when the list refreshes.</summary>
    public string DisplayKey => $"{Distance}|{ToolsLabel}|{string.Join(",", Tools.Select(t => t.Name))}";
    public string AddMarkerName => $"Add a marker for the Minor Settlement at {Coordinates} to my save";
    public string UndoMarkerName => $"Undo the marker for the Minor Settlement at {Coordinates}";

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

    public string Hint => "Shops (Minor Settlements with a multi-tool cabinet and a trader), worked out from the game's own building rules, with the coordinates your Analysis Visor shows there. Add a marker and the game leads you there next time you load. Every cabinet in the system sells from the list of where your game was loaded (see Current System); which of those multi-tools a shop has, and its class, depends on where its cabinet stands inside the building, which isn't worked out yet.";

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
        string where = body == 0 ? "at the space station" : $"on {UniverseInfo.PlanetName(planetAddress & ~(0xFUL << 52), body)}";
        string toolsLabel = savedBody is null ? "After warping in, sells one of these:"
            : $"Your save loads {where}, so it sells one of these:";
        var rows = sites.Select(s => new ShopSiteRowViewModel(s, fromYou ? "away" : "from the portal", toolsLabel,
            pool.Select(t => new SystemMultiToolItemViewModel(t)).ToArray())).ToArray();
        ObservableCollectionSync.Apply(Shops, rows, row => row.Key, (current, next) => current.DisplayKey == next.DisplayKey);
        foreach (var row in Shops) foreach (var tool in row.Tools) _ = tool.ResolveKindAsync(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
        Heading = fromYou ? "SHOPS NEAR YOU" : "SHOPS NEAR THE PORTAL";
        HasShops = rows.Length > 0;
    }

    public void RefreshMarkers()
    {
        foreach (var shop in Shops) shop.Refresh();
    }
}
