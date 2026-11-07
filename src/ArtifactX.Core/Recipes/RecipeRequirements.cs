namespace ArtifactX.Core.Recipes;

public sealed record RequirementRecipe(string Id, string Kind, string ResultId, int ResultAmount,
    IReadOnlyList<RequirementIngredient> Ingredients);
public sealed record RequirementIngredient(string Id, int Amount);
