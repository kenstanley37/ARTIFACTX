using ArtifactX.Core.Unlocks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One section tile of the Catalog page (the game's Materials &amp; Items,
/// Technology, Building Parts...), with the game's own count line ("Blueprints Known:
/// 964/1,024"). List is the save list its edits go to; null where none is mapped yet.</summary>
public sealed partial class GameCatalogSectionViewModel : ObservableObject
{
    private readonly CatalogueSection _section;

    public GameCatalogSectionViewModel(CatalogueSection section, List<GameCatalogCategoryViewModel> categories, UnlockListViewModel? list,
        bool tracksKnown)
    {
        _section = section;
        Categories = categories;
        List = list;
        TracksKnown = tracksKnown;
        Refresh();
    }

    public string Key => _section.Key;
    public string Title => _section.Title;
    public List<GameCatalogCategoryViewModel> Categories { get; }
    public UnlockListViewModel? List { get; }

    /// <summary>Recipes and Wonders list no items in the game's files - the game fills them at run time.</summary>
    public bool IsAvailable => Categories.Any(c => c.Items.Count > 0);
    /// <summary>Known state is read (a list, or Wonders' records); editable only with a List.</summary>
    public bool TracksKnown { get; }
    public string AutomationName => $"{Title}, {CountText}";

    [ObservableProperty] public partial string CountText { get; private set; } = "";
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public void NotifySelected() => OnPropertyChanged(nameof(IsSelected));

    public IEnumerable<GameCatalogItemViewModel> AllItems => Categories.SelectMany(c => c.Items);

    public void Refresh()
    {
        foreach (var category in Categories) category.Refresh(TracksKnown);
        var counted = Categories.Where(c => c.Counted).ToList();
        int total = counted.Sum(c => c.Items.Count);
        if (!IsAvailable) CountText = "Not shown in ArtifactX yet";
        else if (!TracksKnown) CountText = $"{total:N0} items";
        else
        {
            int known = counted.Sum(c => c.Items.Count(i => i.IsUnlocked));
            CountText = _section.KnownLabel.Replace("%CURRENT%", known.ToString("N0")).Replace("%AMOUNT%", total.ToString("N0"));
        }
        OnPropertyChanged(nameof(AutomationName));
    }
}
