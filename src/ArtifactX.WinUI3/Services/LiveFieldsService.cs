using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Fields that follow the game while it plays (user choice 2026-10-22, "quiet update"):
/// when the game saves the loaded slot, a page's fields the player hasn't edited take
/// the game's new values - an engine flare picked in game showed the old one until a
/// reload. Fields with unsaved edits are left alone (AdoptFromGame refuses them), and
/// an expedition/main game switch is left to MainWindow's full reload.
/// </summary>
public static class LiveFieldsService
{
    /// <summary>Takes the game's value for each path that differs and has no unsaved
    /// edits, in order; returns the paths that changed. "vLc" paths, as AdoptFromGame.
    /// An inventory (a path in <paramref name="inventories"/>) is still taken when only
    /// charge levels moved - fuel and ammo change on nearly every save - but counts as
    /// changed only when an item was added, removed or moved.</summary>
    public static List<string[]> Adopt(JObject disk, IEnumerable<string[]> paths, ICollection<string[]>? inventories = null)
    {
        var changed = new List<string[]>();
        if (SaveSessionManager.IsMidExpedition(disk) != SaveSessionManager.IsExpeditionActive) return changed;
        foreach (var path in paths)
        {
            JToken? theirs = disk;
            foreach (var key in SaveSessionManager.MapPath(path))
                theirs = theirs switch
                {
                    JArray array => int.TryParse(key, out int i) && i >= 0 && i < array.Count ? array[i] : null,
                    JObject obj => obj[key],
                    _ => null,
                };
            var ours = SaveSessionManager.GetValue(path);
            if (theirs is null || JToken.DeepEquals(theirs, ours)) continue;
            bool shows = inventories?.Contains(path) != true || Layout(theirs) != Layout(ours);
            if (SaveSessionManager.AdoptFromGame(disk, path) && shows) changed.Add(path);
        }
        return changed;
    }

    /// <summary>What an inventory holds where: each slot's item id and position
    /// (":No" slots, "b2n" id, "3ZH" position), plus its class.</summary>
    private static string Layout(JToken? inventory) =>
        inventory?["B@N"]?.ToString(Newtonsoft.Json.Formatting.None) + "|" + string.Join(";",
            (inventory?[":No"] as JArray ?? new JArray()).Select(slot =>
                $"{slot["b2n"]}@{slot["3ZH"]?[">Qh"]},{slot["3ZH"]?["XJ>"]}"));
}
