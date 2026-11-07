using ArtifactX.Core.Recipes;
using Newtonsoft.Json.Linq;

internal static class CargoSuppliesChecks
{
    public static void Check()
    {
        JObject Container(int amount, string category = "Substance") => new()
        {
            ["WA4"] = new JObject { ["rri"] = "Chest" },
            [":No"] = new JArray(new JObject
            {
                ["b2n"] = "^GAS3", ["1o9"] = amount,
                ["Vn8"] = new JObject { ["elv"] = category }
            })
        };
        var player = new JObject { [";l5"] = Container(3), ["8ZP"] = Container(4) };
        // Suit/freighter aren't Chest containers in real saves.
        player[";l5"]!["WA4"]!["rri"] = "Default";
        player["8ZP"]!["WA4"]!["rri"] = "Default";
        for (int i = 0; i < 10; i++) player[$"storage{i}"] = Container(10);
        player["unknownChest"] = Container(900);
        player["KQ<"] = Container(7);
        player["Kha"] = Container(8); player["Kha"]!["WA4"]!["rri"] = "Default";
        player["@Cs"] = new JArray(
            new JObject { ["NTx"] = new JObject { ["93M"] = "ship.scene" }, ["NKm"] = "My ship", [";l5"] = Container(5), ["PMT"] = Container(900) },
            new JObject { ["NTx"] = new JObject { ["93M"] = "" }, [";l5"] = Container(900) });
        player["P;m"] = new JArray(new JObject { [";l5"] = Container(6) });
        player[";?C"] = Container(9, "Product");
        player["wem"] = Container(900, "Technology");
        var before = player.ToString();
        var supplies = CargoSupplies.Read(player);
        if (supplies.Sum(s => s.Amount) != 142 || supplies.Count(s => s.Location.StartsWith("Storage Container")) != 10)
            throw new InvalidOperationException("Cargo discovery included a mirror, extra chest, unowned ship or installed technology.");
        if (!supplies.Any(s => s.Location == "My ship" && s.Amount == 5) || supplies.Any(s => s.ItemId != "GAS3"))
            throw new InvalidOperationException("Cargo locations or normalized item ids differ.");
        if (before != player.ToString() || CargoSupplies.Read(null).Count != 0)
            throw new InvalidOperationException("Cargo discovery changed the save or failed without a save.");
        Console.WriteLine("PASS: cargo locations, shared-storage cap, cold/ingredient storage, ownership and technology exclusion; read-only.");
    }
}
