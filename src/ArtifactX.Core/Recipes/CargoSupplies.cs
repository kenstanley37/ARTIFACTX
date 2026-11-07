using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.Recipes;

public sealed record CargoSupply(string Location, string ItemId, long Amount);

/// <summary>Read-only cargo discovery using the save paths confirmed in
/// NmsInventoryContainer. The first ten Chest containers are the shared storage
/// pool, counted once rather than once per base. Cold/ingredient storage and
/// the dismantled-base cache use their separately confirmed paths. Technology
/// inventories and unidentified mirrors are excluded. Read-only inspection of
/// Slot 8 save15.hg.json on 2026-11-01 reconfirmed the ten Chest entries precede
/// the three special chests, Kha is Default and KQ&lt; is a separate Chest.
/// Shared locations therefore never need duplication for each placed base.</summary>
public static class CargoSupplies
{
    public static IReadOnlyList<CargoSupply> Read(JObject? player)
    {
        var result = new List<CargoSupply>();
        if (player is null) return result;
        void Add(JToken? container, string fallback)
        {
            if (container?[":No"] is not JArray slots) return;
            string name = container["NKm"]?.Value<string>()?.Trim() ?? "";
            string location = name.Length > 0 ? $"{fallback}: {name}" : fallback;
            foreach (var group in slots.OfType<JObject>()
                .Where(s => s["Vn8"]?["elv"]?.Value<string>() is "Product" or "Substance")
                .Select(s => (Id: Normalize(s["b2n"]?.Value<string>() ?? ""), Amount: s["1o9"]?.Value<long>() ?? 0))
                .Where(s => s.Id.Length > 0 && s.Amount > 0).GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
                result.Add(new(location, group.Key, group.Sum(s => s.Amount)));
        }
        Add(player[";l5"], "Exosuit");
        Add(player["8ZP"], "Freighter cargo");
        if (player["@Cs"] is JArray ships)
            for (int i = 0; i < ships.Count; i++)
                if (ships[i] is JObject ship && !string.IsNullOrWhiteSpace(ship["NTx"]?["93M"]?.Value<string>()))
                    Add(ship[";l5"], string.IsNullOrWhiteSpace(ship["NKm"]?.Value<string>()) ? $"Starship {i + 1}" : ship["NKm"]!.Value<string>()!);
        if (player["P;m"] is JArray vehicles)
            for (int i = 0; i < vehicles.Count; i++) Add(vehicles[i]?[";l5"], $"Exocraft {i + 1}");
        int storage = 0;
        foreach (var property in player.Properties())
        {
            if (property.Value is not JObject container || container["WA4"]?["rri"]?.Value<string>() != "Chest") continue;
            if (storage == 10) break;
            Add(container, $"Storage Container {storage++}");
        }
        Add(player["Kha"], "Ingredient storage");
        Add(player["KQ<"], "Cold storage");
        Add(player[";?C"], "Dismantled base components");
        Add(player["wem"], "Corvette workshop cache");
        return result;
    }

    private static string Normalize(string id) => id.TrimStart('^').Split('#')[0];
}
