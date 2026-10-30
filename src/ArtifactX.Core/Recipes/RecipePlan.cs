namespace ArtifactX.Core.Recipes;

public sealed record RecipePlanStep(RequirementRecipe Recipe, long Batches, string[] Alternatives);
public sealed record RecipePlan(IReadOnlyList<RecipePlanStep> Steps, IReadOnlyList<RequirementIngredient> Gather, string Notice);

/// <summary>Builds a single dependency-first route. Alternatives remain choices, never additional requirements.</summary>
public static class RecipePlanner
{
    public static RecipePlan Build(RequirementRecipe root, IReadOnlyList<RequirementRecipe> recipes,
        int quantity, IReadOnlyDictionary<string, string> choices, CancellationToken cancellationToken = default)
    {
        var byResult = recipes.GroupBy(r => r.ResultId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // A recipe is reachable when all inputs can be gathered or already have an acyclic route.
        for (int pass = 0; pass < 32; pass++)
        {
            bool changed = false;
            foreach (var recipe in recipes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inputs = recipe.Ingredients.Where(i => i.Amount > 0).ToArray();
                if (inputs.Any(i => byResult.ContainsKey(i.Id) && !rank.ContainsKey(i.Id))) continue;
                int value = 1 + inputs.Select(i => rank.GetValueOrDefault(i.Id)).DefaultIfEmpty(0).Max();
                if (!rank.TryGetValue(recipe.ResultId, out int old) || value < old)
                { rank[recipe.ResultId] = value; changed = true; }
            }
            if (!changed) break;
        }
        var steps = new List<RecipePlanStep>();
        var gather = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string notice = "";
        long Batches(long amount, int output) => (amount - 1) / Math.Max(1, output) + 1;
        void Walk(RequirementRecipe recipe, long amount, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth >= 32 || steps.Count >= 100 || !path.Add(recipe.ResultId))
                throw new InvalidOperationException("This choice loops back to an earlier ingredient or makes the route too large. Choose another recipe.");
            long batches = Batches(amount, recipe.ResultAmount);
            foreach (var input in recipe.Ingredients.Where(i => i.Amount > 0))
            {
                long needed = checked(batches * input.Amount);
                if (path.Contains(input.Id))
                {
                    if (depth > 0) throw new InvalidOperationException("This recipe choice loops back to an earlier ingredient. Choose another recipe.");
                    gather[input.Id] = checked(gather.GetValueOrDefault(input.Id) + needed);
                    continue;
                }
                if (!byResult.TryGetValue(input.Id, out var alternatives))
                { gather[input.Id] = checked(gather.GetValueOrDefault(input.Id) + needed); continue; }
                RequirementRecipe? selected = null;
                if (choices.TryGetValue(input.Id, out string? pick)) selected = alternatives.FirstOrDefault(r => r.Id == pick);
                selected ??= alternatives.Where(r => r.Ingredients.All(i => !path.Contains(i.Id) && (!byResult.ContainsKey(i.Id)
                        || rank.GetValueOrDefault(i.Id, int.MaxValue) < rank.GetValueOrDefault(input.Id, int.MaxValue))))
                    .OrderBy(r => r.Ingredients.Select(i => rank.GetValueOrDefault(i.Id)).DefaultIfEmpty(0).Max())
                    .ThenBy(r => r.Ingredients.Count).FirstOrDefault();
                if (selected is null)
                {
                    gather[input.Id] = checked(gather.GetValueOrDefault(input.Id) + needed);
                    notice = "Some ingredients only have circular recipes. Gather or buy those ingredients instead.";
                }
                else Walk(selected, needed, depth + 1);
            }
            path.Remove(recipe.ResultId);
            if (steps.Count >= 100) throw new InvalidOperationException("This route exceeds 100 steps. Choose recipes with fewer requirements.");
            steps.Add(new(recipe, batches, byResult.GetValueOrDefault(recipe.ResultId, []).Select(r => r.Id).ToArray()));
        }
        Walk(root, Math.Max(1, quantity), 0);
        return new(steps, gather.Select(g => new RequirementIngredient(g.Key, checked((int)g.Value))).ToArray(), notice);
    }
}
