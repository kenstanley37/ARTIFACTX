using Newtonsoft.Json.Linq;
using System.Collections.Generic;

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
    /// edits, in order; returns the paths that changed. "vLc" paths, as AdoptFromGame.</summary>
    public static List<string[]> Adopt(JObject disk, IEnumerable<string[]> paths)
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
            if (theirs is null || JToken.DeepEquals(theirs, SaveSessionManager.GetValue(path))) continue;
            if (SaveSessionManager.AdoptFromGame(disk, path)) changed.Add(path);
        }
        return changed;
    }
}
