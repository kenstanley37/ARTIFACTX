using ArtifactX.Core.Recipes;
using System.Text.Json;

internal static class RecipePlannerChecks
{
    public static void Check()
    {
        RequirementRecipe R(string id, int yield, params RequirementIngredient[] inputs) => new(id, "Cooking", id, yield, inputs);
        var shared = R("sugar", 2, new RequirementIngredient("raw", 1));
        var left = R("left", 1, new RequirementIngredient("sugar", 1));
        var right = R("right", 1, new RequirementIngredient("sugar", 1));
        var root = R("root", 1, new("left", 1), new("right", 1));
        var plan = RecipePlanner.Build(root, [root, left, right, shared], 1, new Dictionary<string, string>());
        if (plan.Steps.Count != 4 || plan.Steps.Single(s => s.Recipe.ResultId == "sugar").Batches != 1
            || plan.Gather.Single().Amount != 1)
            throw new InvalidOperationException("Shared recipe demand was not aggregated before rounding batches.");
        var loop = R("loop", 1, new RequirementIngredient("loop", 1));
        var loopPlan = RecipePlanner.Build(loop, [loop], 1, new Dictionary<string, string>());
        if (loopPlan.Gather.Single().Id != "loop") throw new InvalidOperationException("Circular starting supply missing.");
        var scaled = RecipePlanner.Build(root, [root, left, right, shared], 3, new Dictionary<string, string>());
        if (scaled.Gather.Single().Amount != 3) throw new InvalidOperationException("Scaled shared batches differ.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { RecipePlanner.Build(root, [root, left, right, shared], 1, new Dictionary<string, string>(), cancelled.Token); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: shared recipe consolidation, batch rounding and circular starting supply.");
    }

    public static void Catalog(string path)
    {
        var recipes = JsonSerializer.Deserialize<RequirementRecipe[]>(File.ReadAllText(path))!;
        int count = 0;
        foreach (var recipe in recipes)
        {
            var plan = RecipePlanner.Build(recipe, recipes, 1, new Dictionary<string, string>());
            if (plan.Steps.Count > 100 || plan.Steps.Select(s => s.Recipe.ResultId).Distinct().Count() != plan.Steps.Count)
                throw new InvalidOperationException("Plan has repeated recipes or exceeded its bound.");
            var completed = plan.Gather.Select(g => g.Id).ToHashSet();
            foreach (var step in plan.Steps)
            {
                if (step.Recipe.Ingredients.Any(i => !completed.Contains(i.Id)))
                    throw new InvalidOperationException($"Dependency order differs for {recipe.Id}");
                completed.Add(step.Recipe.ResultId);
            }
            count++;
        }
        Console.WriteLine($"PASS: {count} catalog routes are finite, consolidated and dependency ordered.");
    }

    public static void Native(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        int count = 0;
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var food = row.Deserialize<FoodIngestorDefinition>()!;
            float expected = row.GetProperty("Expected").GetSingle();
            float actual = FoodIngestorBonus.Calculate(food);
            if (BitConverter.SingleToInt32Bits(expected) != BitConverter.SingleToInt32Bits(actual))
                throw new InvalidOperationException($"Food bonus differs for {food.Id}: {actual:R} vs native {expected:R}");
            count++;
        }
        Console.WriteLine($"PASS: {count} food bonus values match native float bits.");
    }
}
