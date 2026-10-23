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
/// A native multi-tool in the system multi-tool pool view.
/// </summary>
public sealed partial class SystemMultiToolItemViewModel : ObservableObject
{
    private readonly Action<SystemMultiToolItemViewModel> _onCopied;

    public SystemMultiToolItemViewModel(SystemMultiTool tool, Action<SystemMultiToolItemViewModel> onCopied)
    {
        Tool = tool;
        _onCopied = onCopied;
        Kind = tool.IsRoyal ? "Exotic" : "Standard";
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

    public string KindLabel => Kind.ToUpperInvariant();

    public string AutomationName => $"Copy seed for {Tool.DefaultName} ({Tool.SeedHex})";
    public string TooltipText => $"Copy seed {Tool.SeedHex} to clipboard";

    [ObservableProperty]
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
    /// Resolves the multi-tool kind (Alien, Experimental, Rifle, Pistol) from its part tree.
    /// </summary>
    public async Task ResolveKindAsync()
    {
        if (Tool.IsRoyal) return;
        try
        {
            var detected = await MultiToolKinds.KindAsync(Seed);
            if (!string.IsNullOrEmpty(detected))
            {
                Kind = detected;
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
/// and each planet in the current system, with source filtering and 1-click seed copying.
/// </summary>
public sealed partial class SystemMultiToolsViewModel : ObservableObject
{
    private const string CollapsedKey = "SystemMultiToolsCollapsed";

    private List<SystemMultiToolItemViewModel> _allTools = new();
    private ulong? _currentSystem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    public partial bool IsOpen { get; set; } = SettingsStore.Get(SettingsStore.Scope.Profile, CollapsedKey) != "1";

    [ObservableProperty] public partial string Heading { get; private set; } = "SYSTEM MULTI-TOOLS";
    [ObservableProperty] public partial string Subtitle { get; private set; } = "";
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
        IReadOnlyList<(ulong PlanetSeed, int PlanetIndex, string Name)> planets)
    {
        if (_currentSystem == systemAddress && _allTools.Count > 0)
            return;

        _currentSystem = systemAddress;
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var tools = await Task.Run(() => SystemMultiToolGenerator.GenerateSystem(
            portalCode,
            planets,
            k => words.GetValueOrDefault(k)));

        var items = new List<SystemMultiToolItemViewModel>(tools.Count);
        foreach (var t in tools)
        {
            items.Add(new SystemMultiToolItemViewModel(t, OnToolCopied));
        }

        _allTools = items;
        HasTools = items.Count > 0;
        Heading = $"SYSTEM MULTI-TOOLS ({items.Count})";

        int stationCount = items.Count(t => t.Tool.IsStationPool);
        int planetPoolCount = planets.Count;
        Subtitle = $"Space Station ({stationCount}) · {planetPoolCount} Planet Pool{(planetPoolCount == 1 ? "" : "s")}";

        UpdateFilterOptions();
        ApplyFilter();

        // Asynchronously resolve kinds from part trees
        _ = Task.Run(async () =>
        {
            foreach (var item in items)
            {
                await item.ResolveKindAsync();
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

