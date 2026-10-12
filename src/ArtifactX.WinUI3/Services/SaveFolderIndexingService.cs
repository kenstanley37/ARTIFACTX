using ArtifactX.Core;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// <summary>At most one save file per CPU core is read at a time, across
    /// every account being indexed (each read briefly holds a whole save's JSON).</summary>
    private static readonly SemaphoreSlim ParseGate = new(Math.Max(2, Environment.ProcessorCount));

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

            var entries = await Task.WhenAll(group.OrderBy(Path.GetFileName).Select(f => IndexFileAsync(f, slotDir)));
            indexed.Add((group.Key, entries.ToList()));
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
            // Off the UI thread, streaming only the parts the card shows
            // (NmsSaveSummaryReader). Parsing every save whole - twice, on the
            // UI thread - made Save Selection take ~30 s with six accounts;
            // this reads all six in ~3 s (2026-10-12 timing, same results on
            // every file checked).
            NmsSaveSummary summary;
            await ParseGate.WaitAsync();
            try
            {
                summary = await Task.Run(async () =>
                {
                    DevTimingLog.Write($"  file start {filePath}");
                    string rawJson = await SaveStreamProcessor.ExtractRawJsonAsync(filePath);
                    DevTimingLog.Write($"  decrypted {fi.Name} ({rawJson.Length / 1024} KB)");
                    await File.WriteAllTextAsync(workingJsonPath, rawJson);
                    var result = NmsSaveSummaryReader.Read(rawJson);
                    DevTimingLog.Write($"  file done {filePath}");
                    return result;
                });
            }
            finally
            {
                ParseGate.Release();
            }

            return new SaveFileEntry
            {
                FileName = fi.Name,
                FullPath = filePath,
                WorkingJsonPath = workingJsonPath,
                LastModified = fi.LastWriteTime,
                SaveName = summary.SaveName is { Length: > 0 } name ? name : "Unnamed Save",
                SaveType = summary.SaveType is { Length: > 0 } type ? type : "Unknown",
                // GLOBAL_STATS' TIME stat, falling back to F2P (likely session
                // time, not lifetime total) only when that stat is missing, e.g.
                // a brand-new character - see NmsSaveSummaryReader.
                PlayTimeSeconds = summary.PlayTimeSeconds,
                GalaxyIndex = summary.GalaxyIndex,
                GameMode = summary.GameMode,
                PlayerName = summary.PlayerName,
                IsReadable = true
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