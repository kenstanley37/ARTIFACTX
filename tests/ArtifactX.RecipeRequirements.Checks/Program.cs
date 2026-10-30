using ArtifactX.Core.Recipes;

static RequirementRecipe Recipe(string id, string result, int output, params RequirementIngredient[] ingredients)
    => new(id, "Refining", result, output, ingredients);
static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
var root = Recipe("root", "A", 2, new RequirementIngredient("B", 3));
var child = Recipe("child", "B", 4, new RequirementIngredient("Raw", 5));
var rows = RecipeRequirements.Build(root, [root, child], 5);
Require(rows.Single(r => r.ItemId == "B" && !r.IsStep).Amount == 9, "Root batch rounding");
Require(rows.Single(r => r.ItemId == "Raw").Amount == 15, "Nested batch rounding");
var cycle = Recipe("cycle", "b", 1, new RequirementIngredient("a", 1));
rows = RecipeRequirements.Build(root, [root, cycle], 1);
Require(rows.Count == 3 && rows[^1].StopReason is not null, "Case-insensitive cycle detection");
var diamond = Recipe("diamond", "A", 1, new RequirementIngredient("B", 1), new RequirementIngredient("B", 1));
rows = RecipeRequirements.Build(diamond, [diamond, child], 1);
Require(rows.Count(r => r.ItemId == "Raw") == 2, "Repeated independent branches must both expand");
var graph = Enumerable.Range(0, 25).SelectMany(i => Enumerable.Range(0, 4)
    .Select(j => Recipe($"{i}-{j}", $"N{i}", 1, new RequirementIngredient($"N{i + 1}", int.MaxValue)))).ToArray();
rows = RecipeRequirements.Build(graph[0], graph, 99999);
Require(rows.Count <= 1001 && rows.Any(r => r.StopReason is not null), "Bound large graph and quantity overflow");
var wide = Enumerable.Range(0, 25).SelectMany(i => Enumerable.Range(0, 4)
    .Select(j => Recipe($"{i}-{j}", $"N{i}", 1, new RequirementIngredient($"N{i + 1}", 1)))).ToArray();
rows = RecipeRequirements.Build(wide[0], wide, 1);
Require(rows.Count == 1001 && rows[^1].StopReason!.Contains("1,000"), "Bound alternatives and explain truncation");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try { RecipeRequirements.Build(root, [root], 1, cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { }
Console.WriteLine("Recipe requirements checks passed: quantities, cycles, shared branches, overflow, limits and cancellation.");

