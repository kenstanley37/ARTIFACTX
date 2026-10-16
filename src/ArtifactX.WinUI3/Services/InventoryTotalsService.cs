using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// How much of each item the loaded save is carrying, summed across the
/// player's storage: Exosuit cargo, every owned starship's cargo, every
/// Exocraft's cargo, the freighter's cargo and the base storage containers.
/// Read-only (for the Crafting page's "you have" column). Uses the same paths
/// and container rules as the inventory pages; SaveSessionManager's
/// expedition remap applies, so an expedition counts its own inventories.
/// </summary>
public static class InventoryTotalsService
{
    // Same cap and "Chest" marker rule as BaseStoragePage.DiscoverContainers.
    private const int MaxStorageContainers = 10;

    /// <summary>Item id (no "^", no "#seed") to total amount carried.</summary>
    public static Dictionary<string, long> Count()
    {
        var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!SaveSessionManager.IsSaveLoaded) return totals;

        void Add(JToken? container)
        {
            if (container?[":No"] is not JArray slots) return;
            foreach (var slot in slots.OfType<JObject>())
            {
                string id = CatalogService.NormalizeId(slot["b2n"]?.Value<string>() ?? "");
                long amount = slot["1o9"]?.Value<long>() ?? 0;
                if (id.Length > 0 && amount > 0) totals[id] = totals.GetValueOrDefault(id) + amount;
            }
        }

        Add(SaveSessionManager.GetValue(NmsInventoryContainer.ExosuitCargoPath));
        Add(SaveSessionManager.GetValue(NmsInventoryContainer.FreighterCargoPath));
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ShipArrayPath) is JArray ships)
            foreach (var ship in ships.OfType<JObject>()) Add(ship[";l5"]);
        if (SaveSessionManager.GetValue(NmsInventoryContainer.ExocraftArrayPath) is JArray vehicles)
            foreach (var vehicle in vehicles.OfType<JObject>()) Add(vehicle[";l5"]);

        if (SaveSessionManager.GetValue("vLc", "6f=") is JObject player)
        {
            int containers = 0;
            foreach (var prop in player.Properties())
            {
                if (containers >= MaxStorageContainers) break;
                if (prop.Value is not JObject obj || obj["WA4"]?["rri"]?.Value<string>() != "Chest") continue;
                containers++;
                Add(obj);
            }
        }
        return totals;
    }
}
