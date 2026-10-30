namespace ArtifactX.Core.Recipes;

public sealed record RequirementRecipe(string Id, string Kind, string ResultId, int ResultAmount,
    IReadOnlyList<RequirementIngredient> Ingredients);
public sealed record RequirementIngredient(string Id, int Amount);
public sealed record RequirementRow(string ItemId, long Amount, int Depth, string Kind,
    bool IsStep, string? StopReason = null);

/// <summary>Expands catalog alternatives without revisiting an item on the current path.
/// Limits bound both work and output for cyclic or highly connected refiner graphs.</summary>
public static class RecipeRequirements
{
    public static IReadOnlyList<RequirementRow> Build(RequirementRecipe root,
        IReadOnlyList<RequirementRecipe> recipes, int quantity, CancellationToken cancellationToken = default)
    {
        const int maxRows = 1000, maxDepth = 16;
        var byResult = recipes.GroupBy(r => r.ResultId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        var rows = new List<RequirementRow>();
        var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.ResultId };
        long Batches(long amount, int output) => (amount - 1) / Math.Max(1, output) + 1;
        void Walk(RequirementRecipe recipe, long batches, int depth)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows.Count >= maxRows) return;
                if (string.IsNullOrWhiteSpace(ingredient.Id) || ingredient.Amount <= 0) continue;
                bool overflow = batches > long.MaxValue / ingredient.Amount;
                long amount = overflow ? long.MaxValue : batches * ingredient.Amount;
                byResult.TryGetValue(ingredient.Id, out var alternatives);
                string? stop = overflow ? "Quantity is too large to calculate further."
                    : path.Contains(ingredient.Id) ? "Already required above; this recipe loops back."
                    : depth >= maxDepth ? "Further requirements reach the tree depth limit." : null;
                rows.Add(new(ingredient.Id, amount, depth,
                    alternatives is { Length: > 0 } ? alternatives[0].Kind : "", false, stop));
                if (stop is not null || alternatives is null) continue;
                path.Add(ingredient.Id);
                foreach (var alternative in alternatives)
                {
                    if (rows.Count >= maxRows) break;
                    long count = Batches(amount, alternative.ResultAmount);
                    rows.Add(new(ingredient.Id, count, depth + 1, alternative.Kind, true));
                    Walk(alternative, count, depth + 1);
                }
                path.Remove(ingredient.Id);
            }
        }
        Walk(root, Batches(Math.Max(1, quantity), root.ResultAmount), 0);
        if (rows.Count >= maxRows)
            rows.Add(new(root.ResultId, 0, 0, "", true,
                "This recipe has many alternatives. Showing the first 1,000 requirements."));
        return rows;
    }
}
