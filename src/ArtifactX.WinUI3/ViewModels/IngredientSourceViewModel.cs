using ArtifactX.WinUI3.Helpers.Recipes;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// "Where to get it" for one raw ingredient (IngredientSourcePanel): the kinds of planet whose wild plants or objects
/// give it, then the closest systems that have it, searched from where the save is (IngredientLocatorService). A newer
/// ingredient cancels an older search; the list fills in place when the search ends.
/// </summary>
public sealed partial class IngredientSourceViewModel : ObservableObject
{
    private CancellationTokenSource? _search;
    private (string Item, ulong? Start)? _shown;

    public ObservableCollection<string> Places { get; } = [];
    public ObservableCollection<IngredientPlace> Closest { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlacesVisibility))] public partial bool HasPlaces { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusVisibility))] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial bool IsSearching { get; set; }
    public Visibility PlacesVisibility => HasPlaces ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StatusVisibility => Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Shows <paramref name="itemId"/>, searching around <paramref name="start"/> (the save's system; null
    /// without a save). Showing the same item from the same place again does nothing.</summary>
    public async Task ShowAsync(string itemId, ulong? start)
    {
        if (_shown == (itemId, start)) return;
        _shown = (itemId, start);
        _search?.Cancel();
        var cancel = _search = new CancellationTokenSource();
        Closest.Clear();
        Places.Clear();
        try
        {
            var rules = await IngredientLocatorService.RulesAsync(itemId);
            if (cancel.IsCancellationRequested) return;
            foreach (string line in HarvestPlaceText.Lines(rules)) Places.Add(line);
            HasPlaces = Places.Count > 0;
            if (!HasPlaces) { Status = ""; IsSearching = false; return; }
            if (start is not ulong from) { Status = "Load a save to see the closest places that have it."; return; }
            IsSearching = true;
            Status = "Searching the star systems around you…";
            var found = await IngredientLocatorService.FindAsync(from, itemId, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            foreach (var place in found) Closest.Add(place);
            Status = found.Count == 0 ? "None of the systems in or next to your region has it." : "";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Ingredient places] {ex}"); Status = "The search could not finish."; }
        finally { if (ReferenceEquals(_search, cancel)) IsSearching = false; }
    }

    public void Stop() { _search?.Cancel(); _shown = null; }
}
