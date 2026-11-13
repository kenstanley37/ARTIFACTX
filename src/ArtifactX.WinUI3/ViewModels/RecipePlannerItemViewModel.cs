using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One ingredient or cooking step of the open recipe: a tile in the tree, a Shopping list row and what the
/// Selected step panel shows. Raw ingredients (no recipe of their own) show what you have against what the recipe
/// needs; steps show the equipment that makes them.</summary>
public sealed partial class RecipePlannerItemViewModel : ObservableObject
{
    public string Id { get; init; } = "";
    public string TreeNodeId { get; init; } = "";
    public string TreeKey => TreeNodeId.Length > 0 ? TreeNodeId : Id;
    public string RecipeId { get; init; } = "";
    public string Name { get; init; } = "";
    public ImageSource? Icon { get; init; }
    public string Station { get; init; } = "";
    public ImageSource? StationIcon { get; init; }
    /// <summary>The verb on a step's tile: "Cook" or "Refine".</summary>
    public string Verb { get; init; } = "Cook";
    public string Amount { get; init; } = "";
    public string Buff { get; init; } = "";
    public Visibility BuffVisibility => Buff.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    /// <summary>The game's own description of the item (how the game says to get it).</summary>
    public string Source { get; init; } = "";
    public Visibility SourceVisibility => Source.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string UsedFor { get; init; } = "";
    public long Required { get; init; }
    /// <summary>A starting supply a circular conversion needs (gathered or stored, not cooked here).</summary>
    public bool IsSupply { get; init; }
    public bool IsRaw => RecipeId.Length == 0;
    public IReadOnlyList<RecipePlannerItemViewModel> Ingredients { get; init; } = [];
    public IReadOnlyList<RecipePlannerAlternative> Alternatives { get; init; } = [];
    /// <summary>A step with other recipes, or a raw ingredient that some recipe also makes.</summary>
    public Visibility AlternativesVisibility => Alternatives.Count > (IsRaw ? 0 : 1) ? Visibility.Visible : Visibility.Collapsed;
    public string AlternativesHeading => IsRaw ? "YOU CAN ALSO MAKE IT" : "OTHER WAYS TO MAKE IT";
    public Visibility UsedForVisibility => UsedFor.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RecipeVisibility => IsRaw ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RawVisibility => IsRaw ? Visibility.Visible : Visibility.Collapsed;
    public IRelayCommand SelectCommand { get; init; } = new RelayCommand(() => { });
    [ObservableProperty] public partial bool IsPinned { get; set; }
    public IRelayCommand PinCommand { get; init; } = new RelayCommand(() => { });
    public string PinName => $"Pin or unpin {Name}";

    /// <summary>How many the save holds (null without a loaded save).</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TileStatus), nameof(TileStatusBrush), nameof(TileName), nameof(HaveText), nameof(HaveEnough))]
    public partial long? Owned { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(InventoryLocationsVisibility))] public partial string InventoryLocations { get; set; } = "";
    public Visibility InventoryLocationsVisibility => InventoryLocations.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(BaseSourcesVisibility))] public partial string BaseSources { get; set; } = "";
    public Visibility BaseSourcesVisibility => BaseSources.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool HaveEnough => Owned is long owned && owned >= Required;
    /// <summary>Under a tile's name: "133 / 2" for a raw ingredient, the verb for a step.</summary>
    public string TileStatus => IsRaw ? Owned is long owned ? $"{owned:N0} / {Required:N0}" : $"Need {Required:N0}" : Verb;
    /// <summary>Green when you have enough, amber when some, red when none; dim for steps and without a save.</summary>
    public Brush TileStatusBrush => ThemeResources.Brush(!IsRaw || Owned is null ? "ThemeTextDimBrush"
        : HaveEnough ? "ThemeDoneBrush" : Owned > 0 ? "ThemeWarningBrush" : "ThemeDamagedBrush");
    public string HaveText => Owned is long owned
        ? IsRaw ? $"You have {owned:N0} · the recipe needs {Required:N0}" : owned > 0 ? $"You have {owned:N0} already" : "Make it from the steps below"
        : IsRaw ? $"The recipe needs {Required:N0}" : "Make it from the steps below";
    public string TileName => IsRaw
        ? Owned is long owned ? $"{Name}, you have {owned:N0}, need {Required:N0}" : $"{Name}, need {Required:N0}"
        : $"{Name}, {Verb.ToLowerInvariant()} it";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CardBorder), nameof(CardBorderThickness))]
    public partial bool IsSelected { get; set; }
    public Brush CardBorder => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness CardBorderThickness => new(IsSelected ? 2 : 1);
}

/// <summary>A node of the ingredient tree: an item and the ingredients it is made from (the same item can appear
/// under several steps). Last ends the connector line at its tile.</summary>
public sealed class RecipeTreeNode
{
    public RecipePlannerItemViewModel Item { get; init; } = new();
    public IReadOnlyList<RecipeTreeNode> Children { get; init; } = [];
    public bool IsLast { get; init; } = true;
    public Visibility ChildrenVisibility => Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LowerLineVisibility => IsLast ? Visibility.Collapsed : Visibility.Visible;
}

public sealed record RecipePlannerEffect(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record RecipePlannerAlternative(string Label, string Equipment, string ActionName, IRelayCommand ChooseCommand, bool IsCurrent)
{
    public string ButtonText => IsCurrent ? "In use" : "Use this recipe";
    public bool CanChoose => !IsCurrent;
}
