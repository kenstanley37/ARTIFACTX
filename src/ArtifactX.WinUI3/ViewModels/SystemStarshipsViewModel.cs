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
/// A starship or fleet vehicle in the system starship pool view.
/// </summary>
public sealed partial class SystemStarshipItemViewModel : ObservableObject
{
    private readonly Action<SystemStarshipItemViewModel> _onCopied;

    public SystemStarshipItemViewModel(SystemStarship ship, Action<SystemStarshipItemViewModel> onCopied)
    {
        Ship = ship;
        _onCopied = onCopied;
    }

    public SystemStarship Ship { get; }
    public int Index => Ship.Index;
    public string IndexLabel => $"#{Index:D2}";
    public string DefaultName => Ship.DefaultName;
    public string TypeLabel => Ship.TypeLabel.ToUpperInvariant();
    public string SeedHex => Ship.SeedHex;
    public SystemStarshipType Type => Ship.Type;

    public string AutomationName => $"Copy seed for {Ship.DefaultName} ({Ship.SeedHex})";
    public string TooltipText => $"Copy seed {Ship.SeedHex} to clipboard";

    [ObservableProperty]
    public partial bool IsCopied { get; set; }

    public string CopyButtonText => IsCopied ? "Copied!" : "Copy";

    public Brush BadgeBackground => Type switch
    {
        SystemStarshipType.Exotic => ThemeResources.Brush("ThemeDoneBrush"),
        SystemStarshipType.Solar => ThemeResources.Brush("ThemeWarningBrush"),
        SystemStarshipType.Fighter => ThemeResources.Brush("ThemeAccentBrush"),
        SystemStarshipType.Hauler => ThemeResources.Brush("ThemeAccentAltBrush"),
        SystemStarshipType.Explorer => ThemeResources.Brush("ThemePanelBrush"),
        SystemStarshipType.SentinelInterceptor => ThemeResources.Brush("ThemeAccentAltBrush"),
        SystemStarshipType.PirateDreadnought => ThemeResources.Brush("ThemeDamagedBrush"),
        SystemStarshipType.Freighter => ThemeResources.Brush("ThemeLineBrush"),
        _ => ThemeResources.Brush("ThemeLineBrush"),
    };

    public Brush BadgeForeground => Type switch
    {
        SystemStarshipType.Exotic => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.Solar => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.Fighter => ThemeResources.Brush("ThemeOnAccentBrush"),
        SystemStarshipType.Hauler => ThemeResources.Brush("ThemeBandTextBrush"),
        SystemStarshipType.SentinelInterceptor => ThemeResources.Brush("ThemeBandTextBrush"),
        SystemStarshipType.PirateDreadnought => ThemeResources.Brush("ThemeBandTextBrush"),
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
/// Filter option pill for starships and fleet vehicles.
/// </summary>
public sealed record StarshipFilterOption(string Label, string FilterKey, int Count, bool IsSelected);

/// <summary>
/// System Starships panel ViewModel: displays the native starships and fleet vehicles of the current system,
/// with class breakdown pills, filtering, and 1-click seed copying.
/// </summary>
public sealed partial class SystemStarshipsViewModel : ObservableObject
{
    private const string CollapsedKey = "SystemStarshipsCollapsed";

    private List<SystemStarshipItemViewModel> _allShips = new();
    private ulong? _currentSystem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    public partial bool IsOpen { get; set; } = SettingsStore.Get(SettingsStore.Scope.Profile, CollapsedKey) != "1";

    [ObservableProperty] public partial string Heading { get; private set; } = "SYSTEM STARSHIPS & FLEET";
    [ObservableProperty] public partial string Subtitle { get; private set; } = "";
    [ObservableProperty] public partial bool HasShips { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<SystemStarshipItemViewModel> FilteredShips { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<StarshipFilterOption> FilterOptions { get; private set; } = [];
    [ObservableProperty] public partial string SelectedFilter { get; set; } = "All";

    public string ToggleLabel => IsOpen ? "Hide ships" : $"Show fleet & ships ({_allShips.Count})";

    public void Toggle()
    {
        IsOpen = !IsOpen;
        SettingsStore.Set(SettingsStore.Scope.Profile, CollapsedKey, IsOpen ? "0" : "1");
    }

    /// <summary>
    /// Loads the starships and fleet vehicles for the system address and attributes.
    /// </summary>
    public async Task LoadAsync(ulong systemAddress, ulong portalCode, SystemRace race, bool isPirate)
    {
        if (_currentSystem == systemAddress && _allShips.Count > 0)
            return;

        _currentSystem = systemAddress;
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var ships = await Task.Run(() => SystemStarshipGenerator.Generate(portalCode, race, isPirate, k => words.GetValueOrDefault(k)));

        var items = new List<SystemStarshipItemViewModel>(ships.Count);
        foreach (var s in ships)
        {
            items.Add(new SystemStarshipItemViewModel(s, OnShipCopied));
        }

        _allShips = items;
        HasShips = items.Count > 0;
        Heading = $"SYSTEM STARSHIPS & FLEET ({items.Count})";

        int standardCount = items.Count(s => s.Ship.IsStandardStarship);
        int sentinelCount = items.Count(s => s.Type == SystemStarshipType.SentinelInterceptor);
        int freighterCount = items.Count(s => s.Type == SystemStarshipType.Freighter);
        int dreadnoughtCount = items.Count(s => s.Type == SystemStarshipType.PirateDreadnought);

        var parts = new List<string>();
        if (standardCount > 0) parts.Add($"{standardCount} Native Starships");
        if (sentinelCount > 0) parts.Add("Sentinel Interceptor");
        if (freighterCount > 0) parts.Add($"{freighterCount} Freighters");
        if (dreadnoughtCount > 0) parts.Add("Pirate Dreadnought");

        Subtitle = string.Join(" · ", parts);
        UpdateFilterOptions();
        ApplyFilter();
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

        int starshipsCount = _allShips.Count(s => s.Ship.IsStandardStarship);
        if (starshipsCount > 0 && starshipsCount < _allShips.Count)
        {
            options.Add(new("Starships", "Starships", starshipsCount, SelectedFilter == "Starships"));
        }

        void AddOption(string label, string key, SystemStarshipType type)
        {
            int c = _allShips.Count(s => s.Type == type);
            if (c > 0)
                options.Add(new(label, key, c, SelectedFilter == key));
        }

        AddOption("Exotic", "Exotic", SystemStarshipType.Exotic);
        AddOption("Fighters", "Fighter", SystemStarshipType.Fighter);
        AddOption("Haulers", "Hauler", SystemStarshipType.Hauler);
        AddOption("Explorers", "Explorer", SystemStarshipType.Explorer);
        AddOption("Shuttles", "Shuttle", SystemStarshipType.Shuttle);
        AddOption("Solars", "Solar", SystemStarshipType.Solar);
        AddOption("Sentinel", "Sentinel", SystemStarshipType.SentinelInterceptor);
        AddOption("Freighters", "Freighter", SystemStarshipType.Freighter);
        AddOption("Dreadnought", "Dreadnought", SystemStarshipType.PirateDreadnought);

        FilterOptions = options;
    }

    private void ApplyFilter()
    {
        if (SelectedFilter == "All")
        {
            FilteredShips = _allShips;
        }
        else if (SelectedFilter == "Starships")
        {
            FilteredShips = _allShips.Where(s => s.Ship.IsStandardStarship).ToList();
        }
        else if (SelectedFilter == "Sentinel")
        {
            FilteredShips = _allShips.Where(s => s.Type == SystemStarshipType.SentinelInterceptor).ToList();
        }
        else if (SelectedFilter == "Dreadnought")
        {
            FilteredShips = _allShips.Where(s => s.Type == SystemStarshipType.PirateDreadnought).ToList();
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
