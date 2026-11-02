namespace ArtifactX.Core.Recipes;

public sealed record RecipePlanStep(RequirementRecipe Recipe, long Batches, string[] Alternatives);
public sealed record RecipePlanSupply(string ParentId, string Id, long Amount);
public sealed record RecipePlan(IReadOnlyList<RecipePlanStep> Steps, IReadOnlyList<RequirementIngredient> Gather, string Notice)
{
    public IReadOnlyList<RecipePlanSupply> CircularSupplies { get; init; } = [];
}

/// <summary>Builds a single dependency-first route. Alternatives remain choices, never additional requirements.</summary>
public static class RecipePlanner
{
    public static RecipePlan Build(RequirementRecipe root, IReadOnlyList<RequirementRecipe> recipes,
        int quantity, IReadOnlyDictionary<string, string> choices, CancellationToken cancellationToken = default,
        IReadOnlySet<string>? suppliedItems = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (suppliedItems?.Contains(root.ResultId) == true)
            return new([], [new(root.ResultId, Math.Max(1, quantity))], "");
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
        var selectedRecipes = new Dictionary<string, RequirementRecipe>(StringComparer.OrdinalIgnoreCase);
        var terminalEdges = new HashSet<(string Parent, string Child)>();
        var circularEdges = new HashSet<(string Parent, string Child)>();
        var order = new List<string>();
        var gather = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string notice = "";
        long Batches(long amount, int output) => (amount - 1) / Math.Max(1, output) + 1;
        void Walk(RequirementRecipe recipe, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth >= 32 || selectedRecipes.Count >= 100 || !path.Add(recipe.ResultId))
                throw new InvalidOperationException("This choice loops back to an earlier ingredient or makes the route too large. Choose another recipe.");
            selectedRecipes[recipe.ResultId] = recipe;
            foreach (var input in recipe.Ingredients.Where(i => i.Amount > 0))
            {
                if (suppliedItems?.Contains(input.Id) == true)
                { terminalEdges.Add((recipe.ResultId, input.Id)); continue; }
                if (path.Contains(input.Id))
                {
                    terminalEdges.Add((recipe.ResultId, input.Id));
                    circularEdges.Add((recipe.ResultId, input.Id));
                    notice = "This route needs a starting supply for a circular conversion. Gather or use stored ingredients listed below.";
                    continue;
                }
                if (!byResult.TryGetValue(input.Id, out var alternatives))
                { terminalEdges.Add((recipe.ResultId, input.Id)); continue; }
                if (selectedRecipes.ContainsKey(input.Id)) continue;
                RequirementRecipe? selected = null;
                if (choices.TryGetValue(input.Id, out string? pick)) selected = alternatives.FirstOrDefault(r => r.Id == pick);
                selected ??= alternatives.Where(r => r.Ingredients.All(i => !path.Contains(i.Id) && (!byResult.ContainsKey(i.Id)
                        || rank.GetValueOrDefault(i.Id, int.MaxValue) < rank.GetValueOrDefault(input.Id, int.MaxValue))))
                    .OrderBy(r => r.Ingredients.Select(i => rank.GetValueOrDefault(i.Id)).DefaultIfEmpty(0).Max())
                    .ThenBy(r => r.Ingredients.Count).FirstOrDefault();
                // A closed conversion family has no finite rank. Expand it anyway;
                // only the actual back-edge needs a gathered starting supply.
                selected ??= alternatives.OrderBy(r => r.Ingredients.Count(i => path.Contains(i.Id)))
                    .ThenBy(r => r.Ingredients.Count).First();
                Walk(selected, depth + 1);
            }
            path.Remove(recipe.ResultId);
            order.Add(recipe.ResultId);
        }
        Walk(root, 0);
        var amounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [root.ResultId] = Math.Max(1, quantity) };
        var batchesByItem = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in order.AsEnumerable().Reverse())
        {
            var recipe = selectedRecipes[id];
            long batches = Batches(amounts[id], recipe.ResultAmount);
            batchesByItem[id] = batches;
            foreach (var input in recipe.Ingredients.Where(i => i.Amount > 0))
            {
                long needed = checked(batches * input.Amount);
                var totals = terminalEdges.Contains((id, input.Id)) ? gather : amounts;
                totals[input.Id] = checked(totals.GetValueOrDefault(input.Id) + needed);
            }
        }
        foreach (string id in order)
            steps.Add(new(selectedRecipes[id], batchesByItem[id], byResult.GetValueOrDefault(id, []).Select(r => r.Id).ToArray()));
        return new(steps, gather.Select(g => new RequirementIngredient(g.Key, checked((int)g.Value))).ToArray(), notice)
        {
            CircularSupplies = circularEdges.Select(edge => new RecipePlanSupply(edge.Parent, edge.Child,
                checked(batchesByItem[edge.Parent] * selectedRecipes[edge.Parent].Ingredients.Where(i => i.Id == edge.Child).Sum(i => (long)i.Amount)))).ToArray()
        };
    }
}
