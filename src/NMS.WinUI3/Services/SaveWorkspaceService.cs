using NMS.Core;
using NMS.Core.NmsModels;
using NMS.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NMS.WinUI3.Services;

public static class SaveWorkspaceService
{
    public static async Task<List<SaveSlotOverview>> InitializeFolderScanAsync(string folderPath)
    {
        return await Task.Run(async () =>
        {
            var results = new List<SaveSlotOverview>();

            // Working folder lives next to the EXE, not in per-user AppData, so this
            // doesn't depend on a machine-specific path other users won't have.
            string workingRoot = Path.Combine(AppContext.BaseDirectory, "Working");

            Directory.CreateDirectory(workingRoot);
            Debug.WriteLine($"Working directory created at: {workingRoot}");

            // Strict filter: only files that start with "save" and end with ".hg"
            var hgFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).StartsWith("save", StringComparison.OrdinalIgnoreCase) &&
                            Path.GetExtension(f).Equals(".hg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Correct numeric grouping
            var slotGroups = hgFiles
                .GroupBy(f =>
                {
                    string name = Path.GetFileNameWithoutExtension(f);

                    // Extract digits from filename (e.g., "save10" -> "10")
                    string digits = new string(name.Where(char.IsDigit).ToArray());

                    int fileIndex = int.TryParse(digits, out int n) ? n : 1; // "save" (no digits) = 1

                    // Slot formula: (1,2) -> 1, (3,4) -> 2, (5,6) -> 3, etc.
                    return ((fileIndex - 1) / 2) + 1;
                })
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    SlotId = g.Key,
                    Files = g.ToList()
                })
                .ToList();

            foreach (var slot in slotGroups)
            {
                try
                {
                    string slotDir = Path.Combine(workingRoot, $"Slot{slot.SlotId}");
                    Directory.CreateDirectory(slotDir);

                    DateTime latestWrite = DateTime.MinValue;
                    NmsSaveFile? latestSave = null;
                    string? latestFilePath = null;

                    foreach (var filePath in slot.Files)
                    {
                        try
                        {
                            string rawJson = await SaveStreamProcessor.ExtractRawJsonAsync(filePath);
                            string jsonPath = Path.Combine(slotDir, $"{Path.GetFileName(filePath)}.json");
                            File.WriteAllText(jsonPath, rawJson);

                            var save = Newtonsoft.Json.JsonConvert.DeserializeObject<NmsSaveFile>(rawJson);
                            var fi = new FileInfo(filePath);

                            // Track the newest file for overview display
                            if (fi.LastWriteTime > latestWrite)
                            {
                                latestWrite = fi.LastWriteTime;
                                latestSave = save;
                                latestFilePath = filePath;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Slot {slot.SlotId}] Failed to decrypt {filePath}: {ex.Message}");
                        }
                    }

                    if (latestSave != null && latestFilePath != null)
                    {
                        results.Add(new SaveSlotOverview
                        {
                            SlotId = slot.SlotId,
                            OriginalFile = latestFilePath,
                            WorkingJsonPath = Path.Combine(slotDir, $"{Path.GetFileName(latestFilePath)}.json"),
                            LastModified = latestWrite,

                            // We DO NOT have GameMode yet
                            GameMode = "UNKNOWN",

                            // Currency comes from Universe (vLc)
                            Units = latestSave.CurrencyData?.Units ?? 0,
                            Nanites = latestSave.CurrencyData?.Nanites ?? 0,
                            Quicksilver = latestSave.CurrencyData?.Quicksilver ?? 0,

                            // SaveName comes from Header (Pk4)
                            PlayerName = latestSave.Header?.SaveName ?? "Unknown",

                            // GalaxyIndex comes from Universe (vLc)
                            GalaxyIndex = latestSave.Universe?.GalaxyIndex ?? -1
                        });

                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Slot {slot.SlotId}] Error processing slot: {ex.Message}");
                }
            }

            return results;
        });
    }
}