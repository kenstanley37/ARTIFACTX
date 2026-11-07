using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A ship-builder part as the game shows it: icon and name.</summary>
public sealed class ShipPartChipViewModel(string id)
{
    public string Id { get; } = id;
    public string Name { get; } = ShipPartsService.PartName(id);
    public ImageSource? Icon { get; } = CatalogService.TryGet(id)?.Icon;
}

/// <summary>One choice in a part set's dropdown ("Any", or a part with its odds given
/// the other picks; parts never seen with them are dimmed but still pickable).</summary>
public sealed partial class ShipPartChoiceViewModel : ObservableObject
{
    public ShipPartChoiceViewModel(string? id)
    {
        Id = id;
        Name = id is null ? "Any" : ShipPartsService.PartName(id);
        Icon = id is null ? null : CatalogService.TryGet(id)?.Icon;
    }

    public string? Id { get; }
    public string Name { get; }
    public ImageSource? Icon { get; }

    [ObservableProperty] public partial string Odds { get; set; } = "";
    [ObservableProperty] public partial double Opacity { get; set; } = 1;

    public override string ToString() => Odds.Length > 0 ? $"{Name}, {Odds}" : Name;
}

/// <summary>A part set's dropdown in "Find a seed".</summary>
public sealed partial class ShipPartSetChoiceViewModel : ObservableObject
{
    private readonly Action _changed;

    public ShipPartSetChoiceViewModel(PartSet set, Action changed)
    {
        Set = set;
        _changed = changed;
        Choices = new[] { new ShipPartChoiceViewModel(null) }
            .Concat(set.Parts.Select(p => new ShipPartChoiceViewModel(p.Id)).OrderBy(c => c.Name))
            .ToList();
        SelectedIndex = 0;
    }

    public PartSet Set { get; }
    public IReadOnlyList<ShipPartChoiceViewModel> Choices { get; }

    [ObservableProperty] public partial int SelectedIndex { get; set; }

    public string? WantedId => SelectedIndex > 0 && SelectedIndex < Choices.Count ? Choices[SelectedIndex].Id : null;

    partial void OnSelectedIndexChanged(int value) => _changed?.Invoke();
}

/// <summary>
/// The Starships details' parts list: the ship-builder parts the seed gives the selected
/// ship (or the ones it was built with), and "Find a seed" - one choice per part set with
/// each part's odds, the odds of the whole combination, then a search on all cores.
/// </summary>
public sealed partial class ShipPartsViewModel : ObservableObject
{
    private string _scene = "";
    private List<PartSet> _sets = new();
    private CancellationTokenSource? _search;
    private int _oddsVersion;

    /// <summary>A found seed's Use was clicked.</summary>
    public event EventHandler<string>? SeedChosen;

    /// <summary>Why there are no parts to show (or a hint), dimmed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; } = "";

    public bool HasMessage => Message.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHeading))]
    public partial string Heading { get; private set; } = "";

    public bool HasHeading => Heading.Length > 0;

    public ObservableCollection<ShipPartChipViewModel> Chips { get; } = new();

    [ObservableProperty] public partial bool CanSearch { get; private set; }
    [ObservableProperty] public partial bool SearchOpen { get; set; }
    public ObservableCollection<ShipPartSetChoiceViewModel> Sets { get; } = new();
    [ObservableProperty] public partial string OddsLine { get; private set; } = "";
    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial string SearchLabel { get; private set; } = "Search";
    public ObservableCollection<string> Results { get; } = new();

    public void Clear()
    {
        Message = Heading = "";
        Chips.Clear();
        CanSearch = false;
        Sets.Clear();
        Results.Clear();
        Status = "";
    }

    public void ShowMessage(string message)
    {
        Clear();
        Message = message;
    }

    /// <summary>The parts for a ship model: its built parts when it was built at a
    /// station, else the ones its seed (null: not a valid seed) gives.</summary>
    public void Show(string scene, IReadOnlyList<PartGroup>? root, ulong? seed, BuiltLook? built)
    {
        Clear();
        if (root is null)
        {
            Message = "This ship type has a fixed look - its seed doesn't change its parts.";
            return;
        }
        var sets = ShipPartsService.PartSetsFor(root);
        if (sets.Count == 0)
        {
            Message = "This ship type can't be built from parts or broken into parts in game, so its parts have no names to show.";
            return;
        }

        // A ship built at a station keeps its parts in the save, whatever its seed says.
        if (built is { Items.Count: > 0 })
        {
            Heading = "PARTS CHOSEN WHEN BUILT";
            // One chip per item: a Hauler wing stores two groups (DROPS_WINGDA, DROPS_WINGDA_).
            foreach (var id in built.Items.Select(ShipPartsService.BuilderItem).Distinct(StringComparer.OrdinalIgnoreCase))
                Chips.Add(new ShipPartChipViewModel(id));
        }
        else
        {
            Heading = "PARTS THIS SEED GIVES";
            if (seed is ulong s)
            {
                var picked = ShipPartsService.Picked(scene, s);
                foreach (var set in sets)
                    if (ShipPartsService.Match(set, picked) is string id) Chips.Add(new ShipPartChipViewModel(id));
                if (Chips.Count == 0) Message = "No ship-builder parts match this seed.";
                // Paint isn't shown: no seed -> colour rule fits every ship yet (see the preview).
            }
            else
            {
                Message = "Not a seed - it should look like 0x1CD40D0620985D13.";
            }
        }

        _scene = scene;
        _sets = sets;
        foreach (var set in sets) Sets.Add(new ShipPartSetChoiceViewModel(set, () => _ = UpdateOddsAsync()));
        CanSearch = true;
        OddsLine = "Working out the odds...";
        _ = UpdateOddsAsync();
    }

    /// <summary>The picked part per set (set index -> part id), leaving out "Any".</summary>
    private Dictionary<int, string> Wanted()
    {
        var wanted = new Dictionary<int, string>();
        for (int s = 0; s < Sets.Count; s++)
            if (Sets[s].WantedId is string id) wanted[s] = id;
        return wanted;
    }

    /// <summary>Each part's chance given the other picks, and the whole combination's.</summary>
    private async Task UpdateOddsAsync()
    {
        int mine = ++_oddsVersion;
        string scene = _scene;
        var sets = _sets;
        var sample = await ShipPartsService.SampleFor(scene, sets);
        var wanted = Wanted();
        var ids = Sets.Select(s => s.Choices.Skip(1).Select(c => c.Id!).ToList()).ToList();
        // Counting 100,000 ships per part is too slow for the UI thread on ship types
        // with many parts (a Hauler has 122 wings).
        var (perPart, all) = await Task.Run(() =>
        {
            var counts = new List<Dictionary<string, (int N, bool HasOthers)>>();
            for (int s = 0; s < sets.Count; s++)
            {
                var others = wanted.Where(c => c.Key != s).ToDictionary(c => c.Key, c => c.Value);
                var d = new Dictionary<string, (int, bool)>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in ids[s])
                    d[id] = (ShipPartsService.CountWith(sample, new Dictionary<int, string>(others) { [s] = id }), others.Count > 0);
                counts.Add(d);
            }
            return (counts, wanted.Count == 0 ? -1 : ShipPartsService.CountWith(sample, wanted));
        });
        if (mine != _oddsVersion || !ReferenceEquals(sets, _sets)) return; // a newer pick is already being counted
        for (int s = 0; s < Sets.Count; s++)
            foreach (var choice in Sets[s].Choices.Skip(1))
            {
                var (n, hasOthers) = perPart[s][choice.Id!];
                choice.Odds = hasOthers && n == 0 ? "not seen with your other picks" : ShipPartsService.Odds(n, ShipPartsService.SampleSize);
                choice.Opacity = n == 0 ? 0.5 : 1;
            }
        if (all < 0) { OddsLine = "Pick the parts you want, then Search."; return; }
        double perSecond = 60_000.0 * Math.Max(1, Environment.ProcessorCount - 1);
        OddsLine = all == 0
            ? $"None of {ShipPartsService.SampleSize:N0} sample ships had all of these - they may never appear together. You can still search."
            : $"About {ShipPartsService.Odds(all, ShipPartsService.SampleSize)} ships have all of these - a search should take {ShipPartsService.SearchTime(5 * ShipPartsService.SampleSize / (double)all / perSecond)}.";
    }

    /// <summary>Search, or Stop a running search.</summary>
    public async Task SearchAsync()
    {
        if (_search is not null) { _search.Cancel(); return; }
        Results.Clear();
        _search = new CancellationTokenSource();
        SearchLabel = "Stop";
        Status = "Searching...";
        try
        {
            var (found, tried) = await ShipPartsService.SearchAsync(_scene, _sets, Wanted(), _search.Token);
            Status = found.Count == 0
                ? $"No match in {tried:N0} seeds - these parts may never appear together."
                : $"Found {found.Count} in {tried:N0} seeds.";
            foreach (ulong seed in found) Results.Add($"0x{seed:X16}");
        }
        finally
        {
            _search = null;
            SearchLabel = "Search";
        }
    }

    public void Use(string seed) => SeedChosen?.Invoke(this, seed);
}
