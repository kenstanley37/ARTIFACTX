using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Models;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>A choice in one of the finder's pickers.</summary>
public sealed record FinderChoice(string Label, string Value)
{
    public override string ToString() => Label;
}

/// <summary>A find: the line saying what it is, where (system and distance), and what to do; tap opens the system.</summary>
public sealed record FinderRowViewModel(string Title, string Place, string Action, string PortalCode, int Galaxy, bool IsBest)
{
    public string AccessibleName => $"{Title}. {Place}. {Action}. Open the system.";
}

/// <summary>
/// The phone's Find tab: searches the star systems around one of your recent systems (the phone has no save to say
/// where you are) for space station cabinets selling a kind and class of multi-tool (MultiToolFinderService, the
/// desktop's Multi-Tool Finder), best class first then nearest, with the place to load the game for each; or for the
/// planets that give an ingredient, creature produce or a fish (IngredientFinderService, the desktop's Ingredient
/// Finder); or for claimable crashed ships of a class and type (ShipFinderService, the desktop's crashed-ship rules).
/// </summary>
public sealed partial class FinderViewModel : ObservableObject
{
    private const int MaxRows = 100;

    private IReadOnlyList<CabinetMultiToolFind> _found = [];
    private (ulong Start, int Radius)? _searchedFor;
    private CancellationTokenSource? _cancel;
    private ulong _start;
    /// <summary>The start region's stars (RegionStarPositions), for galaxy-map light-years inside it; null when unknown.</summary>
    private IReadOnlyList<RegionStar>? _stars;

    private const string MultiToolsMode = "MultiTools", IngredientsMode = "Ingredients", ShipsMode = "Ships";

    /// <summary>What the tab finds: multi-tools, ingredients or crashed ships.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultiTools), nameof(IsIngredients), nameof(IsShips), nameof(HasClass), nameof(Title), nameof(Intro))]
    public partial string Mode { get; set; } = MultiToolsMode;

    public bool IsMultiTools => Mode == MultiToolsMode;
    public bool IsIngredients => Mode == IngredientsMode;
    public bool IsShips => Mode == ShipsMode;
    public bool HasClass => !IsIngredients;
    public string Title => Mode switch { IngredientsMode => "Find ingredients", ShipsMode => "Find crashed ships", _ => "Find multi-tools" };
    public string Intro => Mode switch
    {
        IngredientsMode => "The nearest systems whose planets give a plant, mineral, creature produce or fish you need.",
        ShipsMode => "Crashed ships you can claim, of the class and type you want, on the planets of the nearest systems.",
        _ => "Station cabinets selling the kind and class of multi-tool you want, in the systems around one you've opened.",
    };

    /// <summary>The ship types a wreck can be (no Living Ships or freighters crash).</summary>
    public IReadOnlyList<FinderChoice> ShipTypes { get; } = (FinderChoice[])[new("Any type", ""), new("Fighter", "Fighter"),
        new("Hauler", "Hauler"), new("Explorer", "Explorer"), new("Shuttle", "Shuttle"), new("Exotic", "Exotic"), new("Solar", "Solar")];
    [ObservableProperty] public partial FinderChoice? ShipType { get; set; }

    /// <summary>What the player types to find an ingredient; suggestions follow it.</summary>
    [ObservableProperty] public partial string IngredientText { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<FindableItem> Suggestions { get; private set; } = [];
    [ObservableProperty] public partial bool HasSuggestions { get; private set; }
    private FindableItem? _ingredient;
    private IReadOnlyList<FindableItem> _findable = [];

    [ObservableProperty] public partial IReadOnlyList<RecentSystemSummary> Starts { get; private set; } = [];
    [ObservableProperty] public partial RecentSystemSummary? Start { get; set; }
    [ObservableProperty] public partial bool HasStarts { get; private set; }
    public bool HasNoStarts => !HasStarts;

    public IReadOnlyList<FinderChoice> Areas { get; } =
        (FinderChoice[])[new("This region", "0"), new("This region and the ones around it (slower)", "1")];
    [ObservableProperty] public partial FinderChoice? Area { get; set; }

    public IReadOnlyList<FinderChoice> Classes { get; } =
        (FinderChoice[])[new("S-class only", "3"), new("A-class or better", "2"), new("Any class", "0")];
    [ObservableProperty] public partial FinderChoice? Class { get; set; }

    [ObservableProperty] public partial IReadOnlyList<FinderChoice> Kinds { get; private set; } = (FinderChoice[])[new("Any kind", "")];
    [ObservableProperty] public partial FinderChoice? Kind { get; set; }

    [ObservableProperty] public partial IReadOnlyList<FinderRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string Status { get; private set; } = "";
    [ObservableProperty] public partial bool IsSearching { get; private set; }

    /// <summary>How the load place works, in one line (SystemCabinets.RollSteps has the full steps on System Details).</summary>
    public string RollNote =>
        "Each find says where to load your game: save there, quit to the main menu and load that save, then go to the station cabinet. "
        + "Just warped in counts as loading at the station.";

    partial void OnHasStartsChanged(bool value) => OnPropertyChanged(nameof(HasNoStarts));

    /// <summary>Refreshes the start list from Recent systems and searches when the start changed.</summary>
    public void Refresh()
    {
        var items = RecentSystemsService.Items;
        if (!items.SequenceEqual(Starts))
        {
            var keep = Start;
            Starts = items.ToArray();
            HasStarts = Starts.Count > 0;
            Start = Starts.FirstOrDefault(s => s == keep) ?? Starts.FirstOrDefault();
        }
        Area ??= Areas[0];
        Class ??= Classes[0];
        Kind ??= Kinds[0];
        ShipType ??= ShipTypes[0];
        if (!HasStarts) Status = "Open a system on the Systems tab first: the search starts from your recent systems.";
    }

    partial void OnStartChanged(RecentSystemSummary? value) => Run();

    partial void OnModeChanged(string value)
    {
        _cancel?.Cancel();
        IsSearching = false;
        Results = [];
        Status = "";
        _searchedFor = null;
        if (IsIngredients)
        {
            if (_ingredient is null) Status = "Type what you need, then pick it from the list.";
            _ = LoadFindableAsync();
        }
        Run();
    }

    /// <summary>Searches again for the current mode.</summary>
    private void Run()
    {
        if (IsMultiTools) _ = SearchAsync();
        else if (IsIngredients) _ = FindIngredientAsync();
        else _ = FindShipsAsync();
    }

    [RelayCommand] private void ShowMultiTools() => Mode = MultiToolsMode;

    [RelayCommand] private void ShowIngredients() => Mode = IngredientsMode;

    [RelayCommand] private void ShowShips() => Mode = ShipsMode;

    partial void OnShipTypeChanged(FinderChoice? value) { if (IsShips) _ = FindShipsAsync(); }

    /// <summary>Crashed ships of the chosen class and type, nearest systems first; finds appear system by system.</summary>
    private async Task FindShipsAsync()
    {
        if (Start is not { } start || Class is null || ShipType is null) return;
        ulong from = NmsPortalCode.ToAddress(start.PortalCode, start.GalaxyNumber) & ~(0xFUL << 52);
        int minClass = int.Parse(Class.Value);
        string type = ShipType.Value;
        string grade = minClass switch { 3 => "S-class", 2 => "A-class or better", 1 => "B-class or better", _ => "" };
        string what = $"{grade} {(type.Length > 0 ? type : "ship")}".Trim();
        _cancel?.Cancel();
        var cancel = _cancel = new CancellationTokenSource();
        IsSearching = true;
        Results = [];
        Status = $"Looking for a crashed {what} around {start.Name}...";
        var progress = new Progress<(int Systems, IReadOnlyList<ShipFind> Found)>(p =>
        {
            if (cancel.IsCancellationRequested) return;
            ShowShipFinds(p.Found);
            Status = $"Searched {p.Systems} of the nearest systems: {p.Found.Count} found so far...";
        });
        try
        {
            var found = await ShipFinderService.FindAsync(from, minClass, type, progress, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            if (found is null) { Status = "The finder needs the game rules, which this copy of the app doesn't have."; return; }
            ShowShipFinds(found);
            Status = found.Count == 0 ? $"No crashed {what} in the {ShipFinderService.MaxSystems} systems nearest {start.Name}."
                : "Crashed ships nearest you, best first. Tap one to open its system; the coordinates are for the Analysis Visor.";
        }
        catch (OperationCanceledException) { }
        finally { if (!cancel.IsCancellationRequested) IsSearching = false; }
    }

    private void ShowShipFinds(IReadOnlyList<ShipFind> found) => Results = found.Select(f =>
    {
        var (portal, galaxy) = SystemProfiles.PortalOf(f.SystemAddress);
        string letter = f.ClassIndex switch { 3 => "S", 2 => "A", 1 => "B", _ => "C" };
        string distance = f.SystemLy == 0 ? "the start system" : f.SystemLy < double.MaxValue ? $"{f.SystemLy:N0} ly away" : "in the same region";
        string where = (f.Underwater ? "Underwater at " : "At ") + f.Coordinates
            + (f.PortalKm is double km ? $" · {km:N1} km from the portal" : "");
        return new FinderRowViewModel($"{letter}-class {f.TypeLabel}: {f.ShipName}", $"{f.SystemName} · {f.PlanetName} · {distance}",
            where, $"1{portal & 0xFFFFFFFFFFF:X11}", galaxy + 1, f.ClassIndex == 3);
    }).ToArray();

    private async Task LoadFindableAsync()
    {
        if (_findable.Count == 0) _findable = await IngredientFinderService.ItemsAsync();
        Suggest();
    }

    partial void OnIngredientTextChanged(string value) => Suggest();

    /// <summary>Up to 8 items whose name has the typed text, names starting with it first; none once one is picked.</summary>
    private void Suggest()
    {
        string text = IngredientText.Trim();
        Suggestions = text.Length < 2 || _ingredient?.Name == IngredientText ? []
            : _findable.Where(i => i.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(i => !i.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase)).ThenBy(i => i.Name.Length)
                .Take(8).ToArray();
        HasSuggestions = Suggestions.Count > 0;
    }

    [RelayCommand]
    private async Task PickIngredientAsync(FindableItem? item)
    {
        if (item is null) return;
        _ingredient = item;
        IngredientText = item.Name;
        Suggestions = [];
        HasSuggestions = false;
        await FindIngredientAsync();
    }

    private async Task FindIngredientAsync()
    {
        if (Start is not { } start || _ingredient is not { } item) return;
        ulong from = NmsPortalCode.ToAddress(start.PortalCode, start.GalaxyNumber) & ~(0xFUL << 52);
        _cancel?.Cancel();
        var cancel = _cancel = new CancellationTokenSource();
        IsSearching = true;
        Results = [];
        Status = $"Looking for {item.Name} around {start.Name}...";
        try
        {
            var places = await IngredientFinderService.FindAsync(from, item.Id, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            if (places is null) { Status = "The finder needs the game rules, which this copy of the app doesn't have."; return; }
            Results = places.Select(p =>
            {
                var (portal, galaxy) = SystemProfiles.PortalOf(p.SystemAddress);
                return new FinderRowViewModel(p.SystemName, p.Distance, string.Join(Environment.NewLine, p.Bodies.Select(b => "• " + b)),
                    $"1{portal & 0xFFFFFFFFFFF:X11}", galaxy + 1, p.IsHere);
            }).ToArray();
            Status = Results.Count == 0 ? $"No planets near {start.Name} give {item.Name}."
                : $"The nearest systems with {item.Name}. Tap one to open it.";
        }
        catch (OperationCanceledException) { }
        finally { if (!cancel.IsCancellationRequested) IsSearching = false; }
    }
    partial void OnAreaChanged(FinderChoice? value) { if (IsMultiTools) _ = SearchAsync(); }
    partial void OnClassChanged(FinderChoice? value) { if (IsShips) _ = FindShipsAsync(); else Apply(); }
    partial void OnKindChanged(FinderChoice? value) => Apply();

    [RelayCommand]
    private static async Task OpenAsync(FinderRowViewModel? row)
    {
        if (row is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync($"system-details?code={Uri.EscapeDataString(row.PortalCode)}&galaxy={row.Galaxy}");
    }

    private async Task SearchAsync()
    {
        if (Start is not { } start || Area is null) return;
        _start = NmsPortalCode.ToAddress(start.PortalCode, start.GalaxyNumber) & ~(0xFUL << 52);
        int radius = int.Parse(Area.Value);
        if (_searchedFor == (_start, radius)) return;
        _cancel?.Cancel();
        var cancel = _cancel = new CancellationTokenSource();
        IsSearching = true;
        Results = [];
        Status = radius == 0 ? $"Searching the star systems in the region of {start.Name}..." : "Searching the star systems around you...";
        var progress = new Progress<int>(n => { if (!cancel.IsCancellationRequested) Status = $"Searching star systems: {n:N0} done..."; });
        try
        {
            var found = await MultiToolFinderService.FindAsync(_start, radius, progress, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            if (found is null)
            {
                Status = "The finder needs the game rules, which this copy of the app doesn't have.";
                return;
            }
            _found = found;
            _searchedFor = (_start, radius);
            ulong from = _start;
            _stars = await GameRulesService.GetAsync() is { GalaxySettings: { } settings, GalaxyPlacement: { } placement }
                ? await Task.Run(() => RegionStarPositions.ForSystem(from, settings, placement)) : null;
            string keep = Kind?.Value ?? "";
            Kinds = (FinderChoice[])[new("Any kind", ""), .. found.GroupBy(f => f.Kind).OrderBy(g => g.Key, StringComparer.CurrentCulture)
                .Select(g => new FinderChoice(g.Key, g.Key))];
            Kind = Kinds.FirstOrDefault(k => k.Value == keep) ?? Kinds[0];
            Apply();
        }
        catch (OperationCanceledException) { }
        finally { if (!cancel.IsCancellationRequested) IsSearching = false; }
    }

    private void Apply()
    {
        if (!IsMultiTools || _searchedFor is null || Class is null) return;
        int minClass = int.Parse(Class.Value);
        string kind = Kind?.Value ?? "";
        var shown = _found.Where(f => f.ClassIndex >= minClass && (kind.Length == 0 || f.Kind == kind))
            // The same tool in the same cabinet once (two cabinets can draw the same pool member).
            .GroupBy(f => (f.SystemAddress, f.ReloadBody, f.Cabinet, f.Tool.Seed)).Select(g => g.First())
            .Select(f => (Find: f, Ly: LightYears(f.SystemAddress)))
            .OrderByDescending(x => x.Find.ClassIndex).ThenBy(x => x.Ly).ThenBy(x => x.Find.ReloadBody)
            .ToList();
        Results = shown.Take(MaxRows).Select(x => Row(x.Find, x.Find.SystemAddress == _start ? null : x.Ly)).ToArray();
        Status = shown.Count == 0 ? "Nothing matches here. Try another class or kind, or search the regions around too."
            : shown.Count > MaxRows ? $"The best {MaxRows} of {shown.Count:N0} offers. Tap one to open its system."
            : $"{shown.Count:N0} offers. Tap one to open its system.";
    }

    /// <summary>Light-years from the start: the galaxy map's inside its region (the stars' own positions, checked on
    /// the map 2026-10-27), else between regions (NmsPlanets.DistanceLightYears).</summary>
    private double? LightYears(ulong system)
    {
        if (RegionStarGeneration.RegionKey(system) == RegionStarGeneration.RegionKey(_start) && _stars is { } stars)
        {
            int here = (int)((_start >> 40) & 0xFFF), there = (int)((system >> 40) & 0xFFF);
            if (stars.FirstOrDefault(s => s.System == here) is { System: > 0 } a && stars.FirstOrDefault(s => s.System == there) is { System: > 0 } b)
                return RegionStarPositions.LightYears(a, b);
        }
        return NmsPlanets.DistanceLightYears(_start, system);
    }

    private static FinderRowViewModel Row(CabinetMultiToolFind f, double? ly)
    {
        var (portal, galaxy) = SystemProfiles.PortalOf(f.SystemAddress);
        string letter = f.ClassIndex switch { 3 => "S", 2 => "A", 1 => "B", _ => "C" };
        string system = SystemGenerator.SystemName(portal, galaxy);
        // Systems whose star isn't placed (spiral regions, the centre) fall back to between-region distance: 0 inside.
        string distance = ly is not double d ? "the start system" : d < 1 ? "in the same region" : $"{d:N0} ly away";
        string load = f.ReloadBody == 0 ? "Load at the space station (or just warp in)"
            : $"Load on {SystemGenerator.PlanetName(portal | ((ulong)f.ReloadBody << 44), galaxy)} (body {f.ReloadBody})";
        string code = $"1{portal & 0xFFFFFFFFFFF:X11}";
        return new FinderRowViewModel($"{letter}-class {f.Kind}: {f.Tool.DefaultName}", $"{system} · {distance}",
            $"{load}, then station cabinet {f.Cabinet}", code, galaxy + 1, f.ClassIndex == 3);
    }
}
