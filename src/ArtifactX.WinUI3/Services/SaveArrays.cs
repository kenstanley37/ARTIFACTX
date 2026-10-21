using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Edits to fixed-size arrays in the save (a pet's traits, a settlement's stats).</summary>
public static class SaveArrays
{
    /// <summary>Stages one entry by rebuilding and staging the WHOLE array - a deeper
    /// leaf-only stage isn't seen by SaveSessionManager's staged-edit lookup. Does
    /// nothing unless the array has the expected size.</summary>
    public static void StageEntry(string[] path, int index, int expectedCount, Func<JToken, JToken> newValue)
    {
        if (SaveSessionManager.GetValue(path) is not JArray array || array.Count != expectedCount) return;
        var updated = new JArray(array.Select(v => v.DeepClone()));
        updated[index] = newValue(array[index]);
        SaveSessionManager.StageEdit(updated, path);
    }
}
