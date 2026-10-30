using System.Collections.Generic;
namespace ArtifactX.WinUI3.ViewModels;

public sealed record RecipePlanChoice(string Id, string Label)
{
    public override string ToString() => Label;
}
public sealed class RecipePlanCardViewModel
{
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public RecipePlanCardViewModel()
    {
        Heading = Station = UsedFor = ItemId = "";
        Ingredients = new();
        Result = new() { ItemId = "", Label = "" };
        Choices = new();
        SelectedChoice = null;
        ChoiceVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public required string Heading { get; init; }
    public required string Station { get; init; }
    public required string UsedFor { get; init; }
    public required string ItemId { get; init; }
    public required List<RecipeItemViewModel> Ingredients { get; init; }
    public required RecipeItemViewModel Result { get; init; }
    public required List<RecipePlanChoice> Choices { get; init; }
    public required RecipePlanChoice? SelectedChoice { get; init; }
    public required Microsoft.UI.Xaml.Visibility ChoiceVisibility { get; init; }
}
