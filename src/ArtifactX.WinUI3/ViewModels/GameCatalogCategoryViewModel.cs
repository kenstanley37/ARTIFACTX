using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One subcategory in the Catalog page's left list ("Space Station Exterior"),
/// in the game's order, with how many of its items are known.</summary>
public sealed partial class GameCatalogCategoryViewModel : ObservableObject
{
    public GameCatalogCategoryViewModel(string name, List<GameCatalogItemViewModel> items, bool counted = true)
    {
        Name = name;
        Items = items;
        Counted = counted;
    }

    public string Name { get; }

    /// <summary>False for Personal Records, which the game leaves out of its Wonders total.</summary>
    public bool Counted { get; }
    public List<GameCatalogItemViewModel> Items { get; }

    [ObservableProperty] public partial string CountText { get; private set; } = "";

    public void Refresh(bool tracksKnown) =>
        CountText = tracksKnown ? $"{Items.Count(i => i.IsUnlocked):N0}/{Items.Count:N0}" : $"{Items.Count:N0}";
}
