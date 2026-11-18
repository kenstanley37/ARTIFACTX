namespace ArtifactX.Core.Recipes;

public sealed record RequirementRecipe(string Id, string Kind, string ResultId, int ResultAmount,
    IReadOnlyList<RequirementIngredient> Ingredients);
public sealed record RequirementIngredient(string Id, int Amount);

/// <summary>The machine a recipe runs on, as a product id whose display name is shown: the Nutrient Processor
/// (COOKER) for cooking, otherwise the refiner with enough input slots (BUILD_REFINER1-3, one per ingredient).</summary>
public static class RecipeStations
{
    public static string MachineId(string kind, int ingredientCount) =>
        kind == "Cooking" ? "COOKER" : "BUILD_REFINER" + Math.Clamp(ingredientCount, 1, 3);
}
