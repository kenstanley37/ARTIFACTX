using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// A starship in the system starship pool view, with its deterministic S-Class base stats,
/// max S-Class slot capacity, and spawn chance in this system.
/// </summary>
public sealed partial class SystemStarshipItemViewModel : ObservableObject
{
    private readonly Action<SystemStarshipItemViewModel> _onCopied;

    public SystemStarshipItemViewModel(
        SystemStarship ship,
        ShipSClassStats stats,
        string spawnChanceSummary,
        Action<SystemStarshipItemViewModel> onCopied)
    {
        Ship = ship;
        Stats = stats;
        SpawnChanceSummary = spawnChanceSummary;
        _onCopied = onCopied;
    }

    public SystemStarship Ship { get; }
    public ShipSClassStats Stats { get; }
    public int Index => Ship.Index;
    public string IndexLabel => $"#{Index:D2}";
    public string DefaultName => Ship.DefaultName;
    public string TypeLabel => Ship.TypeLabel.ToUpperInvariant();
    public string SeedHex => Ship.SeedHex;
    public SystemStarshipType Type => Ship.Type;

    public string SClassStatsSummary => $"S-Class Base: {Stats.CompactStatsSummary}";
    public string SClassSlotsSummary => Stats.SlotsSummary;
    public string SpawnChanceSummary { get; }

    public string AutomationName => $"Copy seed for {Ship.DefaultName} ({Ship.SeedHex})";
    public string TooltipText => $"Copy seed {Ship.SeedHex} to clipboard";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonText))]
    public partial bool IsCopied { get; set; }

    public string CopyButtonText => IsCopied ? "Copied!" : "Copy";

    public Brush BadgeBackground => Type switch
    {
        SystemStarshipType.Exotic => ThemeResources.Brush("ThemeDoneBrush"),
        SystemStarshipType.SentinelInterceptor => ThemeResources.Brush("ThemeWarningBrush"),
        SystemStarshipType.Solar => ThemeResources.Brush("ThemeWarningBrush"),
        SystemStarshipType.Fighter => ThemeResources.Brush("ThemeAccentBrush"),
        SystemStarshipType.Hauler => ThemeResources.Brush("ThemeAccentAltBrush"),
        SystemStarshipType.Explorer => ThemeResources.Brush("ThemePanelBrush"),
        _ => ThemeResources.Brush("ThemeLineBrush"),
    };

    public Brush BadgeForeground => Type switch
    {
        SystemStarshipType.Exotic => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.SentinelInterceptor => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.Solar => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.Fighter => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.Hauler => ThemeResources.Brush("ThemeBandTextBrush"),
        _ => ThemeResources.Brush("ThemeTextBrush"),
    };

    public void CopySeed()
    {
        var package = new DataPackage();
        package.SetText(SeedHex);
        Clipboard.SetContent(package);
        _onCopied(this);
    }
}

/// <summary>
/// Filter option pill for starships.
/// </summary>
public sealed record StarshipFilterOption(string Label, string FilterKey, int Count, bool IsSelected);

/// <summary>
/// System Starships panel ViewModel: displays the native starships of the current system
/// (21 standard models, plus the system's Sentinel Interceptor when a dissonant planet is present),
/// with S-Class spawn probabilities, S-Class base stats, max slot capacities, and 1-click seed copying.
/// </summary>
public sealed partial class SystemStarshipsViewModel : ObservableObject
{
    private const string CollapsedKey = "SystemStarshipsCollapsed";

    private List<SystemStarshipItemViewModel> _allShips = new();
    private ulong? _currentSystem;
    private bool _lastCorrupted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    public partial bool IsOpen { get; set; } = SettingsStore.Get(SettingsStore.Scope.Profile, CollapsedKey) != "1";

    [ObservableProperty] public partial string Heading { get; private set; } = "POSSIBLE SYSTEM STARSHIPS";
    [ObservableProperty] public partial string Subtitle { get; private set; } = "";
    [ObservableProperty] public partial string SpawnRulesSummary { get; private set; } = "";
    [ObservableProperty] public partial bool HasShips { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<SystemStarshipItemViewModel> FilteredShips { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<StarshipFilterOption> FilterOptions { get; private set; } = [];
    [ObservableProperty] public partial string SelectedFilter { get; set; } = "All";

    public string ToggleLabel => IsOpen ? "Hide ships" : $"Show ships ({_allShips.Count})";

    public void Toggle()
    {
        IsOpen = !IsOpen;
        SettingsStore.Set(SettingsStore.Scope.Profile, CollapsedKey, IsOpen ? "0" : "1");
    }

    /// <summary>
    /// Loads the starships for the system address and attributes, computing S-Class base stats and spawn probabilities.
    /// </summary>
    public async Task LoadAsync(
        ulong systemAddress,
        ulong portalCode,
        SystemFacts facts,
        bool hasCorruptedPlanet)
    {
        if (_currentSystem == systemAddress && _lastCorrupted == hasCorruptedPlanet && _allShips.Count > 0)
            return;

        _currentSystem = systemAddress;
        _lastCorrupted = hasCorruptedPlanet;

        var words = await CatalogService.GetProceduralNameWordsAsync();
        var invTables = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();

        var ships = await Task.Run(() => SystemStarshipGenerator.Generate(
            portalCode,
            facts.Race,
            facts.Pirate,
            k => words.GetValueOrDefault(k),
            includeFleet: false,
            includeSentinel: hasCorruptedPlanet));

        // Put Exotic (#21) and Sentinel Interceptor (#36) at the top so the most sought-after S-Class ships are immediately visible
        var ordered = ships
            .OrderByDescending(s => s.Type == SystemStarshipType.Exotic)
            .ThenByDescending(s => s.Type == SystemStarshipType.SentinelInterceptor)
            .ThenBy(s => s.Index)
            .ToList();

        var items = new List<SystemStarshipItemViewModel>(ordered.Count);
        foreach (var s in ordered)
        {
            var stats = EquipmentGeneration.CalculateShipSClassStats(s.Type, s.Seed, invTables);
            string spawnChance = EquipmentGeneration.FormatShipSpawnChance(
                s.Type, facts.Wealth, facts.Pirate, facts.Uncharted, facts.Abandoned, invTables);
            items.Add(new SystemStarshipItemViewModel(s, stats, spawnChance, OnShipCopied));
        }

        _allShips = items;
        HasShips = items.Count > 0;
        Heading = $"POSSIBLE SYSTEM STARSHIPS ({items.Count})";
        SpawnRulesSummary = EquipmentGeneration.FormatSystemShipRules(
            facts.Wealth, facts.WealthLabel, facts.Pirate, facts.Uncharted, facts.Abandoned, hasCorruptedPlanet, invTables);

        int exoticCount = items.Count(s => s.Type == SystemStarshipType.Exotic);
        int sentinelCount = items.Count(s => s.Type == SystemStarshipType.SentinelInterceptor);
        int fighterCount = items.Count(s => s.Type == SystemStarshipType.Fighter);
        int haulerCount = items.Count(s => s.Type == SystemStarshipType.Hauler);
        int explorerCount = items.Count(s => s.Type == SystemStarshipType.Explorer);
        int shuttleCount = items.Count(s => s.Type == SystemStarshipType.Shuttle);
        int solarCount = items.Count(s => s.Type == SystemStarshipType.Solar);

        var parts = new List<string>();
        if (exoticCount > 0) parts.Add($"{exoticCount} Exotic (100% S-Class)");
        if (sentinelCount > 0) parts.Add($"{sentinelCount} Sentinel Interceptor");
        if (fighterCount > 0) parts.Add($"{fighterCount} Fighter{(fighterCount == 1 ? "" : "s")}");
        if (haulerCount > 0) parts.Add($"{haulerCount} Hauler{(haulerCount == 1 ? "" : "s")}");
        if (explorerCount > 0) parts.Add($"{explorerCount} Explorer{(explorerCount == 1 ? "" : "s")}");
        if (shuttleCount > 0) parts.Add($"{shuttleCount} Shuttle{(shuttleCount == 1 ? "" : "s")}");
        if (solarCount > 0) parts.Add($"{solarCount} Solar{(solarCount == 1 ? "" : "s")}");

        Subtitle = string.Join(" · ", parts);
        UpdateFilterOptions();
        ApplyFilter();
        OnPropertyChanged(nameof(ToggleLabel));
    }

    public void SelectFilter(string filterKey)
    {
        if (SelectedFilter == filterKey) return;
        SelectedFilter = filterKey;
        UpdateFilterOptions();
        ApplyFilter();
    }

    private void UpdateFilterOptions()
    {
        var options = new List<StarshipFilterOption>
        {
            new("All", "All", _allShips.Count, SelectedFilter == "All")
        };

        void AddOption(string label, string key, SystemStarshipType type)
        {
            int c = _allShips.Count(s => s.Type == type);
            if (c > 0)
                options.Add(new(label, key, c, SelectedFilter == key));
        }

        AddOption("Exotic", "Exotic", SystemStarshipType.Exotic);
        AddOption("Sentinel", "SentinelInterceptor", SystemStarshipType.SentinelInterceptor);
        AddOption("Fighters", "Fighter", SystemStarshipType.Fighter);
        AddOption("Haulers", "Hauler", SystemStarshipType.Hauler);
        AddOption("Explorers", "Explorer", SystemStarshipType.Explorer);
        AddOption("Shuttles", "Shuttle", SystemStarshipType.Shuttle);
        AddOption("Solars", "Solar", SystemStarshipType.Solar);

        FilterOptions = options;
    }

    private void ApplyFilter()
    {
        if (SelectedFilter == "All")
        {
            FilteredShips = _allShips;
        }
        else if (Enum.TryParse<SystemStarshipType>(SelectedFilter, out var targetType))
        {
            FilteredShips = _allShips.Where(s => s.Type == targetType).ToList();
        }
        else
        {
            FilteredShips = _allShips;
        }
    }

    private async void OnShipCopied(SystemStarshipItemViewModel item)
    {
        item.IsCopied = true;
        await Task.Delay(1500);
        item.IsCopied = false;
    }
}
