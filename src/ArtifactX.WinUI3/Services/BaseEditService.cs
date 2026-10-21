using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Deleting bases, loose placed objects and base parts, as staged edits on the loaded
/// save (SaveSessionManager.StageEdit) - written, with a backup first, when the player
/// saves.
///
/// The Bases pages list what the newest save on disk holds while the game runs
/// (LiveSaveWatcher), which is a different copy from the loaded save edits go to, so
/// entries are found again in the loaded save by what they are, never by reference.
/// </summary>
public static class BaseEditService
{
    /// <summary>Whether two base entries are the same base: same address, name and type.</summary>
    public static bool SameBase(JObject a, JObject b) =>
        ReferenceEquals(a, b) ||
        (JToken.DeepEquals(a["oZw"], b["oZw"]) &&
         a["NKm"]?.Value<string>() == b["NKm"]?.Value<string>() &&
         a["peI"]?["DPp"]?.Value<string>() == b["peI"]?["DPp"]?.Value<string>());

    /// <summary>Removes these bases (and everything built in them). False when none was found.</summary>
    public static bool RemoveBases(IReadOnlyCollection<JObject> bases)
    {
        if (bases.Count == 0 || SaveSessionManager.GetValue(S.BasesPath) is not JArray current) return false;
        var kept = new JArray(current.Where(t => t is not JObject b || !bases.Any(r => SameBase(b, r))));
        if (kept.Count == current.Count) return false;
        SaveSessionManager.StageEdit(kept, S.BasesPath);
        Changed();
        return true;
    }

    /// <summary>Removes these loose placed objects. False when none was found.</summary>
    public static bool RemoveLooseObjects(IReadOnlyCollection<JObject> objects)
    {
        if (objects.Count == 0 || SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is not JArray current) return false;
        var kept = new JArray(current.Where(t => t is not JObject o || !objects.Any(r => ReferenceEquals(o, r) || JToken.DeepEquals(o, r))));
        if (kept.Count == current.Count) return false;
        SaveSessionManager.StageEdit(kept, NmsBuildCounts.LooseObjectsPath);
        Changed();
        return true;
    }

    /// <summary>Removes every part of these kinds (r&lt;7 ids) from a base, and returns the
    /// base's parts as they now are - null when the base isn't in the loaded save.</summary>
    public static JArray? RemovePartKinds(JObject baseEntry, IReadOnlySet<string> partIds)
    {
        if (SaveSessionManager.GetValue(S.BasesPath) is not JArray current) return null;
        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] is not JObject b || !SameBase(b, baseEntry)) continue;
            var kept = new JArray((b["@ZJ"] as JArray ?? new JArray())
                .Where(t => t is not JObject part || !partIds.Contains(part["r<7"]?.Value<string>() ?? "")));
            SaveSessionManager.StageEdit(kept, S.BasesPath.Append(i.ToString(CultureInfo.InvariantCulture)).Append("@ZJ").ToArray());
            Changed();
            return kept;
        }
        return null;
    }

    // The live snapshot no longer matches the loaded save; the next read takes the edited one.
    private static void Changed() => LiveSaveWatcher.LatestSnapshot = null;
}
