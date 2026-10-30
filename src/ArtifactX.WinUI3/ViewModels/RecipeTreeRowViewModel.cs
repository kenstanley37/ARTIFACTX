using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in RecipeTreePage's requirements tree - either an item
/// needed at some step, or a "make this via [station] × N batches" step.
/// Built once and never changes, so plain init properties.</summary>
public sealed class RecipeTreeRowViewModel
{
    public required string Label { get; init; }         // item name or step text
    public ImageSource? Icon { get; init; }
    public required string Amount { get; init; }        // "× 2" on item rows, empty on step rows
    public required int Depth { get; init; }
    public required Thickness Indent { get; init; }
    public required string Badge { get; init; }         // RAW / REFINER / COOKING
    public required Brush BadgeBrush { get; init; }
    public required string ToolTip { get; init; }
    public required bool IsRecipeStep { get; init; }

    public Visibility ItemVisibility => IsRecipeStep ? Visibility.Collapsed : Visibility.Visible;
    public Visibility StepVisibility => IsRecipeStep ? Visibility.Visible : Visibility.Collapsed;
}