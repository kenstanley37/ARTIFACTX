using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One folding section of the Bases page (Freighter base, Planet bases...).
/// Whether it's open is remembered; while searching, sections with matches open and
/// the rest hide, without changing what's remembered.</summary>
public sealed partial class BaseSectionViewModel : ObservableObject
{
    private readonly string _expandedKey;
    private bool _searching;

    public BaseSectionViewModel(BaseKind kind, string glyph, string emptyText)
    {
        Kind = kind;
        Glyph = glyph;
        EmptyText = emptyText;
        // Keys from the first version of the sections, so players keep what they chose.
        _expandedKey = "Bases." + kind switch
        {
            BaseKind.Freighter => "Freighter",
            BaseKind.Planet => "Planetary",
            BaseKind.Station => "Stations",
            BaseKind.Settlement => "Settlements",
            _ => "Placed",
        } + "Expanded";
        IsExpanded = AppSettingsService.GetFlag(_expandedKey, true);
    }

    public BaseKind Kind { get; }
    public string Title => BaseListBuilder.KindLabel(Kind).ToUpperInvariant();
    public string Glyph { get; }
    public string EmptyText { get; }

    /// <summary>Planet bases and placed objects can be selected for deleting (see BaseRowViewModel.CanDelete).</summary>
    public bool CanSelect => Kind is BaseKind.Planet or BaseKind.Placed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowSelectionBar))]
    public partial IReadOnlyList<BaseRowViewModel> Items { get; private set; } = [];

    [ObservableProperty]
    public partial string CountLabel { get; private set; } = "0";

    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsShown { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    public partial bool IsExpanded { get; private set; }

    public string ChevronGlyph => IsExpanded ? "" : "";
    public bool ShowEmpty => Items.Count == 0 && !_searching;
    public bool ShowSelectionBar => CanSelect && Items.Count > 0;

    public void Toggle()
    {
        IsExpanded = !IsExpanded;
        if (!_searching) AppSettingsService.SetFlag(_expandedKey, IsExpanded);
    }

    public void Show(IReadOnlyList<BaseRowViewModel> shown, int total, bool searching)
    {
        _searching = searching;
        Items = shown;
        CountLabel = searching ? $"{shown.Count} of {total}" : $"{shown.Count}";
        IsShown = !searching || shown.Count > 0;
        IsExpanded = searching ? shown.Count > 0 || AppSettingsService.GetFlag(_expandedKey, true) : AppSettingsService.GetFlag(_expandedKey, true);
        int parts = shown.Sum(r => r.Entry.Parts);
        Summary = shown.Count == 0 ? EmptyText : Kind switch
        {
            BaseKind.Freighter => $"{parts:N0} parts placed",
            BaseKind.Planet => $"{parts:N0} parts placed across {shown.Count:N0} bases",
            BaseKind.Station => $"{parts:N0} parts placed across {shown.Count:N0} stations",
            BaseKind.Settlement => shown.Count == 1 ? "1 settlement" : $"{shown.Count:N0} settlements",
            _ => shown.Count == 1 ? "1 object placed outside a base" : $"{shown.Count:N0} objects placed outside bases",
        };
        OnPropertyChanged(nameof(ShowEmpty));
    }

    public void SelectAll()
    {
        foreach (var row in Items.Where(r => r.CanDelete)) row.IsSelected = true;
    }

    public void DeselectAll()
    {
        foreach (var row in Items) row.IsSelected = false;
    }
}
