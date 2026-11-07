using ArtifactX.Core.Recipes;

static RequirementRecipe Recipe(string id, string result, int output, params RequirementIngredient[] ingredients)
    => new(id, "Refining", result, output, ingredients);
static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
var root = Recipe("root", "A", 2, new RequirementIngredient("B", 3));
var child = Recipe("child", "B", 4, new RequirementIngredient("Raw", 5));
var cycle = Recipe("cycle", "b", 1, new RequirementIngredient("a", 1));
var plan = RecipePlanner.Build(root, [root, child], 5, new Dictionary<string, string>());
Require(plan.Steps.Count == 2 && plan.Steps[0].Recipe.Id == "child" && plan.Steps[1].Recipe.Id == "root", "Dependency-first recipe cards");
Require(plan.Gather.Single().Amount == 15, "Starting ingredients match scaled batches");
var alternative = Recipe("alternative", "B", 1, new RequirementIngredient("OtherRaw", 2));
plan = RecipePlanner.Build(root, [root, child, alternative], 1,
    new Dictionary<string, string> { ["B"] = "alternative" });
Require(plan.Steps.Count == 2 && plan.Gather.Single().Id == "OtherRaw", "Only the selected alternative contributes requirements");
Require(plan.Steps[0].Alternatives.Length == 2, "Alternative recipes retained for selection");
plan = RecipePlanner.Build(root, [root, cycle], 1, new Dictionary<string, string>());
Require(plan.Steps.Count == 1 && plan.Notice.Length > 0, "Circular-only ingredient requires gathering");
try
{
    RecipePlanner.Build(root, [root, cycle], 1, new Dictionary<string, string> { ["B"] = "cycle" });
    throw new Exception("Selected cycle accepted");
}
catch (InvalidOperationException) { }
Console.WriteLine("Recipe card plan checks passed: order, scaled shopping list, alternatives and cycles.");

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try { RecipePlanner.Build(root, [root, child], 1, new Dictionary<string, string>(), cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { }
var shared = Recipe("shared", "Final", 1, new RequirementIngredient("B", 2), new RequirementIngredient("B", 2));
plan = RecipePlanner.Build(shared, [shared, child], 1, new Dictionary<string, string>());
Require(plan.Gather.Single().Amount == 10, "Shared requirements included in shopping list");
var large = Enumerable.Range(0, 35).Select(i => Recipe($"r{i}", $"N{i}", 1, new RequirementIngredient($"N{i+1}", 1))).ToArray();
try { RecipePlanner.Build(large[0], large, 1, large.ToDictionary(r => r.ResultId, r => r.Id)); throw new Exception("Depth limit ignored"); }
catch (InvalidOperationException) { }
Console.WriteLine("Cancellation, shared ingredients and route limits passed.");

if (args.Length == 1)
{
    var catalog = System.Text.Json.JsonSerializer.Deserialize<RequirementRecipe[]>(File.ReadAllText(args[0]))!;
    int maximum = 0;
    foreach (var recipe in catalog)
    {
        var actual = RecipePlanner.Build(recipe, catalog, 1, new Dictionary<string, string>());
        Require(actual.Steps[^1].Recipe.Id == recipe.Id, "Requested recipe must remain the final step");
        maximum = Math.Max(maximum, actual.Steps.Count);
    }
    Console.WriteLine($"Real catalog: {catalog.Length} recipe routes passed; largest route {maximum} steps.");
}
