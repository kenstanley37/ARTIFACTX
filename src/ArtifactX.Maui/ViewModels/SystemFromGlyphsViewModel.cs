using System.Collections.ObjectModel;
using ArtifactX.Core.Theming;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

public sealed partial class SystemFromGlyphsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    private int _galaxyNumber;

    /// <summary>The galaxy of the system on screen (1-256), 0 before one loads.</summary>
    public int GalaxyNumber => _galaxyNumber;
    private IReadOnlyList<SystemShipItemViewModel> _allShips = [];

    [ObservableProperty]
    public partial string SystemName { get; set; } = "";

    [ObservableProperty]
    public partial string RegionAndGalaxySubtitle { get; set; } = "";

    [ObservableProperty]
    public partial string PortalCode { get; set; } = "";

    [ObservableProperty]
    public partial string CoordinatesAndDistanceText { get; set; } = "";

    [ObservableProperty]
    public partial bool RequiresPurpleStarAccess { get; set; }

    [ObservableProperty]
    public partial string StarPill { get; set; } = "";

    [ObservableProperty]
    public partial string RacePill { get; set; } = "";

    [ObservableProperty]
    public partial string EconomyWealthPill { get; set; } = "";

    [ObservableProperty]
    public partial string ConflictPill { get; set; } = "";


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlanetsTabSelected), nameof(IsShipsTabSelected), nameof(IsMultiToolsTabSelected))]
    public partial string SelectedTab { get; set; } = "Planets";

    public bool IsPlanetsTabSelected => SelectedTab == "Planets";
    public bool IsShipsTabSelected => SelectedTab == "Ships";
    public bool IsMultiToolsTabSelected => SelectedTab == "MultiTools";

    [ObservableProperty]
    public partial string PlanetsTabTitle { get; set; } = "Planets";

    [ObservableProperty]
    public partial string ShipsTabTitle { get; set; } = "Ships";

    [ObservableProperty]
    public partial string MultiToolsTabTitle { get; set; } = "Multi-tools";

    [ObservableProperty]
    public partial string ShipRulesSummary { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedShipFilterKey { get; set; } = "All";

    [ObservableProperty]
    public partial string MultiToolOddsSummary { get; set; } = "";

    /// <summary>Why a system's cabinets has no tools to show, or a note on what is shown.</summary>
    [ObservableProperty]
    public partial string MultiToolNote { get; set; } = "";

    [ObservableProperty]
    public partial bool HasMultiToolPlaces { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MultiToolPlaceViewModel> MultiToolPlaces { get; set; } = [];

    /// <summary>How rolling a cabinet works, numbered (Core SystemCabinets.RollSteps, the desktop's words).</summary>
    public IReadOnlyList<string> MultiToolSteps { get; } = SystemCabinets.RollSteps.Select((step, i) => $"{i + 1}. {step}").ToArray();

    [ObservableProperty]
    public partial string CopyBannerText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasCopyBanner { get; set; }

    public ObservableCollection<SystemBodyItemViewModel> Bodies { get; } = [];
    public ObservableCollection<ShipFilterPillViewModel> ShipFilterOptions { get; } = [];
    public ObservableCollection<SystemShipItemViewModel> FilteredShips { get; } = [];

    public async Task LoadSystemAsync(string portalCode, int galaxyNumber)
    {
        if (PortalCode == portalCode && _galaxyNumber == galaxyNumber) return;
        IsLoading = true;
        try
        {
            var readout = await Task.Run(() => PortalSystemReadout.Generate(portalCode, galaxyNumber));
            _galaxyNumber = galaxyNumber;
            RecentSystemsService.Record(readout);

            SystemName = readout.SystemName;
            RegionAndGalaxySubtitle = $"{readout.RegionName} · {readout.GalaxyName}";
            PortalCode = readout.PortalCode;
            // The glyphs below are the address; the galactic coordinates are what a signal booster takes.
            CoordinatesAndDistanceText = $"{readout.GalacticCoordinates} · {readout.DistanceToCoreLightYears:N0} ly from core";
            RequiresPurpleStarAccess = readout.RequiresPurpleStarAccess;

            StarPill = readout.StarPill;
            RacePill = readout.RacePill;
            EconomyWealthPill = readout.EconomyWealthPill;
            ConflictPill = readout.ConflictPill;

            Bodies.Clear();
            foreach (var b in readout.Bodies)
                Bodies.Add(new SystemBodyItemViewModel(b, galaxyNumber));

            ShipRulesSummary = readout.ShipRulesSummary;
            _allShips = readout.Ships.Select(s => new SystemShipItemViewModel(s)).ToArray();
            SelectedShipFilterKey = "All";
            RebuildShipFilters();
            ApplyShipFilter();

            MultiToolOddsSummary = "";
            MultiToolNote = "Working out the cabinets...";
            MultiToolPlaces = [];
            HasMultiToolPlaces = false;

            PlanetsTabTitle = $"Planets ({Bodies.Count})";
            ShipsTabTitle = $"Ships ({_allShips.Count})";
            MultiToolsTabTitle = "Multi-tools";
            _ = LoadProfilesAsync(readout);
            _ = LoadCabinetsAsync(readout);
        }
        finally { IsLoading = false; }
    }

    private static Task<MultiToolKindClassifier?>? _kinds;

    /// <summary>What the cabinets sell for every place the game can be loaded at (Core SystemCabinets), with the
    /// system's class chances.</summary>
    private async Task LoadCabinetsAsync(PortalSystemReadout readout)
    {
        var rules = await GameRulesService.GetAsync();
        if (rules is not { Stations: { } stations, Inventory: { } inventory, MultiToolModel: { } model })
        {
            MultiToolNote = "Multi-tools need the game rules, which this copy of the app doesn't have.";
            return;
        }
        // The model's part tree is parsed once for the whole app (it names a tool's kind: Rifle, Pistol, Alien...).
        _kinds ??= Task.Run(() => (MultiToolKindClassifier?)new MultiToolKindClassifier(model));
        var kinds = await _kinds;
        if (kinds is null) return;
        var names = readout.Bodies.OrderBy(b => b.BodyIndex).Select(b => b.Name).ToArray();
        var places = await Task.Run(() => SystemCabinets.ForSystem(readout.PackedAddress, names, stations, inventory, kinds, rules.Words));
        if (PortalCode != readout.PortalCode || _galaxyNumber != readout.GalaxyNumber) return;
        var attrs = readout.Attributes;
        if (places.Count == 0)
        {
            MultiToolOddsSummary = "";
            MultiToolNote = attrs.Uncharted || attrs.Abandoned
                ? "This system has no working multi-tool cabinets." : "The cabinets of this system can't be worked out.";
            return;
        }
        var (c, b, a, s) = EquipmentGeneration.GetClassProbabilities(attrs.Wealth, attrs.Pirate, inventory);
        MultiToolOddsSummary = $"Cabinet class chances here: S {s:0}% · A {a:0}% · B {b:0}% · C {c:0}%";
        MultiToolNote = places.All(p => p.Station.Count == 0)
            ? "The station's cabinets can't be worked out for this station; the planet shop lists below still apply."
            : "";
        MultiToolPlaces = places.Select(p => new MultiToolPlaceViewModel(p)).ToArray();
        HasMultiToolPlaces = true;
    }

    /// <summary>Each body's weather, sentinels, life and resources (Core SystemProfiles, the desktop's generator) from the
    /// rules file, worked out after the system is on screen; bodies stay without them when the rules are missing.</summary>
    private async Task LoadProfilesAsync(PortalSystemReadout readout)
    {
        if (await GameRulesService.GetAsync() is not { Biomes: { } biomes, Profiles: { } tables } rules) return;
        var (portal, galaxy) = SystemProfiles.PortalOf(readout.PackedAddress);
        var result = await Task.Run(() =>
        {
            try { return (Profiles: SystemProfiles.Generate(readout.PackedAddress, biomes, tables), Types: PlanetGenerator.Generate(portal, galaxy, biomes)); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemDetails] profiles: {ex}");
                return (Profiles: Array.Empty<PlanetProfile?>(), Types: Array.Empty<PlanetType>());
            }
        });
        if (PortalCode != readout.PortalCode || _galaxyNumber != readout.GalaxyNumber) return;   // another system opened meanwhile
        foreach (var body in Bodies)
        {
            int i = body.Body.BodyIndex - 1;
            if (i >= 0 && i < result.Profiles.Length && result.Profiles[i] is { } profile)
                body.ApplyProfile(profile, i < result.Types.Length ? result.Types[i] : null, rules);
        }
    }

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab)) return;
        SelectedTab = tab;
    }

    [RelayCommand]
    private void SelectShipFilter(ShipFilterPillViewModel? pill)
    {
        if (pill is null) return;
        SelectedShipFilterKey = pill.Key;
        foreach (var item in ShipFilterOptions)
            item.IsSelected = item.Key == pill.Key;
        ApplyShipFilter();
    }

    [RelayCommand]
    private async Task CopyPortalCodeAsync()
    {
        await Clipboard.Default.SetTextAsync(PortalCode);
        await ShowTemporaryBannerAsync($"Copied portal code {PortalCode} to clipboard.");
    }

    [RelayCommand]
    private async Task CopyBodyPortalCodeAsync(SystemBodyItemViewModel? body)
    {
        if (body is null) return;
        await Clipboard.Default.SetTextAsync(body.BodyPortalCode);
        body.IsCopied = true;
        await ShowTemporaryBannerAsync($"Copied {body.Name} portal code ({body.BodyPortalCode}).");
        body.IsCopied = false;
    }

    [RelayCommand]
    private async Task CopyShipSeedAsync(SystemShipItemViewModel? ship)
    {
        if (ship is null) return;
        await Clipboard.Default.SetTextAsync(ship.SeedHex);
        ship.IsCopied = true;
        await ShowTemporaryBannerAsync($"Copied seed {ship.SeedHex} ({ship.Name}).");
        ship.IsCopied = false;
    }

    [RelayCommand]
    private static async Task GoBackAsync()
    {
        if (Shell.Current is null) return;
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
            await Shell.Current.GoToAsync("..");
        else
            await Shell.Current.GoToAsync("//systems");
    }

    private void RebuildShipFilters()
    {
        ShipFilterOptions.Clear();
        ShipFilterOptions.Add(new ShipFilterPillViewModel("All", "All", _allShips.Count, SelectedShipFilterKey == "All"));

        void AddIfAny(string key, string label, SystemStarshipType type)
        {
            int count = _allShips.Count(s => s.Ship.Type == type);
            if (count > 0)
                ShipFilterOptions.Add(new ShipFilterPillViewModel(key, label, count, SelectedShipFilterKey == key));
        }

        AddIfAny("Exotic", "Exotic", SystemStarshipType.Exotic);
        AddIfAny("SentinelInterceptor", "Sentinel", SystemStarshipType.SentinelInterceptor);
        AddIfAny("Fighter", "Fighters", SystemStarshipType.Fighter);
        AddIfAny("Hauler", "Haulers", SystemStarshipType.Hauler);
        AddIfAny("Explorer", "Explorers", SystemStarshipType.Explorer);
        AddIfAny("Solar", "Solars", SystemStarshipType.Solar);
        AddIfAny("Shuttle", "Shuttles", SystemStarshipType.Shuttle);
        AddIfAny("Freighter", "Freighters", SystemStarshipType.Freighter);
    }

    private void ApplyShipFilter()
    {
        FilteredShips.Clear();
        IEnumerable<SystemShipItemViewModel> source = _allShips;
        if (SelectedShipFilterKey != "All" && Enum.TryParse<SystemStarshipType>(SelectedShipFilterKey, out var filterType))
        {
            source = _allShips.Where(s => s.Ship.Type == filterType);
        }

        foreach (var item in source)
            FilteredShips.Add(item);
    }

    private async Task ShowTemporaryBannerAsync(string text)
    {
        CopyBannerText = text;
        HasCopyBanner = true;
        await Task.Delay(1800);
        if (CopyBannerText == text)
            HasCopyBanner = false;
    }
}
