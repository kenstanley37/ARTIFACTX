using Newtonsoft.Json;
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
        var results = new List<SaveSlotGroup>();

        foreach (var group in slotFiles)
        {
            string slotDir = Path.Combine(workingRoot, $"Slot{group.Key}");
            Directory.CreateDirectory(slotDir);

            var entries = new List<SaveFileEntry>();
            foreach (var filePath in group.OrderBy(Path.GetFileName))
                entries.Add(await IndexFileAsync(filePath, slotDir));

            results.Add(new SaveSlotGroup
            {
                SlotId = group.Key,
                Files = entries,
                SourceDisplayName = candidate.DisplayName
            });
        }

        return results;
    }

    // Slot pairing: (save.hg, save2.hg) -> Slot 1, (save3.hg, save4.hg) -> Slot 2, etc.
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

            return new SaveFileEntry
            {
                FileName = fi.Name,
                FullPath = filePath,
                WorkingJsonPath = workingJsonPath,
                LastModified = fi.LastWriteTime,
                SaveName = save?.Header?.SaveName is { Length: > 0 } name ? name : "Unnamed Save",
                SaveType = save?.SaveType is { Length: > 0 } type ? type : "Unknown",
                PlayTimeSeconds = save?.PlayTimeSeconds ?? 0,
                GalaxyIndex = save?.Universe?.GalaxyIndex ?? -1,
                GameMode = save?.Universe?.GameMode ?? "Unknown",
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