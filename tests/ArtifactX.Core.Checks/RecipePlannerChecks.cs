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
        var ammonia = R("ammonia", 1, new RequirementIngredient("nitrogen", 1));
        var nitrogen = R("nitrogen", 1, new RequirementIngredient("radon", 3));
        var radon = R("radon", 1, new RequirementIngredient("nitrogen", 3));
        var converted = RecipePlanner.Build(ammonia, [ammonia, nitrogen, radon], 2,
            new Dictionary<string, string> { ["nitrogen"] = "nitrogen" });
        if (converted.Steps.Count != 3 || converted.Gather.Single() is not { Id: "nitrogen", Amount: 18 })
            throw new InvalidOperationException("Chosen gas conversion did not preserve its starting supply.");
        var closed = RecipePlanner.Build(ammonia, [ammonia, nitrogen, radon], 1,
            new Dictionary<string, string> { ["nitrogen"] = "nitrogen", ["radon"] = "radon" });
        if (closed.Gather.Single() is not { Id: "nitrogen", Amount: 9 } || closed.Notice.Length == 0)
            throw new InvalidOperationException("Explicit circular conversion must stop at a clearly explained starting supply.");
        var automatic = RecipePlanner.Build(ammonia, [ammonia, nitrogen, radon], 1, new Dictionary<string, string>());
        if (automatic.Steps.Count != 3 || automatic.CircularSupplies.Single() is not { ParentId: "radon", Id: "nitrogen", Amount: 9 })
            throw new InvalidOperationException("Automatic gas route stopped before the actual circular edge.");
        var scaled = RecipePlanner.Build(root, [root, left, right, shared], 3, new Dictionary<string, string>());
        if (scaled.Gather.Single().Amount != 3) throw new InvalidOperationException("Scaled shared batches differ.");
        var supplied = RecipePlanner.Build(root, [root, left, right, shared], 3, new Dictionary<string, string>(),
            suppliedItems: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sugar" });
        if (supplied.Steps.Count != 3 || supplied.Steps.Any(s => s.Recipe.ResultId == "sugar")
            || supplied.Gather.Single() is not { Id: "sugar", Amount: 6 })
            throw new InvalidOperationException("Chosen supply failed to replace the shared recipe and aggregate its demand.");
        var suppliedRoot = RecipePlanner.Build(shared, [shared], 3, new Dictionary<string, string>(),
            suppliedItems: new HashSet<string> { "sugar" });
        if (suppliedRoot.Steps.Count != 0 || suppliedRoot.Gather.Single() is not { Id: "sugar", Amount: 3 })
            throw new InvalidOperationException("Sourcing the target incorrectly rounded up to recipe batch output.");
        var suppliedGas = RecipePlanner.Build(ammonia, [ammonia, nitrogen, radon], 4, new Dictionary<string, string>(),
            suppliedItems: new HashSet<string> { "nitrogen" });
        if (suppliedGas.Steps.Count != 1 || suppliedGas.Gather.Single() is not { Id: "nitrogen", Amount: 4 }
            || suppliedGas.CircularSupplies.Count != 0 || suppliedGas.Notice.Length > 0)
            throw new InvalidOperationException("Chosen gas supply retained a conversion loop.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { RecipePlanner.Build(root, [root, left, right, shared], 1, new Dictionary<string, string>(), cancelled.Token); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: shared recipe consolidation, batch rounding and circular starting supply.");
    }

    public static void Catalog(string path)
    {
        var recipes = JsonSerializer.Deserialize<RequirementRecipe[]>(File.ReadAllText(path))!;
        int count = 0;
        foreach (var (recipe, available) in recipes.Select(r => (r, recipes))
            .Concat(recipes.Where(r => r.Kind == "Refiner").Select(r => (r, recipes.Where(a => a.Kind == "Refiner").ToArray()))))
        {
            var plan = RecipePlanner.Build(recipe, available, 1, new Dictionary<string, string>());
            if (plan.Steps.Count > 100 || plan.Steps.Select(s => s.Recipe.ResultId).Distinct().Count() != plan.Steps.Count)
                throw new InvalidOperationException("Plan has repeated recipes or exceeded its bound.");
            var completed = plan.Gather.Select(g => g.Id).ToHashSet();
            foreach (var step in plan.Steps)
            {
                if (step.Recipe.Ingredients.Any(i => !completed.Contains(i.Id)))
                    throw new InvalidOperationException($"Dependency order differs for {recipe.Id}");
                completed.Add(step.Recipe.ResultId);
            }
            foreach (var id in completed)
            {
                long supply = plan.Gather.Where(g => g.Id == id).Sum(g => (long)g.Amount)
                    + plan.Steps.Where(s => s.Recipe.ResultId == id).Sum(s => s.Batches * s.Recipe.ResultAmount);
                long demand = plan.Steps.Sum(s => s.Batches * s.Recipe.Ingredients.Where(i => i.Id == id).Sum(i => (long)i.Amount))
                    + (id == recipe.ResultId ? 1 : 0);
                if (supply < demand) throw new InvalidOperationException($"Supply shortage for {recipe.Id}/{id}");
            }
            if (recipe.ResultId is "EX_PURPLE" or "ALLOY1")
                Console.WriteLine($"Expanded {recipe.Id}: {plan.Steps.Count} operations, {plan.CircularSupplies.Count} circular supplies.");
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
