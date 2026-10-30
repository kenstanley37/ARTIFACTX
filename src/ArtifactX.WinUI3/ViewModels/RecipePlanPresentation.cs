using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

internal static class RecipePlanPresentation
{
    public static List<RecipePlanCardViewModel> Create(RecipePlan plan, string targetId,
        IReadOnlyList<RecipeInfo> recipes, IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, string> sources)
    {
        string Name(string id) => names.GetValueOrDefault(id) ?? "Unknown ingredient";
        RecipeItemViewModel Item(string id, long amount) => new()
        { ItemId = id, Label = $"{amount:N0} x {Name(id)}", Icon = CatalogService.TryGet(id)?.Icon };
        var startingCards = plan.Gather.Select(ingredient => new RecipePlanCardViewModel
        {
            Heading = $"GET - {Name(ingredient.Id)}",
            Station = recipes.Any(r => r.Result.Id.Equals(ingredient.Id, StringComparison.OrdinalIgnoreCase))
                ? "Starting supply needed for this route" : "No cooking or refining recipe produces this ingredient",
            SourceDescription = sources.TryGetValue(ingredient.Id, out string? description) && !string.IsNullOrWhiteSpace(description)
                ? description : "The game catalog does not describe how to obtain this ingredient.",
            ItemId = ingredient.Id,
            Result = Item(ingredient.Id, ingredient.Amount),
            UsedFor = "Needed for " + string.Join(", ", plan.Steps.Select((step, index) => (step, index))
                .Where(x => x.step.Recipe.Ingredients.Any(i => i.Id.Equals(ingredient.Id, StringComparison.OrdinalIgnoreCase)))
                .Select(x => $"step {x.index + 1}")),
        }).ToList();
        return startingCards.Concat(plan.Steps.Select((step, index) =>
        {
            var recipe = step.Recipe;
            var choices = recipes.Where(r => step.Alternatives.Contains(r.Id)).Select(r => new RecipePlanChoice(r.Id,
                string.Join(" + ", r.Ingredients.Select(i => $"{i.Amount} x {Name(i.Id)}")) + $" = {r.Result.Amount} x {Name(r.Result.Id)}")).ToList();
            string[] uses = plan.Steps.Select((s, i) => (s, i)).Where(x => x.i > index && x.s.Recipe.Ingredients.Any(i => i.Id.Equals(recipe.ResultId, StringComparison.OrdinalIgnoreCase)))
                .Select(x => $"step {x.i + 1}").ToArray();
            return new RecipePlanCardViewModel
            {
                Heading = $"STEP {index + 1} - {Name(recipe.ResultId)}",
                Station = $"{(recipe.Kind == "Cooking" ? "Nutrient Processor" : "Refiner")} - run {step.Batches:N0} batch{(step.Batches == 1 ? "" : "es")}",
                UsedFor = index == plan.Steps.Count - 1 ? "Your requested recipe" : $"Needed for {string.Join(", ", uses)}",
                ItemId = recipe.ResultId,
                Ingredients = recipe.Ingredients.Select(i => Item(i.Id, checked(step.Batches * i.Amount))).ToList(),
                Result = Item(recipe.ResultId, checked(step.Batches * Math.Max(1, recipe.ResultAmount))),
                Choices = choices,
                SelectedChoice = choices.FirstOrDefault(c => c.Id == recipe.Id),
                ChoiceVisibility = recipe.Id != targetId && choices.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
            };
        })).ToList();
    }
}
