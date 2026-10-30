using ArtifactX.Core.Recipes;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

internal static class RecipeRequirementsService
{
    public static Task<RecipePlan> BuildAsync(RecipeInfo recipe,
        IReadOnlyList<RecipeInfo> recipes, int quantity, IReadOnlyDictionary<string, string> choices, CancellationToken cancellationToken) => Task.Run(() =>
    {
        RequirementRecipe Convert(RecipeInfo r) => new(r.Id, r.Kind, r.Result.Id, r.Result.Amount,
            r.Ingredients.Select(i => new RequirementIngredient(i.Id, i.Amount)).ToArray());
        return RecipePlanner.Build(Convert(recipe), recipes.Select(Convert).ToArray(), quantity, choices, cancellationToken);
    }, cancellationToken);
}
