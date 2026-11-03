using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.Core.Inventory;

/// <summary>Combines compatible cargo stacks without losing quantities or
/// changing item metadata. Technology and non-stackable items stay separate.</summary>
public static class InventoryStackMerger
{
    public static JArray? Merge(JArray slots, out int freedSlots)
    {
        freedSlots = 0;
        var result = (JArray)slots.DeepClone();
        var groups = new List<(JObject Identity, List<JObject> Slots)>();
        foreach (var slot in result.OfType<JObject>())
        {
            string? category = slot["Vn8"]?["elv"]?.Value<string>();
            if (category is not ("Product" or "Substance") || slot["b2n"]?.Type != JTokenType.String
                || slot["F9q"]?.Value<long>() is not > 1 || slot["1o9"]?.Value<long>() is not > 0
                || slot["1o9"]!.Value<long>() > slot["F9q"]!.Value<long>()) continue;
            var identity = (JObject)slot.DeepClone();
            identity.Remove("1o9");
            identity.Remove("3ZH");
            var group = groups.FirstOrDefault(g => JToken.DeepEquals(g.Identity, identity));
            if (group.Slots is null) groups.Add((identity, [slot]));
            else group.Slots.Add(slot);
        }
        bool changed = false;
        foreach (var group in groups)
        {
            var ordered = group.Slots.OrderBy(s => s["3ZH"]?["XJ>"]?.Value<int>() ?? 0)
                .ThenBy(s => s["3ZH"]?[">Qh"]?.Value<int>() ?? 0).ToList();
            long total = ordered.Sum(s => s["1o9"]!.Value<long>());
            foreach (var slot in ordered)
            {
                if (total == 0) { slot.Remove(); freedSlots++; changed = true; continue; }
                long amount = Math.Min(total, slot["F9q"]!.Value<long>());
                if (amount != slot["1o9"]!.Value<long>()) { slot["1o9"] = amount; changed = true; }
                total -= amount;
            }
        }
        return changed ? result : null;
    }
}
