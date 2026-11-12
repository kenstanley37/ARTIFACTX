using ArtifactX.Core.Collections;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Helpers;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

public sealed record CrashedShipPlanetChoice(int Index, string Name, ulong Address, bool IsHere)
{
    public override string ToString() => IsHere ? $"{Name} (You are here)" : Name;
}

/// <summary>
/// Dedicated ViewModel for searching, filtering, and marking predicted crashed starships across
/// planets in the current system (offloading spatial calculations from Planet Details).
/// </summary>
public sealed partial class CrashedShipsPageViewModel : ObservableObject
{
    private ulong? _currentSystem;
    private PlanetSnapshot? _snapshot;
    private bool _rebuildingTypes;
    private string _wantedType = "";

    [ObservableProperty] public partial string SystemHeading { get; private set; } = "CRASHED SHIPS";
    [ObservableProperty] public partial string SystemLocation { get; private set; } = "";
    [ObservableProperty] public partial string PortalCode { get; private set; } = "";
    [ObservableProperty] public partial string Summary { get; private set; } = "";
    [ObservableProperty] public partial bool IsLoading { get; private set; }
    [ObservableProperty] public partial bool HasShips { get; private set; }

    public ObservableCollection<CrashedShipPlanetChoice> PlanetChoices { get; } = [];
    [ObservableProperty] public partial CrashedShipPlanetChoice? SelectedPlanet { get; set; }

    public IReadOnlyList<string> ShipClassChoices { get; } = (string[])["Any class", "S-class", "A-class", "B-class", "C-class"];
    [ObservableProperty] public partial int ShipClassChoice { get; set; } = 1; // Default S-class

    public IReadOnlyList<string> ShipCountChoices { get; } = (string[])["5", "10", "25", "50", "100", "250", "All"];
    private static readonly int[] ShipCounts = [5, 10, 25, 50, 100, 250, int.MaxValue];
    [ObservableProperty] public partial int ShipCountChoice { get; set; } = 1; // Default 10
    private int ShownShips => ShipCounts[Math.Clamp(ShipCountChoice, 0, ShipCounts.Length - 1)];

    [ObservableProperty] public partial IReadOnlyList<CrashedShipTypeOption> ShipTypeOptions { get; private set; } = [];
    [ObservableProperty] public partial CrashedShipTypeOption? SelectedShipType { get; set; }

    public ObservableCollection<CrashedShipRowViewModel> CrashedShips { get; } = [];

    partial void OnSelectedPlanetChanged(CrashedShipPlanetChoice? value)
    {
        if (value is not null && _snapshot is not null)
            _ = ReloadForPlanetAsync(value, _snapshot);
    }

    partial void OnShipClassChoiceChanged(int value)
    {
        if (value >= 0 && SelectedPlanet is not null && _snapshot is not null)
            _ = ReloadForPlanetAsync(SelectedPlanet, _snapshot);
    }

    partial void OnSelectedShipTypeChanged(CrashedShipTypeOption? value)
    {
        if (_rebuildingTypes || value is null) return;
        _wantedType = value.Type?.ToString() ?? "";
        if (SelectedPlanet is not null && _snapshot is not null)
            _ = LoadShipsAsync(SelectedPlanet, _snapshot);
    }

    partial void OnShipCountChoiceChanged(int value)
    {
        if (value >= 0 && SelectedPlanet is not null && _snapshot is not null)
            _ = LoadShipsAsync(SelectedPlanet, _snapshot);
    }

    public async Task InitializeAsync(ulong? targetPlanetAddress = null)
    {
        var snapshot = await LiveSaveWatcher.GetLatestAsync();
        _snapshot = snapshot;
        ulong? system = NmsSpaceStationPaths.ReadSystemAddress(snapshot.Universe);
        _currentSystem = system;
        if (system is null || system == 0)
        {
            Summary = "No active solar system found in the current save.";
            return;
        }

        string systemName = UniverseInfo.SystemName(system.Value);
        int galaxy = NmsSpaceStationPaths.GalaxyNumber(system.Value);
        SystemHeading = $"CRASHED SHIPS IN {systemName.ToUpperInvariant()}";
        SystemLocation = $"{systemName} · {UniverseInfo.Region(system.Value)} · {PlanetText.GalaxyLabel(galaxy)}";
        PortalCode = NmsSpaceStationPaths.PortalAddress(system.Value, 0);

        var planets = snapshot.Discoveries is not null ? NmsPlanets.ReadAll(snapshot.Discoveries).Where(p => p.SystemAddress == system.Value).ToList() : new List<KnownPlanet>();
        var known = planets.Select(p => p.PlanetIndex).ToHashSet();
        if (!PlanetTypesService.IsReady) _ = PlanetTypesService.EnsureLoadedAsync();
        var allBodies = planets.Concat(PlanetTypesService.Undiscovered(system.Value, known)).OrderBy(p => p.PlanetIndex).ToList();

        int currentPlanet = (int?)snapshot.Universe?["oZw"]?["jsv"] ?? 0;

        PlanetChoices.Clear();
        CrashedShipPlanetChoice? preferred = null;

        foreach (var body in allBodies)
        {
            string name = UniverseInfo.PlanetName(system.Value, body.PlanetIndex);
            bool isHere = body.PlanetIndex == currentPlanet && currentPlanet > 0;
            var choice = new CrashedShipPlanetChoice(body.PlanetIndex, name, body.Address, isHere);
            PlanetChoices.Add(choice);

            if (targetPlanetAddress is not null && body.Address == targetPlanetAddress.Value)
                preferred = choice;
            else if (preferred is null && isHere)
                preferred = choice;
        }

        SelectedPlanet = preferred ?? PlanetChoices.FirstOrDefault();
    }

    private bool ClassMatches(CrashedShipSite site) => ShipClassChoice <= 0 || site.ShownClass == 4 - ShipClassChoice;
    private bool Matches(CrashedShipSite site) => ClassMatches(site) && (SelectedShipType?.Type is not { } type || site.Ship?.Type == type);

    private async Task ReloadForPlanetAsync(CrashedShipPlanetChoice planetChoice, PlanetSnapshot snapshot)
    {
        IsLoading = true;
        try
        {
            var counts = await CrashedShipService.CountAsync(planetChoice.Address, ClassMatches, s => s.Ship?.Type.ToString() ?? "");
            var options = new List<CrashedShipTypeOption> { new($"Any type ({counts.Sum(c => c.Count):N0})", null) };
            foreach (var (group, count) in counts)
            {
                if (Enum.TryParse<SystemStarshipType>(group, out var type))
                    options.Add(new($"{(type == SystemStarshipType.SentinelInterceptor ? "Sentinel" : group)} ({count:N0})", type));
            }

            _rebuildingTypes = true;
            ShipTypeOptions = options;
            SelectedShipType = options.FirstOrDefault(o => o.Type?.ToString() == _wantedType) ?? options[0];
            _rebuildingTypes = false;

            await LoadShipsAsync(planetChoice, snapshot);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadShipsAsync(CrashedShipPlanetChoice planetChoice, PlanetSnapshot snapshot)
    {
        bool here = planetChoice.IsHere && snapshot.Coordinates is not null;
        (double Lat, double Lon) from = here ? snapshot.Coordinates!.Value : (0.0, 0.0);

        if (!here)
        {
            var portal = await PortalLocationService.GetAsync(planetChoice.Address);
            if (portal is not null) from = (portal.Latitude, portal.Longitude);
        }

        var found = await CrashedShipService.SearchAsync(planetChoice.Address, Matches, from.Lat, from.Lon, ShownShips);
        if (found is null)
        {
            CrashedShips.Clear();
            HasShips = false;
            Summary = $"No crashed ships found on {planetChoice.Name}.";
            return;
        }

        var sites = found.Nearest;
        var cards = sites.Select((s, i) => new CrashedShipRowViewModel(s, i + 1, here, here ? "away" : "from portal")).ToArray();
        ObservableCollectionSync.Apply(CrashedShips, cards, row => row.Key, (_, _) => true, (row, next) => row.UpdateFrom(next));

        HasShips = CrashedShips.Count > 0;
        Summary = found.Total == 0 ? $"No {(ShipClassChoice > 0 ? ShipClassChoices[ShipClassChoice] + " " : "")}crashed ships matching filters on {planetChoice.Name}."
            : found.Total <= ShownShips ? $"All {found.Total:N0} matching crashed ships on {planetChoice.Name}, nearest first."
            : $"Showing nearest {ShownShips} of {found.Total:N0} matching crashed ships on {planetChoice.Name}.";

        if (found.Uncharted)
            Summary += " In uncharted systems, only underwater wrecks are claimable.";
    }

    public void RefreshMarkers()
    {
        foreach (var ship in CrashedShips)
            ship.Refresh();
    }
}

