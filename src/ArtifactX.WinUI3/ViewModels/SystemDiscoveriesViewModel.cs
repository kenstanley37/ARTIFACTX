using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Current System page's "Discover everything" button under System Data (#43, SystemDiscoveryService): what the save's current
/// system still lacks (its (i) explains), staging it all as discoveries made but not uploaded, and Undo. It acts on the
/// loaded save's location, which the page shows.
/// </summary>
public sealed partial class SystemDiscoveriesViewModel : ObservableObject
{
    private SystemDiscoveryService.Plan? _plan;
    private int _version;

    [ObservableProperty] public partial bool IsVisible { get; private set; }
    [ObservableProperty] public partial bool IsBusy { get; private set; }
    [ObservableProperty] public partial bool IsStaged { get; private set; }
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DiscoverCommand))] public partial bool CanDiscover { get; private set; }
    [ObservableProperty] public partial string Summary { get; private set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Info))] public partial string Note { get; private set; } = "";

    /// <summary>The (i) next to the button: where things stand, then what the button does.</summary>
    public string Info => Summary.Length == 0 ? Note : Summary + Environment.NewLine + Environment.NewLine + Note;
    partial void OnSummaryChanged(string value) => OnPropertyChanged(nameof(Info));

    public bool IsNotStaged => !IsStaged;
    partial void OnIsStagedChanged(bool value) => OnPropertyChanged(nameof(IsNotStaged));

    /// <summary>Works out again what the system lacks (after a load, a new save, or staged edits changing).</summary>
    public async Task RefreshAsync()
    {
        int version = ++_version;
        bool staged = SystemDiscoveryService.IsStaged();
        IsBusy = true;
        SystemDiscoveryService.Plan? plan;
        try { plan = staged ? null : await SystemDiscoveryService.PlanAsync(); }
        catch (Exception ex) { plan = null; System.Diagnostics.Debug.WriteLine($"[SystemDiscoveries] Plan failed: {ex}"); }
        if (version != _version) return;
        IsBusy = false;
        _plan = plan;
        IsStaged = staged;
        IsVisible = staged || plan is not null;
        CanDiscover = plan is { Total: > 0 };
        Note = PlanetTypesService.ProfileTables?.Objects?.Spawn is null
            ? "Plants and minerals need up-to-date game data: rebuild it in Settings to include them."
            : "Marks every planet, creature, plant and mineral here as discovered. They're added as discoveries you haven't uploaded yet: upload them in game from the Discoveries page. Nothing is written until you save.";
        Summary = staged
            ? "Discoveries staged for this system. Save to write them, or Undo."
            : plan is null ? ""
            : plan.Total == 0 ? "Everything in this system is already discovered."
            : "Not yet discovered here: " + Counts(plan) + ".";
    }

    /// <summary>Another page (or Save) staged, saved or discarded edits: work it out again only when this card's
    /// staged state changed (planning runs every planet's generators).</summary>
    public void RefreshIfStagingChanged()
    {
        if (SystemDiscoveryService.IsStaged() != IsStaged) _ = RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDiscover))]
    private async Task DiscoverAsync()
    {
        if (_plan is not { Total: > 0 } plan) return;
        SystemDiscoveryService.Stage(plan);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        SystemDiscoveryService.Revert();
        await RefreshAsync();
    }

    private static string Counts(SystemDiscoveryService.Plan plan)
    {
        var parts = new List<string>();
        void Add(int n, string one, string many) { if (n > 0) parts.Add($"{n:N0} {(n == 1 ? one : many)}"); }
        Add(plan.Planets, "planet", "planets");
        Add(plan.Creatures, "creature", "creatures");
        Add(plan.Flora, "plant", "plants");
        Add(plan.Minerals, "mineral", "minerals");
        return string.Join(", ", parts);
    }
}
