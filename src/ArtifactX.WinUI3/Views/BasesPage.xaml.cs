using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Every base in the save, in folding sections (BasesViewModel), following the game's
/// saves while it runs (FishCatchRecorder). Planet bases and loose placed objects can be deleted; "Open ›"
/// shows a base's parts (BaseDetailsPage).
/// </summary>
public sealed partial class BasesPage : Page
{
    private PlanetSnapshot? _snapshot;
    private string _lastKey = "";

    public BasesViewModel ViewModel { get; } = new();

    public BasesPage()
    {
        InitializeComponent();
        FishCatchRecorder.SnapshotSeen += OnLiveSnapshot;
        SaveSessionManager.ActiveSessionChanged += OnSessionChanged;
        JumpPlanner.RangeChanged += Rebuild;
        PlanetDetailsService.Captured += OnPlanetChanged;
        PlanetDetailsService.Changed += OnPlanetChanged;
        PlanetWaterService.Changed += OnPlanetChanged;
        CreatureListService.Changed += OnPlanetChanged;
        FindsService.Changed += Rebuild;
        SystemNotesService.Changed += OnSystemChanged;
        SystemPageReader.Captured += OnSystemChanged;
        Unloaded += (_, _) =>
        {
            FishCatchRecorder.SnapshotSeen -= OnLiveSnapshot;
            SaveSessionManager.ActiveSessionChanged -= OnSessionChanged;
            JumpPlanner.RangeChanged -= Rebuild;
            PlanetDetailsService.Captured -= OnPlanetChanged;
            PlanetDetailsService.Changed -= OnPlanetChanged;
            PlanetWaterService.Changed -= OnPlanetChanged;
            CreatureListService.Changed -= OnPlanetChanged;
            FindsService.Changed -= Rebuild;
            SystemNotesService.Changed -= OnSystemChanged;
            SystemPageReader.Captured -= OnSystemChanged;
        };
        _ = LoadAsync();
    }

    public Visibility IsListVisible(bool loading) => loading ? Visibility.Collapsed : Visibility.Visible;

    private async Task LoadAsync()
    {
        if (!SaveSessionManager.IsSaveLoaded) return;
        await Task.Yield(); // let the page and its spinner show before reading the save
        await PortalGlyphs.WarmAsync();
        _lastKey = "";
        await BuildAsync(LiveSaveWatcher.FromSession());
    }

    private async Task BuildAsync(PlanetSnapshot snapshot)
    {
        _snapshot = snapshot;
        ulong? here = S.ReadSystemAddress(snapshot.Universe);
        string key = $"{here}|{snapshot.Bases?.Count}|{snapshot.LooseObjects?.Count}|{snapshot.Settlements?.Count}|{NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects)}";
        if (key == _lastKey) return;
        _lastKey = key;

        var placedIds = snapshot.LooseObjects?.OfType<JObject>().Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id.Length > 0).ToList();
        if (placedIds is { Count: > 0 }) await CatalogService.WarmCacheAsync(placedIds);

        var (list, furnished) = await Task.Run(() => (BaseListBuilder.Build(snapshot, here), BaseEditService.StationsWithDefaultFurniture(snapshot.Bases)));
        if (key != _lastKey) return; // a newer snapshot arrived meanwhile
        ViewModel.Show(list);
        ViewModel.ShowFurnished(furnished);
        await ViewModel.LoadPicturesAsync(SaveSessionManager.ActivePlatformFolder);
    }

    private void Rebuild() => DispatcherQueue.TryEnqueue(() =>
    {
        if (_snapshot is null) return;
        _lastKey = "";
        _ = BuildAsync(_snapshot);
    });

    private void OnPlanetChanged(ulong system, int planet) => Rebuild();
    private void OnSystemChanged(ulong system) => Rebuild();
    private void OnSessionChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() => _ = LoadAsync());

    /// <summary>Each save the game writes (FishCatchRecorder reads them for the whole app).</summary>
    private void OnLiveSnapshot(PlanetSnapshot snapshot) => DispatcherQueue.TryEnqueue(() =>
    {
        LiveStatusTxt.Text = $"Following your game · updated {DateTime.Now:t}";
        _ = BuildAsync(snapshot);
    });

    private void Section_OpenRequested(object? sender, BaseRowViewModel row) =>
        Frame.Navigate(typeof(ExplorePages.BaseDetailsPage), row);

    private void DeselectAll_Click(object sender, RoutedEventArgs e) => ViewModel.DeselectAll();

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.Selected.ToList();
        if (selected.Count == 0) return;
        var bases = selected.Where(r => r.Kind == BaseKind.Planet && r.Base is not null).ToList();
        var placed = selected.Where(r => r.Kind == BaseKind.Placed && r.LooseObject is not null).ToList();

        string what = (bases.Count, placed.Count) switch
        {
            (0, var p) => $"This deletes {Count(p, "placed object")} from this save.",
            (var b, 0) => $"This deletes {Count(b, "planet base")} from this save, with everything built in {(b == 1 ? "it" : "them")}.",
            (var b, var p) => $"This deletes {Count(b, "planet base")}, with everything built in {(b == 1 ? "it" : "them")}, and {Count(p, "placed object")} from this save.",
        };
        bool confirmed = await DeleteConfirmation.ShowListAsync(XamlRoot,
            selected.Count == 1 ? $"Delete {selected[0].Name}?" : $"Delete {selected.Count:N0} items?",
            what,
            selected.Select(r => $"{r.Name} - {r.Detail}").ToList(),
            bases.Count > 0 ? "A deleted base is gone completely, base computer and all - to empty a base but keep it, open it and delete its parts instead." : null);
        if (!confirmed) return;

        bool changed = BaseEditService.RemoveBases(bases.Select(r => r.Base!).ToList());
        changed |= BaseEditService.RemoveLooseObjects(placed.Select(r => r.LooseObject!).ToList());
        if (!changed) return;
        _lastKey = "";
        await BuildAsync(LiveSaveWatcher.FromSession());
    }

    private async void Section_ClearFurnitureRequested(object? sender, BaseSectionViewModel section)
    {
        var stations = section.FurnishedStations;
        if (stations.Count == 0) return;
        bool confirmed = await DeleteConfirmation.ShowListAsync(XamlRoot,
            $"Clear {section.FreeableParts:N0} pieces of default furniture?",
            $"This removes the default furniture from {Count(stations.Count, "claimed station")} - only the pieces every claim comes with, where the claim put them. Each station's structure, anything you moved and anything you built stay, and so do your directorships.",
            stations.Select(s => $"{s.Name} · {s.Count:N0}").ToList());
        if (!confirmed) return;
        if (BaseEditService.ClearStationDefaultFurniture(stations.Select(s => s.Base).ToList()) == 0) return;
        _lastKey = "";
        await BuildAsync(LiveSaveWatcher.FromSession());
    }

    private static string Count(int n, string thing) => n == 1 ? $"1 {thing}" : $"{n:N0} {thing}s";

    private async void ExportCsv_Click(object sender, RoutedEventArgs e) =>
        await CsvExportService.ExportAsync(ExportBtn, "ArtifactX bases and placed objects",
            new[] { "Kind", "Name", "Where", "Portal code", "Distance", "Parts" },
            ViewModel.Shown().Select(r => new[] { r.KindLabel, r.Name, r.Detail, r.PortalCode, r.DistanceLabel, r.PartsLabel }));
}
