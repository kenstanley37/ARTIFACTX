using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// BaseDetailsPage: one base's parts, one row per kind, biggest first (what to thin out
/// when a base is near its limit), with where it is. Planet bases and the freighter can
/// have kinds of part deleted; a planet base can be deleted outright, and so can a loose
/// placed object.
/// </summary>
public sealed partial class BaseDetailsViewModel : ObservableObject
{
    private List<BasePartViewModel> _all = new();
    private JArray? _parts;

    public BaseDetailsViewModel(BaseRowViewModel row)
    {
        Row = row;
        PartsLabel = row.PartsLabel;
    }

    public BaseRowViewModel Row { get; }

    public bool IsSettlement => Row.Kind == BaseKind.Settlement;
    public bool IsPlaced => Row.Kind == BaseKind.Placed;
    public bool HasParts => !IsPlaced;

    /// <summary>Parts can be deleted from planet bases and the freighter. A claimed
    /// station's parts go with its claim, and a settlement's are gathered from its planet.</summary>
    public bool CanEditParts => Row.Kind is BaseKind.Planet or BaseKind.Freighter;
    public bool CanDeleteBase => Row.Kind == BaseKind.Planet;
    public bool CanDeleteObject => IsPlaced;

    [ObservableProperty]
    public partial string PartsLabel { get; private set; }

    [ObservableProperty]
    public partial string KindsLabel { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<BasePartViewModel> Parts { get; private set; } = [];

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial string FilterLabel { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(DeleteLabel))]
    public partial int SelectedParts { get; private set; }

    public bool HasSelection => SelectedParts > 0;
    public string DeleteLabel => SelectedParts > 0 ? $"Delete selected ({SelectedParts:N0} parts)" : "Delete selected";

    public IReadOnlyList<BasePartViewModel> SelectedKinds => _all.Where(p => p.IsSelected).ToList();

    partial void OnFilterChanged(string value) => Apply();

    public async Task LoadAsync()
    {
        if (Row.Base is not JObject b) return;
        _parts = b["@ZJ"] as JArray ?? new JArray();
        var ids = _parts.OfType<JObject>().Select(o => o["r<7"]?.Value<string>() ?? "").Where(id => id.Length > 0).Distinct().ToList();
        await CatalogService.WarmCacheAsync(ids);
        ShowParts();
    }

    public void SelectAll()
    {
        foreach (var part in Parts.Where(p => p.CanSelect)) part.IsSelected = true;
    }

    public void DeselectAll()
    {
        foreach (var part in _all) part.IsSelected = false;
    }

    /// <summary>Deletes every part of the selected kinds. False when the base wasn't found in the loaded save.</summary>
    public bool DeleteSelectedParts()
    {
        if (!CanEditParts || Row.Base is null) return false;
        var ids = SelectedKinds.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0 || BaseEditService.RemovePartKinds(Row.Base, ids) is not JArray kept) return false;
        _parts = kept;
        int parts = kept.OfType<JObject>().Count(o => o["r<7"]?.Value<string>() != NmsBuildCounts.BaseComputerId);
        PartsLabel = NmsBuildCounts.UploadLabel(parts);
        ShowParts();
        return true;
    }

    public bool DeleteBase() => CanDeleteBase && Row.Base is not null && BaseEditService.RemoveBases([Row.Base]);

    public bool DeleteObject() => CanDeleteObject && Row.LooseObject is not null && BaseEditService.RemoveLooseObjects([Row.LooseObject]);

    private void ShowParts()
    {
        foreach (var part in _all) part.PropertyChanged -= Part_PropertyChanged;
        // The base computer isn't listed: it's the one part that isn't built (the game's
        // count leaves it out too), and removing it is deleting the base - its own button.
        var groups = (_parts ?? new JArray()).OfType<JObject>()
            .Select(o => o["r<7"]?.Value<string>() ?? "")
            .Where(id => id.Length > 0 && !BasePartNames.IsBaseComputer(id))
            .GroupBy(id => id)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
        // Kinds sharing a name are numbered as the game numbers them: CONTAINER0..9 are ten
        // separate "Storage Container"s, told apart only by the digit on their icons (2026-10-18).
        var shared = groups.GroupBy(g => BasePartNames.Name(g.Key)).Where(n => n.Count() > 1).Select(n => n.Key).ToHashSet();
        _all = groups.Select(g =>
        {
            string name = BasePartNames.Name(g.Key);
            if (shared.Contains(name) && Regex.Match(g.Key, @"\d+$") is { Success: true } number) name = $"{name} {number.Value}";
            var part = new BasePartViewModel
            {
                Id = g.Key,
                Name = name,
                Count = g.Count(),
                Icon = CatalogService.TryGet(g.Key)?.Icon,
                Glyph = BasePartNames.Glyph(g.Key),
                CanSelect = CanEditParts && !BasePartNames.IsBuiltIn(g.Key),
            };
            part.PropertyChanged += Part_PropertyChanged;
            return part;
        }).ToList();
        KindsLabel = _all.Count == 1 ? "1 kind of part" : $"{_all.Count:N0} kinds of part";
        SelectedParts = 0;
        Apply();
    }

    private void Apply()
    {
        string filter = Filter.Trim();
        Parts = filter.Length == 0 ? _all : _all.Where(p => p.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();
        FilterLabel = filter.Length == 0 ? "" : $"{Parts.Count:N0} of {_all.Count:N0} kinds";
    }

    private void Part_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BasePartViewModel.IsSelected))
            SelectedParts = _all.Where(p => p.IsSelected).Sum(p => p.Count);
    }
}
