using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.Core.Inventory;

/// <summary>
/// Remove Duplicates (#67): keeps one stack of each stackable cargo item and deletes the other stacks of it,
/// items included - the owner's request ("remove duplicates", not merge). The kept stack is the fullest one,
/// the first in reading order on a tie. Technology and one-per-slot items (max stack 1: eggs, unique items)
/// are never touched. Items count as the same when their id (b2n) and category match.
/// </summary>
public static class InventoryDuplicateRemover
{
    /// <summary>The slot array without the duplicate stacks, or null when there are none.</summary>
    public static JArray? Remove(JArray slots, out int removedStacks, out long removedItems)
    {
        removedStacks = 0;
        removedItems = 0;
        var result = (JArray)slots.DeepClone();
        var groups = result.OfType<JObject>()
            .Where(s => s["Vn8"]?["elv"]?.Value<string>() is "Product" or "Substance"
                && s["b2n"]?.Type == JTokenType.String && s["F9q"]?.Value<long>() is > 1)
            .GroupBy(s => (Id: s["b2n"]!.Value<string>()!, Category: s["Vn8"]!["elv"]!.Value<string>()!))
            .Where(g => g.Count() > 1);
        var doomed = new List<JObject>();
        foreach (var group in groups)
        {
            var keep = group.OrderByDescending(s => s["1o9"]?.Value<long>() ?? 0)
                .ThenBy(s => s["3ZH"]?["XJ>"]?.Value<int>() ?? 0)
                .ThenBy(s => s["3ZH"]?[">Qh"]?.Value<int>() ?? 0).First();
            doomed.AddRange(group.Where(s => !ReferenceEquals(s, keep)));
        }
        if (doomed.Count == 0) return null;
        foreach (var slot in doomed)
        {
            removedItems += slot["1o9"]?.Value<long>() ?? 0;
            slot.Remove();
        }
        removedStacks = doomed.Count;
        return result;
    }
}
