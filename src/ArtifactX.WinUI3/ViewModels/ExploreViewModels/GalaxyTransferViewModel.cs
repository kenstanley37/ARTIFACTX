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
    public partial string SelectedFilter { get; set; } = "All";

    [ObservableProperty]
    public partial bool IsTransferStaged { get; set; }

    [ObservableProperty]
    public partial string StagedStatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string TransferActionText { get; set; } = "Transfer Starship to Station";

    [ObservableProperty]
    public partial bool CanTransfer { get; set; }

    public ObservableCollection<GalaxyRowViewModel> FilteredGalaxies { get; } = new();

    public string GatewayCoordinatesText =>
        $"X: {NmsGalaxyInfo.CoreGatewayX}, Y: {NmsGalaxyInfo.CoreGatewayY}, Z: {NmsGalaxyInfo.CoreGatewayZ} · Solar System {NmsGalaxyInfo.CoreGatewaySystemIndex}";

    public string DistanceToCoreText =>
        $"{NmsGalaxyInfo.DistanceToCoreLightYears:N0} Light-Years";

    public string GatewayPortalGlyphs =>
        NmsGalaxyInfo.CoreGatewayPortalAddress;

    public GalaxyTransferViewModel()
    {
    }

    public async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            var current = GalaxyTransferService.GetCurrentGalaxy();
            bool isStaged = GalaxyTransferService.HasStagedTransfer();

            var rows = NmsGalaxyInfo.AllGalaxies
                .Select(g => new GalaxyRowViewModel(g, g.Number == current.Number))
                .ToList();

            App.MainWindowInstance?.DispatcherQueue?.TryEnqueue(() =>
            {
                CurrentGalaxy = current;
                CurrentGalaxyTitle = $"Galaxy {current.Number}: {current.Name}";
                CurrentGalaxyClassification = current.ClassificationName;
                IsTransferStaged = isStaged;
                _allRows = rows;

                ApplyFilter();

                SelectedGalaxy = FilteredGalaxies.FirstOrDefault(r => r.Number == current.Number)
                    ?? FilteredGalaxies.FirstOrDefault();

                UpdateTransferState();
            });
        });
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedGalaxyChanged(GalaxyRowViewModel? value)
    {
        UpdateTransferState();
    }

    public void SetFilter(string filter)
    {
        SelectedFilter = filter;
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? "";
        var filter = SelectedFilter ?? "All";

        var filtered = _allRows.Where(r =>
        {
            // Category filter
            bool categoryMatch = filter switch
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

            return false;
        }).ToList();

        FilteredGalaxies.Clear();
        foreach (var item in filtered)
            FilteredGalaxies.Add(item);

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
            CanTransfer = !selected.IsCurrent || IsTransferStaged;

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
        if (SelectedGalaxy is not { } target) return;

        GalaxyTransferService.StageTransfer(target.Info);
        UpdateTransferState();
    }

    [RelayCommand]
    public void RevertTransfer()
    {
        GalaxyTransferService.RevertTransfer();
        UpdateCurrentGalaxy();
    }

    /// <summary>
    /// Quietly updates the current galaxy and HERE badge in-place without rebuilding the list
    /// or resetting scroll position/selection. Follows live game saves and staged edits.
    /// </summary>
    public void UpdateCurrentGalaxy(PlanetSnapshot? snapshot = null)
    {
        var current = GalaxyTransferService.GetCurrentGalaxy(snapshot);
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
            }

            UpdateTransferState();
        });
    }
}

