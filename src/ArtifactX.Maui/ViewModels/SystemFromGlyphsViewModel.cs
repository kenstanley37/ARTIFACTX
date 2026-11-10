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
    public partial IReadOnlyList<PortalGlyphVector> GlyphVectors { get; set; } = [];

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

    [ObservableProperty]
    public partial string MultiToolExplanation { get; set; } = "";

    [ObservableProperty]
    public partial bool HasMultiToolPools { get; set; }

    [ObservableProperty]
    public partial string CopyBannerText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasCopyBanner { get; set; }

    public ObservableCollection<SystemBodyItemViewModel> Bodies { get; } = [];
    public ObservableCollection<ShipFilterPillViewModel> ShipFilterOptions { get; } = [];
    public ObservableCollection<SystemShipItemViewModel> FilteredShips { get; } = [];
    public ObservableCollection<GeneratedMultiToolLoadPool> MultiToolPools { get; } = [];

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
            CoordinatesAndDistanceText = $"{readout.PortalCode} · {readout.GalacticCoordinates} · {readout.DistanceToCoreLightYears:N0} ly from core";
            RequiresPurpleStarAccess = readout.RequiresPurpleStarAccess;

            StarPill = readout.StarPill;
            RacePill = readout.RacePill;
            EconomyWealthPill = readout.EconomyWealthPill;
            ConflictPill = readout.ConflictPill;
            GlyphVectors = readout.GlyphVectors;

            Bodies.Clear();
            foreach (var b in readout.Bodies)
                Bodies.Add(new SystemBodyItemViewModel(b));

            ShipRulesSummary = readout.ShipRulesSummary;
            _allShips = readout.Ships.Select(s => new SystemShipItemViewModel(s)).ToArray();
            SelectedShipFilterKey = "All";
            RebuildShipFilters();
            ApplyShipFilter();

            MultiToolOddsSummary = readout.MultiToolOddsSummary;
            MultiToolExplanation = readout.MultiToolExplanation;
            MultiToolPools.Clear();
            foreach (var pool in readout.MultiToolPools)
                MultiToolPools.Add(pool);
            HasMultiToolPools = MultiToolPools.Count > 0;

            PlanetsTabTitle = $"Planets ({Bodies.Count})";
            ShipsTabTitle = $"Ships ({_allShips.Count})";
            int totalTools = readout.MultiToolPools.Sum(p => p.Tools.Count);
            MultiToolsTabTitle = totalTools > 0 ? $"Multi-tools ({totalTools})" : "Multi-tools";
        }
        finally { IsLoading = false; }
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
    private async Task CopyMultiToolSeedAsync(GeneratedSystemMultiTool? tool)
    {
        if (tool is null) return;
        await Clipboard.Default.SetTextAsync(tool.SeedHex);
        await ShowTemporaryBannerAsync($"Copied Multi-Tool seed {tool.SeedHex} ({tool.Name}).");
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
