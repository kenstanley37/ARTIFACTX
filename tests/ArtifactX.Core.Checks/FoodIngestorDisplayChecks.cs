using ArtifactX.Core.Recipes;

internal static class FoodIngestorDisplayChecks
{
    public static void Check()
    {
        // Owner's Slot 8 Nutrient Ingestor, 2026-11-03.
        var iceCream = new FoodIngestorDefinition("FOOD_ICE", 95, "Suit_Protection_ColdDrain", 1, 0, 1.15f, 1.5f, true, false);
        var doughnut = new FoodIngestorDefinition("FOOD_DNUT_AJAM", 61, "Weapon_Scan_Discovery_Creature", 1, 0, 10, 60, true, false);
        (string Name, string? Actual, string? Expected)[] cases =
        [
            ("Ice Cream", FoodIngestorDisplay.Text(iceCream.Stat, FoodIngestorBonus.Calculate(iceCream)), "+65%"),
            ("Anomalous Doughnut", FoodIngestorDisplay.Text(doughnut.Stat, FoodIngestorBonus.Calculate(doughnut)), "+6,443%"),
            ("Small gain rounds to 1", FoodIngestorDisplay.Text(95, 1.001f), "+1%"),
            ("Small loss rounds to -1", FoodIngestorDisplay.Text(58, 0.999f), "-1%"),
            ("Negated mining speed", FoodIngestorDisplay.Text(3, 0.75f), "+25%"),
            ("Equipment-dependent stat", FoodIngestorDisplay.Text(78, 0.3f), null),
        ];
        foreach (var (name, actual, expected) in cases)
            if (actual != expected) throw new InvalidOperationException($"{name}: '{actual}', expected '{expected}'.");
        Console.WriteLine($"PASS: {cases.Length} food ingestor display checks.");
    }
}
