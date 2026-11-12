using ArtifactX.Core.Inventory;

internal static class InventoryStackLimitChecks
{
    /// <summary>Bug 78: full stacks from the game's stack-limit tables (build 185034 DIFFICULTYCONFIG, High and Normal
    /// options), against slots in the owner's saves (all High): Metal Plating (StackMultiplier 2) is 40 in a storage
    /// container (Chest) and 20 in the Exosuit (Personal); a multiplier of 0 still stacks to 1; substances 9999 on High,
    /// 500 in the Exosuit on Normal; Default containers and machine substances are left to the caller.</summary>
    public static void Check()
    {
        string[] groups = ["Default", "Personal", "PersonalCargo", "Ship", "ShipCargo", "Freighter", "FreighterCargo", "Vehicle",
            "Chest", "BaseCapsule", "MaintenanceObject", "UIPopup", "SeasonTransfer"];
        var tables = new InventoryStackLimitTables(groups,
        [
            new("High", [5, 10, 10, 10, 10, 20, 20, 10, 20, 100, 10, 1, 20], Enumerable.Repeat(9999, 13).ToArray(), 9999, 9999),
            new("Normal", [5, 10, 10, 10, 10, 10, 20, 10, 20, 100, 10, 1, 20], [500, 500, 500, 1000, 1000, 2000, 2000, 1000, 1000, 2000, 250, 250, 9999], 9999, 9999),
        ]);

        int Product(string group, string setting, int multiplier)
        {
            var s = InventoryStackLimits.Sizes(tables, group, setting) ?? throw new InvalidOperationException($"no sizes for {group}/{setting}");
            return InventoryStackLimits.ProductStack(multiplier, s.Product, s.ProductLimit);
        }
        Expect(Product("Chest", "High", 2), 40, "Metal Plating in a storage container (High)");
        Expect(Product("Personal", "High", 2), 20, "Metal Plating in the Exosuit (High)");
        Expect(Product("Freighter", "High", 5), 100, "multiplier 5 in freighter storage (High)");
        Expect(Product("BaseCapsule", "High", 20), 2000, "multiplier 20 in a base capsule (High)");
        Expect(Product("Freighter", "Normal", 5), 50, "multiplier 5 in freighter storage (Normal)");
        Expect(Product("Ship", "High", 0), 1, "multiplier 0 stacks to 1");
        Expect(Product("BaseCapsule", "High", 1000), 9999, "capped at the product limit");
        Expect(InventoryStackLimits.Sizes(tables, "Personal", "Normal")?.Substance ?? -1, 500, "substances in the Exosuit (Normal)");
        Expect(InventoryStackLimits.Sizes(tables, "Ship", "High")?.Substance ?? -1, 9999, "substances in a ship (High)");
        if (InventoryStackLimits.Sizes(tables, "Default", "High") is not null) throw new InvalidOperationException("Default group must be left to the caller.");
        if (InventoryStackLimits.Sizes(tables, "MaintenanceObject", "High")?.Substance is not null) throw new InvalidOperationException("machine substances must be left to the caller.");
        if (InventoryStackLimits.Sizes(tables, "Chest", "Ultra") is not null) throw new InvalidOperationException("unknown setting must give null.");
        Console.WriteLine("PASS: inventory stack limits by stack group and setting (Bug 78: Metal Plating 40 in storage, 20 in the Exosuit).");
    }

    private static void Expect(int got, int want, string what)
    {
        if (got != want) throw new InvalidOperationException($"{what}: {got}, expected {want}.");
    }
}
