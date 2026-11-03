using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Discovers the ten reachable storage containers by save property order.
/// Controlled in-game rename/build tests on 2026-08-11 found ten real Chest
/// entries followed by three unreachable padding entries, even in a fresh save.
/// Neither an empty inventory nor an empty name indicates an unbuilt container.</summary>
public static class BaseStorageService
{
    public static List<StorageContainerEntry> DiscoverContainers()
    {
        var results = new List<StorageContainerEntry>();
        if (SaveSessionManager.GetValue("vLc", "6f=") is not JObject player) return results;
        int unnamed = 0;
        foreach (var property in player.Properties())
        {
            if (results.Count >= 10) break;
            if (property.Value is not JObject obj || obj["WA4"]?["rri"]?.Value<string>() != "Chest") continue;
            // Read the leaf so a staged rename is reflected in the selector.
            string name = SaveSessionManager.GetString(NmsInventoryContainer.BaseStorageContainerPath(property.Name).Append("NKm").ToArray()) ?? "";
            if (string.IsNullOrEmpty(name) || name == "BLD_STORAGE_NAME") name = $"Storage Container {++unnamed}";
            results.Add(new(property.Name, name));
        }
        return results.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static void Rename(string key, string name) =>
        SaveSessionManager.StageEdit(name, NmsInventoryContainer.BaseStorageContainerPath(key).Append("NKm").ToArray());
}
