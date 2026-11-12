using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

public sealed partial class GalaxyTransferViewModel : ObservableObject
{
    private List<GalaxyRowViewModel> _allRows = new();

    [ObservableProperty]
    public partial GalaxyInfo? CurrentGalaxy { get; set; }

    [ObservableProperty]
    public partial string CurrentGalaxyTitle { get; set; } = "";

    [ObservableProperty]
    public partial string CurrentGalaxyClassification { get; set; } = "";

    [ObservableProperty]
    public partial GalaxyRowViewModel? SelectedGalaxy { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedPresenceFilter { get; set; } = "All";

    [ObservableProperty]
    public partial string SelectedCategoryFilter { get; set; } = "All";

    [ObservableProperty]
    public partial bool IsTransferStaged { get; set; }

    [ObservableProperty]
    public partial string StagedStatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string TransferActionText { get; set; } = "Transfer Starship to Station";

    [ObservableProperty]
    public partial bool CanTransfer { get; set; }

    // Filter Button Labels with live counts
    [ObservableProperty] public partial string AllFilterLabel { get; private set; } = "All (256)";
    [ObservableProperty] public partial string VisitedFilterLabel { get; private set; } = "Visited (0)";
    [ObservableProperty] public partial string UnvisitedFilterLabel { get; private set; } = "Unvisited (256)";
    [ObservableProperty] public partial string BasesFilterLabel { get; private set; } = "Bases (0)";
    [ObservableProperty] public partial string StationsFilterLabel { get; private set; } = "Stations (0)";

    public ObservableCollection<GalaxyRowViewModel> FilteredGalaxies { get; } = new();

    /// <summary>The selected galaxy's destination system (exists, has a working station); null until found.</summary>
    private ulong? _destinationAddress;
    private int _destinationVersion;

    [ObservableProperty]
    public partial string GatewayCoordinatesText { get; set; } = "Finding a station system…";

    [ObservableProperty]
    public partial string GatewayPortalGlyphs { get; set; } = "";

    public string DistanceToCoreText =>
        $"{NmsGalaxyInfo.DistanceToCoreLightYears:N0} Light-Years";

    public GalaxyTransferViewModel()
    {
    }

    /// <summary>Asks the page to scroll the list to a row: raised only when the page opens or another save is
    /// loaded, never on live updates, so the player's scroll position is kept while they read.</summary>
    public event Action<GalaxyRowViewModel>? ScrollToRequested;

    /// <summary>Reads the save and starts the list at the player's galaxy. On a reload (another save loaded while
    /// the page is open) the same 256 rows are updated in place rather than rebuilt.</summary>
    public async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            var current = GalaxyTransferService.GetCurrentGalaxy();
            var presenceMap = GalaxyTransferService.GetGalaxyPresence();
            bool isStaged = GalaxyTransferService.HasStagedTransfer();

            GalaxyPresence PresenceOf(GalaxyInfo g) =>
                presenceMap.TryGetValue(g.Number, out var pres) ? pres : GalaxyPresence.Empty(g.Number, g.Number == current.Number);

            List<GalaxyRowViewModel>? rows = _allRows.Count == 0
                ? NmsGalaxyInfo.AllGalaxies.Select(g => new GalaxyRowViewModel(g, PresenceOf(g))).ToList()
                : null;

            App.MainWindowInstance?.DispatcherQueue?.TryEnqueue(() =>
            {
                CurrentGalaxy = current;
                CurrentGalaxyTitle = $"Galaxy {current.Number}: {current.Name}";
                CurrentGalaxyClassification = current.ClassificationName;
                IsTransferStaged = isStaged;
                if (rows is not null)
                    _allRows = rows;
                else
                    foreach (var row in _allRows)
                    {
                        row.IsCurrent = row.Number == current.Number;
                        row.Presence = PresenceOf(row.Info);
                    }

                UpdateCounts();
                ApplyFilter();

                SelectedGalaxy = FilteredGalaxies.FirstOrDefault(r => r.Number == current.Number)
                    ?? FilteredGalaxies.FirstOrDefault();
                if (SelectedGalaxy is { } start)
                    ScrollToRequested?.Invoke(start);

                UpdateTransferState();
            });
        });
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedPresenceFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedGalaxyChanged(GalaxyRowViewModel? value)
    {
        _ = FindDestinationAsync(value);
    }

    /// <summary>Works out where a transfer to the selected galaxy lands; the latest selection wins.</summary>
    private async Task FindDestinationAsync(GalaxyRowViewModel? galaxy)
    {
        int version = ++_destinationVersion;
        _destinationAddress = null;
        GatewayPortalGlyphs = "";
        GatewayCoordinatesText = galaxy is null ? "" : "Finding a station system…";
        UpdateTransferState();
        if (galaxy is null) return;

        ulong? address = await GalaxyTransferService.FindDestinationAsync(galaxy.Info);
        if (version != _destinationVersion) return;
        _destinationAddress = address;
        if (address is ulong found)
        {
            int system = (int)((found >> 40) & 0xFFF);
            GatewayCoordinatesText = $"Region X: {NmsGalaxyInfo.CoreGatewayX}, Y: {NmsGalaxyInfo.CoreGatewayY}, Z: {NmsGalaxyInfo.CoreGatewayZ} · Star system {system}";
            GatewayPortalGlyphs = NmsSpaceStationPaths.PortalAddress(found, 1);
        }
        else
        {
            GatewayCoordinatesText = "Can't pick a station system yet. Read the game data again in Settings, then come back.";
        }
        UpdateTransferState();
    }

    public void SetPresenceFilter(string filter)
    {
        SelectedPresenceFilter = filter;
    }

    public void SetCategoryFilter(string filter)
    {
        SelectedCategoryFilter = filter;
    }

    private void UpdateCounts()
    {
        int total = _allRows.Count;
        int visited = _allRows.Count(r => r.IsVisited);
        int unvisited = _allRows.Count(r => r.IsUnvisited);
        int bases = _allRows.Count(r => r.HasBases);
        int stations = _allRows.Count(r => r.HasStations);

        AllFilterLabel = $"All ({total})";
        VisitedFilterLabel = $"Visited ({visited})";
        UnvisitedFilterLabel = $"Unvisited ({unvisited})";
        BasesFilterLabel = $"Bases ({bases})";
        StationsFilterLabel = $"Stations ({stations})";
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? "";
        var presenceFilter = SelectedPresenceFilter ?? "All";
        var catFilter = SelectedCategoryFilter ?? "All";

        var filtered = _allRows.Where(r =>
        {
            // Presence filter
            bool presenceMatch = presenceFilter switch
            {
                "Visited" => r.IsVisited,
                "Unvisited" => r.IsUnvisited,
                "Bases" => r.HasBases,
                "Stations" => r.HasStations,
                _ => true
            };
            if (!presenceMatch) return false;

            // Category filter
            bool categoryMatch = catFilter switch
            {
                "Lush" => r.Classification == GalaxyClassification.Lush,
                "Harsh" => r.Classification == GalaxyClassification.Harsh,
                "Empty" => r.Classification == GalaxyClassification.Empty,
                "Normal" => r.Classification == GalaxyClassification.Normal,
                _ => true
            };
            if (!categoryMatch) return false;

            // Search filter
            if (string.IsNullOrEmpty(query)) return true;

            if (r.Number.ToString().Equals(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (r.ClassificationName.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (r.PresenceSummary.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }).ToList();

        // Change only the rows that differ (both lists keep galaxy order), so the rows still showing stay where
        // they are on screen instead of the list resetting to the top.
        var keep = new HashSet<GalaxyRowViewModel>(filtered);
        for (int i = FilteredGalaxies.Count - 1; i >= 0; i--)
            if (!keep.Contains(FilteredGalaxies[i]))
                FilteredGalaxies.RemoveAt(i);
        for (int i = 0; i < filtered.Count; i++)
            if (i >= FilteredGalaxies.Count || !ReferenceEquals(FilteredGalaxies[i], filtered[i]))
                FilteredGalaxies.Insert(i, filtered[i]);

        if (SelectedGalaxy is not null && !FilteredGalaxies.Contains(SelectedGalaxy))
        {
            SelectedGalaxy = FilteredGalaxies.FirstOrDefault();
        }
    }

    private void UpdateTransferState()
    {
        IsTransferStaged = GalaxyTransferService.HasStagedTransfer();

        if (SelectedGalaxy is { } selected)
        {
            TransferActionText = $"Transfer to {selected.Name} Station";
            CanTransfer = IsTransferStaged || _destinationAddress is not null;

            if (IsTransferStaged)
            {
                StagedStatusMessage = "Transfer staged! Close No Man's Sky if running, then click Save at the top to commit changes.";
            }
            else if (selected.IsCurrent)
            {
                StagedStatusMessage = "You are currently in this galaxy. You can still transfer to its Gateway Station near the core.";
            }
            else
            {
                StagedStatusMessage = $"Ready to stage transfer to {selected.Name} ({selected.Number}).";
            }
        }
        else
        {
            TransferActionText = "Transfer Starship to Station";
            CanTransfer = false;
            StagedStatusMessage = "";
        }
    }

    [RelayCommand]
    public void StageTransfer()
    {
        if (SelectedGalaxy is not { } target || _destinationAddress is not ulong destination) return;

        GalaxyTransferService.StageTransfer(target.Info, destination);
        UpdateTransferState();
    }

    [RelayCommand]
    public void RevertTransfer()
    {
        GalaxyTransferService.RevertTransfer();
        UpdateCurrentGalaxy();
    }

    /// <summary>
    /// Quietly updates the current galaxy, telemetry, badges and filter in place without rebuilding the list
    /// or moving the scroll position. Follows live game saves and staged edits; warping to another galaxy moves
    /// the HERE badge but not the list.
    /// </summary>
    public void UpdateCurrentGalaxy(PlanetSnapshot? snapshot = null)
    {
        var current = GalaxyTransferService.GetCurrentGalaxy(snapshot);
        var presenceMap = GalaxyTransferService.GetGalaxyPresence(snapshot);
        bool isStaged = GalaxyTransferService.HasStagedTransfer();

        App.MainWindowInstance?.DispatcherQueue?.TryEnqueue(() =>
        {
            if (_allRows.Count == 0)
            {
                _ = LoadAsync();
                return;
            }

            CurrentGalaxy = current;
            CurrentGalaxyTitle = $"Galaxy {current.Number}: {current.Name}";
            CurrentGalaxyClassification = current.ClassificationName;
            IsTransferStaged = isStaged;

            foreach (var row in _allRows)
            {
                bool isCur = (row.Number == current.Number);
                if (row.IsCurrent != isCur)
                {
                    row.IsCurrent = isCur;
                }
                if (presenceMap.TryGetValue(row.Number, out var pres))
                {
                    row.Presence = pres;
                }
            }

            UpdateCounts();
            ApplyFilter();
            UpdateTransferState();
        });
    }
}
