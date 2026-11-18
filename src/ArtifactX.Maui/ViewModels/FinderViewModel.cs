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
/// desktop's Multi-Tool Finder), best class first then nearest, with the place to load the game for each.
/// </summary>
public sealed partial class FinderViewModel : ObservableObject
{
    private const int MaxRows = 100;

    private IReadOnlyList<CabinetMultiToolFind> _found = [];
    private (ulong Start, int Radius)? _searchedFor;
    private CancellationTokenSource? _cancel;
    private ulong _start;

    [ObservableProperty] public partial IReadOnlyList<RecentSystemSummary> Starts { get; private set; } = [];
    [ObservableProperty] public partial RecentSystemSummary? Start { get; set; }
    [ObservableProperty] public partial bool HasStarts { get; private set; }
    public bool HasNoStarts => !HasStarts;

    public IReadOnlyList<FinderChoice> Areas { get; } =
        [new("This region", "0"), new("This region and the ones around it (slower)", "1")];
    [ObservableProperty] public partial FinderChoice? Area { get; set; }

    public IReadOnlyList<FinderChoice> Classes { get; } =
        [new("S-class only", "3"), new("A-class or better", "2"), new("Any class", "0")];
    [ObservableProperty] public partial FinderChoice? Class { get; set; }

    [ObservableProperty] public partial IReadOnlyList<FinderChoice> Kinds { get; private set; } = [new("Any kind", "")];
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
        if (!HasStarts) Status = "Open a system on the Systems tab first: the search starts from your recent systems.";
    }

    partial void OnStartChanged(RecentSystemSummary? value) => _ = SearchAsync();
    partial void OnAreaChanged(FinderChoice? value) => _ = SearchAsync();
    partial void OnClassChanged(FinderChoice? value) => Apply();
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
            string keep = Kind?.Value ?? "";
            Kinds = [new("Any kind", ""), .. found.GroupBy(f => f.Kind).OrderBy(g => g.Key, StringComparer.CurrentCulture)
                .Select(g => new FinderChoice(g.Key, g.Key))];
            Kind = Kinds.FirstOrDefault(k => k.Value == keep) ?? Kinds[0];
            Apply();
        }
        catch (OperationCanceledException) { }
        finally { if (!cancel.IsCancellationRequested) IsSearching = false; }
    }

    private void Apply()
    {
        if (_searchedFor is null || Class is null) return;
        int minClass = int.Parse(Class.Value);
        string kind = Kind?.Value ?? "";
        var shown = _found.Where(f => f.ClassIndex >= minClass && (kind.Length == 0 || f.Kind == kind))
            // The same tool in the same cabinet once (two cabinets can draw the same pool member).
            .GroupBy(f => (f.SystemAddress, f.ReloadBody, f.Cabinet, f.Tool.Seed)).Select(g => g.First())
            .Select(f => (Find: f, Ly: NmsPlanets.DistanceLightYears(_start, f.SystemAddress)))
            .OrderByDescending(x => x.Find.ClassIndex).ThenBy(x => x.Ly).ThenBy(x => x.Find.ReloadBody)
            .ToList();
        Results = shown.Take(MaxRows).Select(x => Row(x.Find, x.Find.SystemAddress == _start ? null : x.Ly)).ToArray();
        Status = shown.Count == 0 ? "Nothing matches here. Try another class or kind, or search the regions around too."
            : shown.Count > MaxRows ? $"The best {MaxRows} of {shown.Count:N0} offers. Tap one to open its system."
            : $"{shown.Count:N0} offers. Tap one to open its system.";
    }

    private static FinderRowViewModel Row(CabinetMultiToolFind f, double? ly)
    {
        var (portal, galaxy) = SystemProfiles.PortalOf(f.SystemAddress);
        string letter = f.ClassIndex switch { 3 => "S", 2 => "A", 1 => "B", _ => "C" };
        string system = SystemGenerator.SystemName(portal, galaxy);
        // Distances are between regions (NmsPlanets.DistanceLightYears), so the start's own region reads 0.
        string distance = ly is not double d ? "the start system" : d < 1 ? "in the same region" : $"{d:N0} ly away";
        string load = f.ReloadBody == 0 ? "Load at the space station (or just warp in)"
            : $"Load on {SystemGenerator.PlanetName(portal | ((ulong)f.ReloadBody << 44), galaxy)} (body {f.ReloadBody})";
        string code = $"1{portal & 0xFFFFFFFFFFF:X11}";
        return new FinderRowViewModel($"{letter}-class {f.Kind}: {f.Tool.DefaultName}", $"{system} · {distance}",
            $"{load}, then station cabinet {f.Cabinet}", code, galaxy + 1, f.ClassIndex == 3);
    }
}
