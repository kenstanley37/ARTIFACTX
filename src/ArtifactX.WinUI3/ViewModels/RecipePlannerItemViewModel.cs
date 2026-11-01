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
    public string Source { get; init; } = "";
    public string UsedFor { get; init; } = "";
    public int Level { get; init; }
    public long Required { get; init; }
    public IReadOnlyList<RecipePlannerItemViewModel> Ingredients { get; init; } = [];
    public IReadOnlyList<RecipePlanChoice> Choices { get; init; } = [];
    public RecipePlanChoice? SelectedChoice { get; init; }
    public Visibility ChoiceVisibility => Choices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RecipeVisibility => RecipeId.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SourceVisibility => RecipeId.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    [ObservableProperty] public partial string Inventory { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CardBorder))] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial bool IsPinned { get; set; }
    public IRelayCommand PinCommand { get; init; } = new RelayCommand(() => { });
    public IRelayCommand SelectCommand { get; init; } = new RelayCommand(() => { });
    public IRelayCommand ExploreCommand { get; init; } = new RelayCommand(() => { });
    public string SelectionName => $"Show {Name} requirements";
    public string PinName => $"Pin or unpin {Name}";
    public Brush CardBorder => ThemeResources.Brush(IsSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
}

public sealed record RecipePlannerEffect(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record RecipePlannerCrumb(string Name, IRelayCommand NavigateCommand);
public sealed record RecipePlanChoice(string Id, string Label)
{
    public override string ToString() => Label;
}
public sealed record RecipePlannerLevel(ObservableCollection<RecipePlannerItemViewModel> Cards);
