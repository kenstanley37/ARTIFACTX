using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
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
/// The Multi-Tool Finder (issue #60, multi-tools): the multi-tools the space station cabinets sell in the systems
/// around you, visited or not, by kind and class, and where to load the game for each. Follows the game: a new
/// location searches again around it.
/// </summary>
public sealed partial class MultiToolFinderViewModel : ObservableObject
{
    private const int MaxRows = 300;

    private IReadOnlyList<CabinetMultiToolFind> _found = [];
    private JObject? _saveInputs;
    private ulong? _here;
    private (ulong Start, int Radius)? _searchedFor;
    private CancellationTokenSource? _cancel;
    private bool _quiet;

    public MultiToolFinderViewModel() => Kind.Changed += Apply;

    /// <summary>Alien, Experimental, Rifle, Pistol, Starter...</summary>
    public ChoiceFilter Kind { get; } = new("Kind", 200);

    [ObservableProperty] public partial int AreaIndex { get; set; }   // 0 your region, 1 + the regions around it
    [ObservableProperty] public partial int ClassIndex { get; set; }  // 0 any, 1 B or better, 2 A or better, 3 S only
    [ObservableProperty] public partial int SortIndex { get; set; }   // 0 best class, 1 nearest
    [ObservableProperty] public partial bool ArrivalOnly { get; set; }

    [ObservableProperty] public partial IReadOnlyList<MultiToolFinderRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string ResultCount { get; private set; } = "";
    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial int ActiveFilters { get; private set; }

    public bool HasStatus => Status.Length > 0;
    public bool HasActiveFilters => ActiveFilters > 0;
    public string ClearLabel => $"Clear filters ({ActiveFilters})";

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnActiveFiltersChanged(int value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ClearLabel));
    }

    partial void OnAreaIndexChanged(int value) => _ = SearchAsync();
    partial void OnClassIndexChanged(int value) => Apply();
    partial void OnSortIndexChanged(int value) => Apply();
    partial void OnArrivalOnlyChanged(bool value) => Apply();

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
        if (await MultiToolFinderService.StationsSupportedAsync(_saveInputs) is false)
        {
            Status = "The station cabinets can't be worked out for this save yet (an expedition, or space stations with the older layout). Switch to your main save to search.";
            _found = [];
            Apply();
            return;
        }
        Status = radius == 0 ? "Searching the star systems in your region..." : "Searching the star systems around you (a few seconds)...";
        var progress = new Progress<int>(n => { if (!cancel.IsCancellationRequested) Status = $"Searching star systems near you: {n:N0} done..."; });
        try
        {
            var found = await MultiToolFinderService.FindAsync(start, radius, _saveInputs, progress, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            _found = found;
            _searchedFor = (start, radius);
            Status = found.Count == 0 ? "Update your game data (Settings › Game data) to search for multi-tools." : "";
            FillChoices();
            Apply();
        }
        catch (OperationCanceledException) { }
    }

    private void FillChoices() =>
        Kind.Refill("Any kind", _found.GroupBy(f => f.Kind).OrderBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => new ChoiceItem($"{g.Key} ({g.Count():N0})", g.Key)));

    public void ClearFilters()
    {
        _quiet = true;
        Kind.Reset();
        ClassIndex = 0;
        ArrivalOnly = false;
        _quiet = false;
        Apply();
    }

    private void Apply()
    {
        if (_quiet) return;
        var filtered = _found
            .Where(f => f.ClassIndex >= ClassIndex)
            .Where(f => !Kind.IsSet || f.Kind == Kind.Value)
            .Where(f => !ArrivalOnly || f.ReloadBody == 0)
            // The same tool in the same cabinet once (two cabinets can draw the same pool member).
            .GroupBy(f => (f.SystemAddress, f.ReloadBody, f.Cabinet, f.Tool.Seed)).Select(g => g.First())
            .Select(f => (Find: f, Distance: _here is ulong h ? NmsPlanets.DistanceLightYears(h, f.SystemAddress) : null))
            .ToList();
        var ordered = SortIndex == 1
            ? filtered.OrderBy(x => x.Distance ?? double.MaxValue).ThenByDescending(x => x.Find.ClassIndex)
            : filtered.OrderByDescending(x => x.Find.ClassIndex).ThenBy(x => x.Distance ?? double.MaxValue);
        Results = ordered.ThenBy(x => x.Find.ReloadBody).Take(MaxRows)
            .Select(x => new MultiToolFinderRowViewModel(x.Find, _here, x.Distance)).ToList();
        ResultCount = filtered.Count > MaxRows ? $"Best {MaxRows:N0} of {filtered.Count:N0} offers" : $"{filtered.Count:N0} of {_found.Count:N0} offers";
        ActiveFilters = new[] { Kind.IsSet, ClassIndex > 0, ArrivalOnly }.Count(on => on);
    }
}
