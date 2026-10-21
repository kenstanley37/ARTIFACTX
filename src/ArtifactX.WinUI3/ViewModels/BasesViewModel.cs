using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>The Bases page: every base, settlement and placed object in sections, with
/// search, sort, the parts count against the game's limit, and selection for deleting.</summary>
public sealed partial class BasesViewModel : ObservableObject
{
    // Pictures by address, kept while the page is open (a base's picture doesn't change).
    private readonly Dictionary<ulong, WriteableBitmap?> _pictures = new();
    private List<BaseRowViewModel> _rows = new();

    public BasesViewModel()
    {
        Sections =
        [
            new(BaseKind.Freighter, "", "No freighter base in this save"),
            new(BaseKind.Planet, "", "No planet bases in this save"),
            new(BaseKind.Station, "", "No claimed space stations in this save"),
            new(BaseKind.SpaceBase, "", "No space bases in this save"),
            new(BaseKind.Corvette, "", "No corvette builds in this save"),
            new(BaseKind.Settlement, "", "No settlements in this save"),
            new(BaseKind.Placed, "", "Nothing placed outside a base"),
        ];
    }

    public IReadOnlyList<BaseSectionViewModel> Sections { get; }
    public IReadOnlyList<BaseRowViewModel> Rows => _rows;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    /// <summary>0 nearest first, 1 name, 2 galaxy.</summary>
    [ObservableProperty]
    public partial int SortIndex { get; set; }

    [ObservableProperty]
    public partial string CountLabel { get; private set; } = "";

    [ObservableProperty]
    public partial string PartsTotalLabel { get; private set; } = "";

    [ObservableProperty]
    public partial Brush? PartsTotalBrush { get; private set; }

    [ObservableProperty]
    public partial string PartsBreakdown { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(DeleteLabel))]
    public partial int SelectedCount { get; private set; }

    public bool HasSelection => SelectedCount > 0;
    public string DeleteLabel => SelectedCount > 0 ? $"Delete selected ({SelectedCount:N0})" : "Delete selected";

    public IEnumerable<BaseRowViewModel> Selected => _rows.Where(r => r.IsSelected && r.CanDelete);

    partial void OnSearchChanged(string value) => Apply();
    partial void OnSortIndexChanged(int value) => Apply();

    public void Show(BaseList list)
    {
        foreach (var row in _rows) row.PropertyChanged -= Row_PropertyChanged;
        _rows = list.Entries.Select(e => new BaseRowViewModel(e)).ToList();
        foreach (var row in _rows)
        {
            row.PropertyChanged += Row_PropertyChanged;
            if (_pictures.TryGetValue(row.Entry.Address, out var picture)) row.Picture = picture;
        }

        PartsTotalLabel = $"Building parts placed: {NmsBuildCounts.GlobalLabel(list.PartsTotal)} before the game's construction limit";
        PartsTotalBrush = ThemeResources.LevelBrush(list.PartsLevel);
        PartsBreakdown = list.PartsBreakdown;
        SelectedCount = 0;
        Apply();
        IsLoading = false;
    }

    /// <summary>The pictures the game uploaded of bases, loaded one address at a time.
    /// Call on the UI thread (the bitmaps are made there).</summary>
    public async Task LoadPicturesAsync(string? platformFolder)
    {
        foreach (var group in _rows.Where(r => r.Entry.ScreenshotName.Length > 0 && r.Entry.Address != 0)
                     .GroupBy(r => r.Entry.Address).ToList())
        {
            if (!_pictures.TryGetValue(group.Key, out var picture))
            {
                picture = await GameScreenshotCache.LoadAsync(platformFolder, group.Key, group.First().Entry.ScreenshotName);
                _pictures[group.Key] = picture;
            }
            if (picture is null) continue;
            foreach (var row in group) row.Picture = picture;
        }
    }

    public void DeselectAll()
    {
        foreach (var row in _rows) row.IsSelected = false;
    }

    /// <summary>The rows shown, searched and sorted, in section order - for the CSV export too.</summary>
    public List<BaseRowViewModel> Shown()
    {
        string search = Search.Trim();
        var shown = _rows.Where(r => r.Matches(search));
        shown = SortIndex switch
        {
            1 => shown.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            2 => shown.OrderBy(r => r.Entry.Galaxy).ThenBy(r => r.Entry.SortDistance),
            _ => shown.OrderBy(r => r.Entry.SortDistance).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        return shown.OrderBy(r => r.Kind).ToList(); // stable: keeps the sort within each section
    }

    private void Apply()
    {
        bool searching = Search.Trim().Length > 0;
        var shown = Shown();
        foreach (var section in Sections)
            section.Show(shown.Where(r => r.Kind == section.Kind).ToList(), _rows.Count(r => r.Kind == section.Kind), searching);
        CountLabel = _rows.Count == 0 ? "No bases or placed objects in this save"
            : $"{shown.Count:N0} of {_rows.Count:N0} bases, settlements and placed objects";
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BaseRowViewModel.IsSelected)) SelectedCount = Selected.Count();
    }
}
