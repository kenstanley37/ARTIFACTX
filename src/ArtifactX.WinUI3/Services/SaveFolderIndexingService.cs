using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ArtifactX.Core;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The single place raw .hg files get decrypted. Runs once per candidate per app
/// session: decompresses each save file, writes the decrypted JSON to that
/// candidate's working folder, and reads header metadata from that same pass.
/// Everything after this (folder selection, browsing slots, eventually opening a
/// save to edit) reads the results already sitting in memory or on disk — nothing
/// re-decrypts anything.
/// </summary>
public static class SaveFolderIndexingService
{
    public static async Task<List<SaveSlotGroup>> IndexAsync(SaveFolderCandidate candidate)
    {
        string workingRoot = Path.Combine(AppContext.BaseDirectory, "Working", candidate.WorkingFolderKey);
        Directory.CreateDirectory(workingRoot);

        var hgFiles = Directory.GetFiles(candidate.FolderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f).StartsWith("save", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetExtension(f).Equals(".hg", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var slotFiles = hgFiles.GroupBy(SlotIdFor).OrderBy(g => g.Key);
        var indexed = new List<(int SlotId, List<SaveFileEntry> Files)>();

        foreach (var group in slotFiles)
        {
            string slotDir = Path.Combine(workingRoot, $"Slot{group.Key}");
            Directory.CreateDirectory(slotDir);

            var entries = new List<SaveFileEntry>();
            foreach (var filePath in group.OrderBy(Path.GetFileName))
                entries.Add(await IndexFileAsync(filePath, slotDir));
            indexed.Add((group.Key, entries));
        }

        // No Steam name (another platform, or Steam doesn't list the account):
        // use the player name from the newest save, so the card says who plays here.
        if (string.IsNullOrWhiteSpace(candidate.PlayerName))
            candidate.PlayerName = indexed.SelectMany(g => g.Files)
                .Where(f => !string.IsNullOrWhiteSpace(f.PlayerName))
                .OrderByDescending(f => f.LastModified)
                .Select(f => f.PlayerName)
                .FirstOrDefault();

        return indexed.Select(g => new SaveSlotGroup
        {
            SlotId = g.SlotId,
            Files = g.Files,
            SourceDisplayName = candidate.DisplayName
        }).ToList();
    }

    /// <summary>Reads GLOBAL_STATS' "TIME" stat (a float, in seconds) - the
    /// save's actual lifetime play time, discovered 2026-08-06 after F2P
    /// (NmsSaveFile.PlayTimeSeconds) turned out to read far too small for an
    /// established character (see that property's doc comment). Mirrors
    /// MilestonesPage's identically-shaped ResolveGlobalStatGroupIndex/
    /// ResolveStatIndex scan, but against a plain JObject instead of
    /// SaveSessionManager - no slot is loaded into that static session yet
    /// during folder indexing, this runs once per file before the user has
    /// even picked a slot. Returns 0 if the stat isn't present (e.g. a
    /// brand-new character that hasn't accumulated it) rather than throwing -
    /// a missing lifetime-stats entry isn't a corrupt/unreadable file.</summary>
    private static int ReadTotalPlayTimeSeconds(JObject root)
    {
        if (root["vLc"]?["6f="]?["gUR"] is not JArray groups) return 0;

        foreach (var group in groups)
        {
            string groupId = (group[":rc"]?.Value<string>() ?? "").TrimStart('^');
            if (!string.Equals(groupId, "GLOBAL_STATS", StringComparison.OrdinalIgnoreCase)) continue;

            if (group["gUR"] is not JArray stats) return 0;

            foreach (var stat in stats)
            {
                string statId = (stat["b2n"]?.Value<string>() ?? "").TrimStart('^');
                if (!string.Equals(statId, "TIME", StringComparison.OrdinalIgnoreCase)) continue;

                double? floatValue = stat[">MX"]?["eoL"]?.Value<double?>();
                return floatValue.HasValue ? (int)Math.Round(floatValue.Value) : 0;
            }
            return 0;
        }
        return 0;
    }

    // Slot pairing: (save.hg, save2.hg) -> Slot 1, (save3.hg, save4.hg) -> Slot 2, etc.
    /// <summary>The player's own name: the discoverer name (ksu V?:) on their
    /// newest own discovery - one with an owner id (f5Q); records downloaded
    /// from other players have none (see NmsPlanets).</summary>
    private static string? ReadPlayerName(JObject root)
    {
        if (root["fDu"]?["ETO"]?["OsQ"]?["?fB"] is not JArray discoveries) return null;
        string? name = null;
        long newest = long.MinValue;
        foreach (var record in discoveries)
        {
            if (record["ksu"] is not JObject owner || string.IsNullOrEmpty(owner["f5Q"]?.Value<string>())) continue;
            long time = owner["3I1"]?.Type == JTokenType.Integer ? owner["3I1"]!.Value<long>() : 0;
            if (time < newest || owner["V?:"]?.Value<string>() is not { Length: > 0 } candidate) continue;
            newest = time;
            name = candidate;
        }
        return name;
    }

    private static int SlotIdFor(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        string digits = new string(name.Where(char.IsDigit).ToArray());
        int fileIndex = int.TryParse(digits, out int n) ? n : 1;
        return ((fileIndex - 1) / 2) + 1;
    }

    private static async Task<SaveFileEntry> IndexFileAsync(string filePath, string slotDir)
    {
        var fi = new FileInfo(filePath);
        string workingJsonPath = Path.Combine(slotDir, $"{fi.Name}.json");

        try
        {
            string rawJson = await SaveStreamProcessor.ExtractRawJsonAsync(filePath);
            await File.WriteAllTextAsync(workingJsonPath, rawJson);

            var save = JsonConvert.DeserializeObject<NmsSaveFile>(rawJson);

            // Parsed separately from the typed NmsSaveFile above - GLOBAL_STATS
            // is a variable-order "find by id" structure (see
            // NmsPlayerStatsPaths), not a fixed JsonProperty path, so it needs
            // the same runtime scan MilestonesPage does via SaveSessionManager -
            // except no slot is loaded into SaveSessionManager yet at this
            // point (this runs during the folder scan, before the user has
            // picked a slot), so ReadTotalPlayTimeSeconds re-does that same
            // scan directly against this file's own freshly-parsed JObject.
            var root = JObject.Parse(rawJson.TrimEnd('\0', ' ', '\r', '\n'));
            int totalPlayTimeSeconds = ReadTotalPlayTimeSeconds(root);
            string? playerName = ReadPlayerName(root);

            return new SaveFileEntry
            {
                FileName = fi.Name,
                FullPath = filePath,
                WorkingJsonPath = workingJsonPath,
                LastModified = fi.LastWriteTime,
                SaveName = save?.Header?.SaveName is { Length: > 0 } name ? name : "Unnamed Save",
                SaveType = save?.SaveType is { Length: > 0 } type ? type : "Unknown",
                // Falls back to F2P (see its doc comment - likely session
                // time, not lifetime total) only if GLOBAL_STATS' TIME stat
                // couldn't be found at all, e.g. a brand-new character that
                // hasn't accumulated it yet - better an approximate number
                // than always showing 0.
                PlayTimeSeconds = totalPlayTimeSeconds > 0 ? totalPlayTimeSeconds : save?.PlayTimeSeconds ?? 0,
                GalaxyIndex = save?.Universe?.GalaxyIndex ?? -1,
                GameMode = save?.Universe?.GameMode ?? "Unknown",
                PlayerName = playerName,
                IsReadable = save is not null
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveFolderIndexingService] Failed to index {filePath}: {ex.Message}");
            return new SaveFileEntry
            {
                FileName = fi.Name,
                FullPath = filePath,
                LastModified = fi.LastWriteTime,
                SaveName = "Unreadable",
                IsReadable = false
            };
        }
    }
}