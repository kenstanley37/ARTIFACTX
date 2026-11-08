using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Helpers;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

public sealed partial class CabinetMultiToolsPageViewModel : ObservableObject
{
    private ulong? _currentSystem;
    private PlanetSnapshot? _snapshot;

    [ObservableProperty] public partial string SystemHeading { get; private set; } = "CABINET MULTI-TOOLS";
    [ObservableProperty] public partial string SystemLocation { get; private set; } = "";
    [ObservableProperty] public partial string PortalCode { get; private set; } = "";
    [ObservableProperty] public partial string Summary { get; private set; } = "";
    [ObservableProperty] public partial bool IsLoading { get; private set; }

    public StationMultiToolsViewModel StationTools { get; } = new();
    public SystemMultiToolsViewModel MultiTools { get; } = new();
    public PlanetShopsViewModel Shops { get; } = new();

    public ObservableCollection<CrashedShipPlanetChoice> PlanetChoices { get; } = [];
    [ObservableProperty] public partial CrashedShipPlanetChoice? SelectedPlanet { get; set; }

    partial void OnSelectedPlanetChanged(CrashedShipPlanetChoice? value)
    {
        if (value is not null && _snapshot is not null)
            _ = LoadPlanetShopsAsync(value, _snapshot);
    }

    public async Task InitializeAsync(ulong? targetPlanetAddress = null)
    {
        IsLoading = true;
        try
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
            SystemHeading = $"CABINET MULTI-TOOLS IN {systemName.ToUpperInvariant()}";
            SystemLocation = $"{systemName} · {UniverseInfo.Region(system.Value)} · {PlanetText.GalaxyLabel(galaxy)}";
            PortalCode = NmsSpaceStationPaths.PortalAddress(system.Value, 0);

            var facts = UniverseInfo.For(system.Value);
            string station = $"{systemName} Station";

            var planets = snapshot.Discoveries is not null ? NmsPlanets.ReadAll(snapshot.Discoveries).Where(p => p.SystemAddress == system.Value).ToList() : new List<KnownPlanet>();
            var known = planets.Select(p => p.PlanetIndex).ToHashSet();
            if (!PlanetTypesService.IsReady) _ = PlanetTypesService.EnsureLoadedAsync();
            var allBodies = planets.Concat(PlanetTypesService.Undiscovered(system.Value, known)).OrderBy(p => p.PlanetIndex).ToList();

            int? savedBody = CabinetPoolService.SavedLoadBody(system.Value, snapshot.EquipmentSaveInputs);

            _ = StationTools.UpdateAsync(system.Value, snapshot);
            _ = MultiTools.LoadAsync(system.Value, facts, station.Length > 0 ? station : "the space station",
                allBodies.Select(p => (p.PlanetIndex, UniverseInfo.PlanetName(system.Value, p.PlanetIndex))).ToList(), savedBody);

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
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPlanetShopsAsync(CrashedShipPlanetChoice choice, PlanetSnapshot snapshot)
    {
        bool here = choice.IsHere && snapshot.Coordinates is not null;
        (double Lat, double Lon) from = here ? snapshot.Coordinates!.Value : (0.0, 0.0);

        if (!here)
        {
            var portal = await PortalLocationService.GetAsync(choice.Address);
            if (portal is not null) from = (portal.Latitude, portal.Longitude);
        }

        int? savedBody = CabinetPoolService.SavedLoadBody(_currentSystem ?? 0, snapshot.EquipmentSaveInputs);
        await Shops.LoadAsync(choice.Address, from.Lat, from.Lon, here, savedBody, snapshot);
    }

    public void RefreshMarkers()
    {
        Shops.RefreshMarkers();
    }
}

