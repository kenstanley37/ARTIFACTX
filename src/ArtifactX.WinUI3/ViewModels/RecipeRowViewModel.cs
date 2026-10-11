using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item in a recipe (ingredient or result): icon, amount, name.</summary>
public sealed class RecipeItemViewModel
{
    public required string ItemId { get; init; }
    public required string Label { get; init; }
    public ImageSource? Icon { get; init; }
}

/// <summary>One row on RecipesPage. Read-only.</summary>
public sealed class RecipeRowViewModel
{
    public required List<RecipeItemViewModel> Ingredients { get; init; }
    public required RecipeItemViewModel Result { get; init; }
    public required string Name { get; init; }
    public required string Station { get; init; }
}
