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

    public GameCatalogSectionViewModel(CatalogueSection section, List<GameCatalogCategoryViewModel> categories, UnlockListViewModel? list)
    {
        _section = section;
        Categories = categories;
        List = list;
        Refresh();
    }

    public string Key => _section.Key;
    public string Title => _section.Title;
    public List<GameCatalogCategoryViewModel> Categories { get; }
    public UnlockListViewModel? List { get; }

    /// <summary>Recipes and Wonders list no items in the game's files - the game fills them at run time.</summary>
    public bool IsAvailable => Categories.Any(c => c.Items.Count > 0);
    public bool TracksKnown => List is not null;
    public string AutomationName => $"{Title}, {CountText}";

    [ObservableProperty] public partial string CountText { get; private set; } = "";
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public void NotifySelected() => OnPropertyChanged(nameof(IsSelected));

    public IEnumerable<GameCatalogItemViewModel> AllItems => Categories.SelectMany(c => c.Items);

    public void Refresh()
    {
        foreach (var category in Categories) category.Refresh(TracksKnown);
        int total = Categories.Sum(c => c.Items.Count);
        if (!IsAvailable) CountText = "Not shown in ArtifactX yet";
        else if (!TracksKnown) CountText = $"{total:N0} items";
        else
        {
            int known = Categories.Sum(c => c.Items.Count(i => i.IsUnlocked));
            CountText = _section.KnownLabel.Replace("%CURRENT%", known.ToString("N0")).Replace("%AMOUNT%", total.ToString("N0"));
        }
        OnPropertyChanged(nameof(AutomationName));
    }
}
