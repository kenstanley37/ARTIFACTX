using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// A native multi-tool in the system or planet multi-tool pool view, with its deterministic
/// S-Class base stats, max tech slots, and instructions on where/how to find it.
/// </summary>
public sealed partial class SystemMultiToolItemViewModel : ObservableObject
{
    private readonly Action<SystemMultiToolItemViewModel> _onCopied;
    private readonly InventoryGenerationTables? _invTables;
    private readonly bool _isUncharted;
    private readonly bool _isAbandoned;
    private readonly bool _isPirate;

    public SystemMultiToolItemViewModel(
        SystemMultiTool tool,
        InventoryGenerationTables? invTables,
        bool isUncharted,
        bool isAbandoned,
        bool isPirate,
        Action<SystemMultiToolItemViewModel> onCopied,
        string? customLocationGuide = null)
    {
        Tool = tool;
        _invTables = invTables;
        _isUncharted = isUncharted;
        _isAbandoned = isAbandoned;
        _isPirate = isPirate;
        _onCopied = onCopied;
        Kind = tool.IsRoyal ? "Exotic" : "Standard";
        Stats = EquipmentGeneration.CalculateMultiToolSClassStats(Kind, tool.Seed, _invTables);
        HowToFindSummary = customLocationGuide ?? EquipmentGeneration.FormatMultiToolLocationGuide(
            Kind, tool.IsRoyal, tool.IsStationPool, tool.SourceLabel, _isUncharted, _isAbandoned, _isPirate);
    }

    public SystemMultiTool Tool { get; }
    public int Index => Tool.Index;
    public string IndexLabel => $"#{Index:D2}";
    public string DefaultName => Tool.DefaultName;
    public string SourceLabel => Tool.SourceLabel;
    public string SeedHex => Tool.SeedHex;
    public ulong Seed => Tool.Seed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeBackground))]
    [NotifyPropertyChangedFor(nameof(BadgeForeground))]
    [NotifyPropertyChangedFor(nameof(KindLabel))]
    public partial string Kind { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SClassStatsSummary))]
    public partial MultiToolSClassStats Stats { get; set; }

    [ObservableProperty]
    public partial string HowToFindSummary { get; set; }

    public string KindLabel => Kind.ToUpperInvariant();

    public string SClassStatsSummary =>
        $"S-Class Base: {Stats.CompactStatsSummary} · {Stats.MaxTechSlots} Tech Slots";

    public string AutomationName => $"Copy seed for {Tool.DefaultName} ({Tool.SeedHex})";
    public string TooltipText => $"Copy seed {Tool.SeedHex} to clipboard";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonText))]
    public partial bool IsCopied { get; set; }

    public string CopyButtonText => IsCopied ? "Copied!" : "Copy";

    public Brush BadgeBackground => Kind switch
    {
        "Alien" => ThemeResources.Brush("ThemeWarningBrush"),
        "Experimental" => ThemeResources.Brush("ThemeDoneBrush"),
        "Exotic" => ThemeResources.Brush("ThemeAccentBrush"),
        "Rifle" => ThemeResources.Brush("ThemeAccentAltBrush"),
        "Pistol" => ThemeResources.Brush("ThemePanelBrush"),
        _ => ThemeResources.Brush("ThemeLineBrush"),
    };

    public Brush BadgeForeground => Kind switch
    {
        "Alien" => ThemeResources.Brush("ThemeOnAccentBrush"),
        "Experimental" => ThemeResources.Brush("ThemeOnAccentBrush"),
        "Exotic" => ThemeResources.Brush("ThemeOnAccentBrush"),
        "Rifle" => ThemeResources.Brush("ThemeBandTextBrush"),
        _ => ThemeResources.Brush("ThemeTextBrush"),
    };

    public void CopySeed()
    {
        var package = new DataPackage();
        package.SetText(SeedHex);
        Clipboard.SetContent(package);
        _onCopied(this);
    }

    /// <summary>
    /// Resolves the multi-tool kind (Alien, Experimental, Rifle, Pistol) from its part tree
    /// and updates the S-Class base stats for that weapon class.
    /// </summary>
    public async Task ResolveKindAsync(DispatcherQueue? dispatcher = null, string? customLocationGuide = null)
    {
        if (Tool.IsRoyal) return;
        try
        {
            var detected = await MultiToolKinds.KindAsync(Seed);
            if (!string.IsNullOrEmpty(detected))
            {
                var newStats = EquipmentGeneration.CalculateMultiToolSClassStats(detected, Seed, _invTables);
                var newGuide = customLocationGuide ?? EquipmentGeneration.FormatMultiToolLocationGuide(
                    detected, Tool.IsRoyal, Tool.IsStationPool, Tool.SourceLabel, _isUncharted, _isAbandoned, _isPirate);

                if (dispatcher is not null)
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        Kind = detected;
                        Stats = newStats;
                        HowToFindSummary = newGuide;
                    });
                }
                else
                {
                    Kind = detected;
                    Stats = newStats;
                    HowToFindSummary = newGuide;
                }
            }
        }
        catch
        {
            // Fallback to standard
        }
    }
}

/// <summary>
/// Filter option pill for multi-tools.
/// </summary>
public sealed record MultiToolFilterOption(string Label, string FilterKey, int Count, bool IsSelected);

/// <summary>
/// System Multi-Tools panel ViewModel: displays native multi-tool pools for the Space Station
/// and each planet in the current system, with S-Class base stats, cabinet rules, source filtering,
/// and 1-click seed copying.
/// </summary>
public sealed partial class SystemMultiToolsViewModel : ObservableObject
{
    private const string CollapsedKey = "SystemMultiToolsCollapsed";

    private List<SystemMultiToolItemViewModel> _allTools = new();
    private ulong? _currentSystem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    public partial bool IsOpen { get; set; } = SettingsStore.Get(SettingsStore.Scope.Profile, CollapsedKey) != "1";

    [ObservableProperty] public partial string Heading { get; private set; } = "POSSIBLE SYSTEM MULTI-TOOLS";
    [ObservableProperty] public partial string Subtitle { get; private set; } = "";
    [ObservableProperty] public partial string PoolRulesSummary { get; private set; } = "";
    [ObservableProperty] public partial bool HasTools { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<SystemMultiToolItemViewModel> FilteredTools { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<MultiToolFilterOption> FilterOptions { get; private set; } = [];
    [ObservableProperty] public partial string SelectedFilter { get; set; } = "All";

    public string ToggleLabel => IsOpen ? "Hide multi-tools" : $"Show multi-tools ({_allTools.Count})";

    public void Toggle()
    {
        IsOpen = !IsOpen;
        SettingsStore.Set(SettingsStore.Scope.Profile, CollapsedKey, IsOpen ? "0" : "1");
    }

    /// <summary>
    /// Loads the multi-tool pools for the system and its planets.
    /// </summary>
    public async Task LoadAsync(
        ulong systemAddress,
        ulong portalCode,
        SystemFacts facts,
        IReadOnlyList<(ulong PlanetSeed, int PlanetIndex, string Name)> planets)
    {
        if (_currentSystem == systemAddress && _allTools.Count > 0)
            return;

        _currentSystem = systemAddress;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var invTables = PlanetTypesService.InventoryTables ?? await CatalogService.GetInventoryGenerationTablesAsync();

        bool hasStationCabinet = !facts.Uncharted && !facts.Abandoned;
        var tools = await Task.Run(() => SystemMultiToolGenerator.GenerateSystem(
            portalCode,
            planets,
            k => words.GetValueOrDefault(k),
            hasSpaceStationCabinet: hasStationCabinet));

        var items = new List<SystemMultiToolItemViewModel>(tools.Count);
        foreach (var t in tools)
        {
            items.Add(new SystemMultiToolItemViewModel(
                t, invTables, facts.Uncharted, facts.Abandoned, facts.Pirate, OnToolCopied));
        }

        _allTools = items;
        HasTools = items.Count > 0;
        Heading = $"POSSIBLE SYSTEM MULTI-TOOLS ({items.Count})";

        int stationCount = items.Count(t => t.Tool.IsStationPool);
        int planetPoolCount = planets.Count;
        Subtitle = hasStationCabinet
            ? $"Space Station ({stationCount}) · {planetPoolCount} Planet Pool{(planetPoolCount == 1 ? "" : "s")}"
            : $"No Space Station Cabinets · {planetPoolCount} Planet Pool{(planetPoolCount == 1 ? "" : "s")}";

        if (facts.Uncharted)
        {
            PoolRulesSummary =
                "Uncharted system — no Space Station, Minor Settlements, or Sentinel Pillars spawn here. " +
                "These procedural seeds belong to this system's planets, but cannot be claimed from cabinets in this system.";
        }
        else if (facts.Abandoned)
        {
            PoolRulesSummary =
                "Abandoned system — the Space Station and Minor Settlements have no working Multi-Tool cabinets. " +
                "Only Exotic (Royal) Multi-Tools can be claimed at Sentinel Pillars (use a Sentinel Boundary Map on non-dead, non-anomaly worlds).";
        }
        else
        {
            var (_, _, a, s) = EquipmentGeneration.GetClassProbabilities(facts.Wealth, facts.Pirate, invTables);
            string sChance = s <= 0f
                ? $"0% natural S-Class cabinets in 1★ Low Economy ({a:0}% A-Class; upgradeable with Nanites)"
                : $"{s:0}% S-Class cabinet chance ({a:0}% A-Class)";
            PoolRulesSummary =
                $"Cabinet class odds: {sChance}. Saving and reloading on a planet (or the Space Station) activates that body's 4-tool pool across all cabinets in the system. " +
                "Standard/Alien/Experimental tools (#01–#03) spawn in Minor Settlement & Space Station cabinets; Exotic/Royal tools (#04) ONLY spawn at Sentinel Pillars.";
        }

        UpdateFilterOptions();
        ApplyFilter();
        OnPropertyChanged(nameof(ToggleLabel));

        // Asynchronously resolve kinds from part trees and update S-Class stats
        _ = Task.Run(async () =>
        {
            foreach (var item in items)
            {
                await item.ResolveKindAsync(dispatcher);
            }
        });
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
        var options = new List<MultiToolFilterOption>
        {
            new("All", "All", _allTools.Count, SelectedFilter == "All")
        };

        var sources = _allTools.Select(t => t.SourceLabel).Distinct().ToList();
        foreach (var src in sources)
        {
            int c = _allTools.Count(t => t.SourceLabel == src);
            options.Add(new(src, src, c, SelectedFilter == src));
        }

        FilterOptions = options;
    }

    private void ApplyFilter()
    {
        if (SelectedFilter == "All")
        {
            FilteredTools = _allTools;
        }
        else
        {
            FilteredTools = _allTools.Where(t => t.SourceLabel == SelectedFilter).ToList();
        }
    }

    private async void OnToolCopied(SystemMultiToolItemViewModel item)
    {
        item.IsCopied = true;
        await Task.Delay(1500);
        item.IsCopied = false;
    }
}
