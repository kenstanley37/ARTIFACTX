using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>
/// The Trade Finder (issues #9 and #20): pick an item, see which space stations around you sell it and for how much,
/// worked out with the game's own rules (TradeFinderService). Follows the game: a new location searches again around
/// it. Read-only.
/// </summary>
public sealed partial class TradeFinderViewModel : ObservableObject
{
    private const int MaxRows = 200;

    private IReadOnlyList<TradeFinderStation> _found = [];
    private JObject? _saveInputs;
    private ulong? _here;
    private (ulong Start, int Radius)? _searchedFor;
    private CancellationTokenSource? _cancel;

    public TradeFinderViewModel() => Item.Changed += Apply;

    /// <summary>The item to buy: everything some station around you sells.</summary>
    public ChoiceFilter Item { get; } = new("Item to buy", 340);

    [ObservableProperty] public partial int AreaIndex { get; set; }   // 0 your region, 1 + the regions around it
    [ObservableProperty] public partial int SortIndex { get; set; }   // 0 cheapest, 1 nearest

    [ObservableProperty] public partial IReadOnlyList<TradeFinderRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string ResultCount { get; private set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasStatus))] public partial string Status { get; private set; } = "";

    public bool HasStatus => Status.Length > 0;

    partial void OnAreaIndexChanged(int value) => _ = SearchAsync();
    partial void OnSortIndexChanged(int value) => Apply();

    public async Task LoadAsync()
    {
        _searchedFor = null;
        await ShowAsync(await LiveSaveWatcher.GetLatestAsync());
    }

    /// <summary>A save the game wrote: a new location searches again around it.</summary>
    public async Task ShowAsync(PlanetSnapshot snapshot)
    {
        _saveInputs = snapshot.EquipmentSaveInputs;
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        if (here == _here && _searchedFor is not null) return;
        _here = here;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _cancel?.Cancel();
        if (_here is not ulong start)
        {
            Status = "Load a save to search around where you are.";
            return;
        }
        int radius = AreaIndex;
        if (_searchedFor == (start, radius)) return;
        var cancel = _cancel = new CancellationTokenSource();
        if (await TradeFinderService.StationsSupportedAsync(_saveInputs) is false)
        {
            Status = "What the stations sell can't be worked out for this save yet (an expedition, or space stations with the older layout). Switch to your main save to search.";
            _found = [];
            Apply();
            return;
        }
        Status = radius == 0 ? "Searching the space stations in your region..." : "Searching the space stations around you (a few seconds)...";
        var progress = new Progress<int>(n => { if (!cancel.IsCancellationRequested) Status = $"Searching space stations near you: {n:N0} systems done..."; });
        try
        {
            var found = await TradeFinderService.FindAsync(start, radius, _saveInputs, progress, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            await CatalogService.WarmCacheAsync(found.SelectMany(f => f.Stock).Select(s => s.Id).Distinct());
            if (cancel.IsCancellationRequested) return;
            _found = found;
            _searchedFor = (start, radius);
            Status = found.Count == 0 ? "Rebuild your game data (Settings) to search what stations sell." : "";
            FillChoices();
            Apply();
        }
        catch (OperationCanceledException) { }
    }

    private static string NameOf(string id) => CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } name ? name : "";

    private void FillChoices() =>
        Item.Refill("Pick an item", _found.SelectMany(f => f.Stock.Select(s => s.Id).Distinct())
            .GroupBy(id => id).Select(g => (Id: g.Key, Name: NameOf(g.Key), Count: g.Count()))
            .Where(x => x.Name.Length > 0)
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .Select(x => new ChoiceItem($"{x.Name} ({x.Count:N0} station{(x.Count == 1 ? "" : "s")})", x.Id)));

    private void Apply()
    {
        if (!Item.IsSet)
        {
            Results = [];
            ResultCount = _found.Count == 0 ? "" : $"{_found.Count:N0} space stations searched";
            return;
        }
        string id = Item.Value;
        string name = NameOf(id);
        var icon = CatalogService.TryGet(id)?.Icon;
        var offers = _found
            .Select(station => (Station: station, Offer: station.Stock.FirstOrDefault(s => s.Id == id)))
            .Where(x => x.Offer is not null)
            .Select(x => new TradeFinderRowViewModel(x.Station, x.Offer!, name, icon, _here,
                _here is ulong h ? NmsPlanets.DistanceLightYears(h, x.Station.SystemAddress) : null))
            .ToList();
        var ordered = SortIndex == 1
            ? offers.OrderBy(r => r.Distance ?? double.MaxValue).ThenBy(r => r.Price)
            : offers.OrderBy(r => r.Price).ThenBy(r => r.Distance ?? double.MaxValue);
        Results = ordered.Take(MaxRows).ToList();
        ResultCount = offers.Count > MaxRows ? $"{(SortIndex == 1 ? "Nearest" : "Cheapest")} {MaxRows:N0} of {offers.Count:N0} stations selling {name}"
            : $"{offers.Count:N0} station{(offers.Count == 1 ? "" : "s")} selling {name}";
    }
}
