using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item a place sells: icon, name, "1,200 units · 14 in stock · +3.2%".</summary>
public sealed record MarketCard(ImageSource? Icon, string Name, string Details)
{
    public string Tooltip => $"{Name}\n{Details}";
}

/// <summary>Everything one place (the station, or a trading post) sells.</summary>
public sealed record MarketPlace(string Heading, bool ShowHeading, IReadOnlyList<MarketCard> Items);

/// <summary>One place's price for an item in the comparison table.</summary>
public sealed record MarketCell(string Price, string Details, bool Cheapest, bool Sold)
{
    public Windows.UI.Text.FontWeight PriceWeight => Cheapest ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    /// <summary>The cheapest price in the same green as the parts counts; "not sold" dimmed.</summary>
    public Brush PriceBrush => Cheapest ? ThemeResources.LevelBrush(0) : ThemeResources.Brush(Sold ? "ThemeTextBrush" : "ThemeTextDimBrush");
}

public sealed record MarketCompareRow(ImageSource? Icon, string Name, IReadOnlyList<MarketCell> Cells);

/// <summary>
/// What a system's station and trading posts sell, read off their trade terminals
/// (SystemMarketService). Each place keeps its own cards; the table compares only what
/// more than one place sells - a station and a trading post mostly stock different goods,
/// and a full table was mostly "not sold" cells (2026-10-18). Collapsible, remembered.
/// </summary>
public sealed partial class StationMarketViewModel : ObservableObject
{
    private const string CollapsedKey = "PlanetsMarketCollapsed";
    private ulong _system;

    [ObservableProperty] public partial string Heading { get; private set; } = "SOLD AT THE STATION";
    [ObservableProperty] public partial string Seen { get; private set; } = "";
    [ObservableProperty] public partial bool IsEmpty { get; private set; } = true;
    [ObservableProperty] public partial IReadOnlyList<MarketPlace> Places { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<string> ComparePlaces { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<MarketCompareRow> CompareRows { get; private set; } = [];
    [ObservableProperty] public partial string CompareHeading { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(ToggleLabel))]
    public partial bool IsCollapsed { get; private set; } = SettingsStore.Get(SettingsStore.Scope.Profile, CollapsedKey) == "1";

    public bool IsOpen => !IsCollapsed;
    public string ToggleLabel => IsCollapsed ? "Show" : "Hide";
    public bool HasCompare => CompareRows.Count > 0;

    partial void OnCompareRowsChanged(IReadOnlyList<MarketCompareRow> value) => OnPropertyChanged(nameof(HasCompare));

    public void Toggle()
    {
        IsCollapsed = !IsCollapsed;
        SettingsStore.Set(SettingsStore.Scope.Profile, CollapsedKey, IsCollapsed ? "1" : "0");
    }

    public void Show(ulong system)
    {
        _system = system;
        var items = SystemMarketService.For(system);
        IsEmpty = items.Count == 0;
        Seen = items.Count == 0 ? "" : $"{items.Count} item{(items.Count == 1 ? "" : "s")} · last seen {PlanetText.SeenAgo(items.Max(i => i.SeenAt) ?? "")}";
        var placeList = items.Select(i => i.Place).Distinct().OrderBy(p => p == "station" ? "" : p, StringComparer.Ordinal).ToList();
        // "Sold at the station" only when the station is all there is; a trading post makes it the market.
        Heading = placeList.All(p => p == "station") ? "SOLD AT THE STATION" : "MARKET";
        bool headings = placeList.Count > 1 || placeList.Any(p => p != "station");

        Places = items.GroupBy(i => i.Place).Select(g => new MarketPlace(
            (g.Key == "station" ? "Space station" : PlanetText.PlaceName(system, g.Key).Replace("Trading post · ", "Trading post on ")).ToUpperInvariant(),
            headings,
            g.Select(item => new MarketCard(
                CatalogService.TryGet(item.ItemId)?.Icon,
                item.Name,
                // Stock before the change: a long word ("Economy Data Unavailable") then only cuts its own end.
                string.Join(" · ", new[] { $"{item.Price:N0} units", item.Stock > 0 ? $"{item.Stock} in stock" : "", item.Change }.Where(s => s.Length > 0))))
            .ToList())).ToList();

        var shared = items.GroupBy(i => i.ItemId).Where(g => g.Select(i => i.Place).Distinct().Count() > 1).ToList();
        CompareHeading = placeList.Count == 2 ? "SOLD AT BOTH" : "SOLD IN MORE THAN ONE PLACE";
        ComparePlaces = placeList.Select(p => PlanetText.PlaceName(system, p).ToUpperInvariant()).ToList();
        CompareRows = shared.OrderBy(g => g.First().Name, StringComparer.CurrentCulture).Select(group =>
        {
            int cheapest = group.Min(i => i.Price);
            bool single = group.Count(i => i.Price == cheapest) == 1;
            return new MarketCompareRow(CatalogService.TryGet(group.Key)?.Icon, group.First().Name, placeList.Select(place =>
            {
                var at = group.FirstOrDefault(i => i.Place == place);
                if (at is null) return new MarketCell("not sold", "", false, false);
                bool best = single && at.Price == cheapest;
                return new MarketCell($"{at.Price:N0} units" + (best ? "  · cheapest" : ""),
                    string.Join(" · ", new[] { at.Change, at.Stock > 0 ? $"{at.Stock} in stock" : "" }.Where(s => s.Length > 0)), best, true);
            }).ToList());
        }).ToList();

        _ = WarmIconsAsync(system, items);
    }

    private async Task WarmIconsAsync(ulong system, IReadOnlyList<MarketItem> items)
    {
        var missing = items.Where(i => CatalogService.TryGet(i.ItemId)?.Icon is null).Select(i => i.ItemId).ToList();
        if (missing.Count == 0) return;
        await CatalogService.WarmCacheAsync(missing);
        if (system == _system && missing.Any(id => CatalogService.TryGet(id)?.Icon is not null)) Show(system);
    }
}
