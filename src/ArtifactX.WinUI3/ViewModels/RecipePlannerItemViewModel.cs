using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class RecipePlannerItemViewModel : ObservableObject
{
    public string Id { get; init; } = "";
    public string RecipeId { get; init; } = "";
    public string Name { get; init; } = "";
    public ImageSource? Icon { get; init; }
    public string Heading { get; init; } = "";
    public string Station { get; init; } = "";
    public ImageSource? StationIcon { get; init; }
    public string Amount { get; init; } = "";
    public string Buff { get; init; } = "";
    public Visibility BuffVisibility => Buff.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string Source { get; init; } = "";
    public string UsedFor { get; init; } = "";
    public int Level { get; init; }
    public long Required { get; init; }
    public IReadOnlyList<RecipePlannerItemViewModel> Ingredients { get; init; } = [];
    public IReadOnlyList<RecipePlanChoice> Choices { get; init; } = [];
    public IReadOnlyList<RecipePlannerAlternative> Alternatives { get; init; } = [];
    public Visibility AlternativesVisibility => Alternatives.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public RecipePlanChoice? SelectedChoice { get; init; }
    public Visibility ChoiceVisibility => Choices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RecipeVisibility => RecipeId.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SourceVisibility => RecipeId.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    [ObservableProperty] public partial string Inventory { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CardBorder), nameof(CardBackground), nameof(CardBorderThickness), nameof(CardPadding), nameof(SelectionVisibility), nameof(SelectionName))]
    public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial bool IsPinned { get; set; }
    public IRelayCommand PinCommand { get; init; } = new RelayCommand(() => { });
    public IRelayCommand SelectCommand { get; init; } = new RelayCommand(() => { });
    public IRelayCommand ExploreCommand { get; init; } = new RelayCommand(() => { });
    public string SelectionName => IsSelected ? $"{Name}, selected. Details shown in the ingredient panel." : $"Show {Name} requirements";
    public string PinName => $"Pin or unpin {Name}";
    public Brush CardBorder => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Brush CardBackground => ThemeResources.Brush(IsSelected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush");
    public Thickness CardBorderThickness => new(IsSelected ? 3 : 1);
    public Thickness CardPadding => new(IsSelected ? 10 : 12);
    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
}

public sealed record RecipePlannerEffect(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record RecipePlannerCrumb(string Name, IRelayCommand NavigateCommand);
public sealed record RecipePlannerAlternative(string Label, string Equipment, string ActionName, IRelayCommand ChooseCommand);
public sealed record RecipePlanChoice(string Id, string Label)
{
    public override string ToString() => Label;
}
public sealed record RecipePlannerLevel(ObservableCollection<RecipePlannerItemViewModel> Cards);
