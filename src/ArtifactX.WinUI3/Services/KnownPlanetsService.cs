using ArtifactX.Core;
using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Reads the planets known to every OTHER save slot in a platform folder, for
/// PlanetsPage's finder. Each slot's newest .hg file is decrypted straight from
/// disk (not the Working/ JSON cache, which can be stale) and only its
/// discovery list is used. Results are cached per file until its timestamp
/// changes, so revisiting the page doesn't re-decrypt anything.
/// </summary>
public static class KnownPlanetsService
{
    private static readonly Dictionary<string, (DateTime WriteTime, List<KnownPlanet> Planets)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    public static Task<List<KnownPlanet>> ReadOtherSlotsAsync(string platformFolder, int excludeSlotId) =>
        Task.Run(async () =>
        {
            var results = new List<KnownPlanet>();
            foreach (var (slotId, path) in NewestFilePerSlot(platformFolder))
            {
                if (slotId == excludeSlotId) continue;
                foreach (var planet in await ReadFileAsync(path))
                    results.Add(planet with { Source = $"Slot {slotId}" });
            }
            return results;
        });

    private static IEnumerable<(int SlotId, string Path)> NewestFilePerSlot(string folder)
    {
        if (!Directory.Exists(folder)) return Enumerable.Empty<(int, string)>();

        return Directory.GetFiles(folder, "save*.hg", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetExtension(f).Equals(".hg", StringComparison.OrdinalIgnoreCase))
            .GroupBy(SlotIdFor)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.OrderByDescending(File.GetLastWriteTimeUtc).First()));
    }

    // Same pairing as SaveFolderIndexingService: (save, save2) -> 1, (save3, save4) -> 2...
    private static int SlotIdFor(string filePath)
    {
        string digits = new(Path.GetFileNameWithoutExtension(filePath).Where(char.IsDigit).ToArray());
        int fileIndex = int.TryParse(digits, out int n) ? n : 1;
        return ((fileIndex - 1) / 2) + 1;
    }

    private static async Task<List<KnownPlanet>> ReadFileAsync(string path)
    {
        DateTime writeTime = File.GetLastWriteTimeUtc(path);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out var cached) && cached.WriteTime == writeTime)
                return cached.Planets;
        }

        List<KnownPlanet> planets;
        try
        {
            string json = await SaveStreamProcessor.ExtractRawJsonAsync(path);
            var root = JObject.Parse(json.TrimEnd('\0'));
            planets = NmsPlanets.ReadAll(root["fDu"]?["ETO"]?["OsQ"]?["?fB"] as JArray);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KnownPlanetsService] Skipping {Path.GetFileName(path)}: {ex.Message}");
            planets = new();
        }

        lock (CacheLock) Cache[path] = (writeTime, planets);
        return planets;
    }
}

/// <summary>What PlanetsPage needs from one save: the player's location
/// (yhJ), stat groups (per-planet ^PLANET_STATS) and the discovery list.</summary>
public sealed record PlanetSnapshot(JToken? Universe, JArray? StatGroups, JArray? Discoveries);

public static class LiveSaveReader
{
    /// <summary>Decrypts a save straight from disk for PlanetsPage's live
    /// follow, using whichever character is active (main game, or the
    /// expedition root when XTp is "Season" - same rule as
    /// SaveSessionManager). Read-only; returns null if the file can't be read
    /// yet (e.g. the game is still writing it).</summary>
    public static Task<PlanetSnapshot?> ReadAsync(string path) => Task.Run(async () =>
    {
        try
        {
            string json = await SaveStreamProcessor.ExtractRawJsonAsync(path);
            var root = JObject.Parse(json.TrimEnd('\0'));
            bool expedition = root[NmsSaveContext.ActiveContextKey]?.Value<string>() == NmsSaveContext.ExpeditionContextValue
                && root[NmsSaveContext.ExpeditionRootKey]?["6f="] is JObject;
            var player = root[expedition ? NmsSaveContext.ExpeditionRootKey : NmsSaveContext.MainRootKey]?["6f="];
            return new PlanetSnapshot(player?["yhJ"], player?["gUR"] as JArray, root["fDu"]?["ETO"]?["OsQ"]?["?fB"] as JArray);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiveSaveReader] Couldn't read {Path.GetFileName(path)}: {ex.Message}");
            return (PlanetSnapshot?)null;
        }
    });
}
