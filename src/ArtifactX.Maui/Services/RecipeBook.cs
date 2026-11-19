using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.Services;

/// <summary>
/// The refiner and cooking recipes from the rules file, with the names, machines and Nutrient Ingestor effects the
/// phone shows - the same words as the desktop's recipe pages (Core RecipeStations, FoodIngestorDisplay). Built once,
/// off the UI thread; null when the app has no rules file or it has no recipes.
/// </summary>
public sealed class RecipeBook
{
    private readonly GameRules _rules;
    private readonly Dictionary<string, (FoodIngestorDefinition Food, float Bonus)> _foods;

    private RecipeBook(GameRules rules)
    {
        _rules = rules;
        Recipes = rules.Recipes;
        ById = rules.Recipes.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        // FoodIngestorBonus hashes a 16-byte id, as the game does; longer ids have no Ingestor effect shown (as desktop).
        _foods = (rules.Food?.Foods ?? []).Where(f => f.Id.Length <= 16)
            .GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (g.First(), FoodIngestorBonus.Calculate(g.First())), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RequirementRecipe> Recipes { get; }
    public IReadOnlyDictionary<string, RequirementRecipe> ById { get; }

    public string Name(string id) => _rules.ItemName(id) ?? "Unknown ingredient";

    /// <summary>The machine the recipe runs on ("Nutrient Processor", "Refiner").</summary>
    public string Station(RequirementRecipe recipe) =>
        _rules.ItemName(RecipeStations.MachineId(recipe.Kind, recipe.Ingredients.Count)) ?? "Required equipment";

    /// <summary>"1 Ferrite Dust + 1 Carbon → 1 Pure Ferrite".</summary>
    public string Formula(RequirementRecipe recipe) =>
        string.Join(" + ", recipe.Ingredients.Select(i => $"{i.Amount:N0} {Name(i.Id)}")) + $" → {recipe.ResultAmount:N0} {Name(recipe.ResultId)}";

    /// <summary>The food's Ingestor effect, or null for an item that isn't a food.</summary>
    public (FoodIngestorDefinition Food, float Bonus)? Food(string id) => _foods.TryGetValue(id, out var food) ? food : null;

    /// <summary>The Ingestor line ("+65% Cold resistance · 2:30 per item"), or "" for an item that isn't a food.</summary>
    public string Buff(string id) =>
        Food(id) is { } food ? FoodIngestorDisplay.Summary(food.Food, food.Bonus, _rules.Food?.BaseDuration ?? 0) : "";

    private static Task<RecipeBook?>? _load;

    public static Task<RecipeBook?> GetAsync() => _load ??= Task.Run(async () =>
        await GameRulesService.GetAsync() is { Recipes.Count: > 0 } rules ? new RecipeBook(rules) : null);
}
