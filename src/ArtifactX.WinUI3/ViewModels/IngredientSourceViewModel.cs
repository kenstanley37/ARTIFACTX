using ArtifactX.WinUI3.Helpers.Recipes;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// "Where to get it" for one raw ingredient (IngredientSourcePanel): the kinds of planet whose wild plants or objects
/// give it, the creatures to feed or hunt for it, or where it bites, then the closest systems that have it, searched from where the save is (IngredientLocatorService). A newer
/// ingredient cancels an older search; the list fills in place when the search ends.
/// </summary>
public sealed partial class IngredientSourceViewModel : ObservableObject
{
    private CancellationTokenSource? _search;
    private (string Item, ulong? Start)? _shown;
    private string? _placesFor;

    public ObservableCollection<string> Places { get; } = [];
    public ObservableCollection<IngredientPlace> Closest { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlacesVisibility))] public partial bool HasPlaces { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusVisibility))] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial bool IsSearching { get; set; }
    public Visibility PlacesVisibility => HasPlaces ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StatusVisibility => Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Shows <paramref name="itemId"/>, searching around <paramref name="start"/> (the save's system; null
    /// without a save). Showing the same item from the same place again does nothing. When only the place changes (the
    /// player jumped while gathering), what's shown stays put until the new list is ready, then changes card by card,
    /// so the panel doesn't blink or move.</summary>
    public async Task ShowAsync(string itemId, ulong? start)
    {
        if (_shown == (itemId, start)) return;
        bool moved = _placesFor == itemId;
        _shown = (itemId, start);
        _search?.Cancel();
        var cancel = _search = new CancellationTokenSource();
        if (!moved)
        {
            _placesFor = null;
            Closest.Clear();
            Places.Clear();
        }
        try
        {
            if (!moved)
            {
                var rules = await IngredientLocatorService.RulesAsync(itemId);
                if (cancel.IsCancellationRequested) return;
                foreach (string line in HarvestPlaceText.Lines(rules)) Places.Add(line);
                var (fed, hunted) = await IngredientLocatorService.CreatureWaysAsync(itemId);
                if (cancel.IsCancellationRequested) return;
                if (fed) Places.Add(HarvestPlaceText.FeedLine);
                if (hunted) Places.Add(HarvestPlaceText.HuntLine);
                if (await IngredientLocatorService.FishAsync(itemId) is { } fish)
                {
                    if (cancel.IsCancellationRequested) return;
                    Places.Add(HarvestPlaceText.FishLine(fish));
                }
                var names = await IngredientLocatorService.ItemNamesAsync();
                if (cancel.IsCancellationRequested) return;
                foreach (string line in HarvestPlaceText.OtherLines(itemId, names)) Places.Add(line);
                if (await IngredientLocatorService.CraftLineAsync(itemId) is { } craft)
                {
                    if (cancel.IsCancellationRequested) return;
                    Places.Add(craft);
                }
                HasPlaces = Places.Count > 0;
                _placesFor = itemId;
            }
            if (!HasPlaces) { Status = ""; IsSearching = false; return; }
            if (start is not ulong from) { Closest.Clear(); Status = "Load a save to see the closest places that have it."; return; }
            IsSearching = true;
            if (Closest.Count == 0) Status = "Searching the star systems around you…";
            var found = await IngredientLocatorService.FindAsync(from, itemId, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            Sync(found);
            Status = found.Count == 0 ? "None of the systems in or next to your region has it." : "";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Ingredient places] {ex}"); Status = "The search could not finish."; }
        finally { if (ReferenceEquals(_search, cancel)) IsSearching = false; }
    }

    /// <summary>Makes <see cref="Closest"/> match <paramref name="found"/>, leaving the cards that didn't change alone.</summary>
    private void Sync(IReadOnlyList<IngredientPlace> found)
    {
        for (int i = 0; i < found.Count; i++)
            if (i >= Closest.Count) Closest.Add(found[i]);
            else if (!Same(Closest[i], found[i])) Closest[i] = found[i];
        while (Closest.Count > found.Count) Closest.RemoveAt(Closest.Count - 1);
    }

    private static bool Same(IngredientPlace a, IngredientPlace b) =>
        a.SystemAddress == b.SystemAddress && a.IsHere == b.IsHere && a.AccessibleName == b.AccessibleName;

    public void Stop() { _search?.Cancel(); _shown = null; _placesFor = null; }
}
