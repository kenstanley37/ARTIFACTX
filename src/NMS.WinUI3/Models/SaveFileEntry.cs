using System;

namespace NMS.WinUI3.Models;

public sealed class SaveFileEntry
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public string WorkingJsonPath { get; init; } = string.Empty;
    public DateTime LastModified { get; init; }
    public string SaveName { get; init; } = "Unnamed Save";
    public bool IsManualSave { get; init; }
    public bool IsAutoSave { get; init; }
    public bool IsExpeditionSave { get; init; }
    public int PlayTimeSeconds { get; init; }
    public int GalaxyIndex { get; init; } = -1;
    public bool IsReadable { get; init; } = true;

    public string SaveKindLabel => !IsReadable ? "Unreadable"
        : IsExpeditionSave ? "Expedition"
        : IsManualSave ? "Manual"
        : IsAutoSave ? "Autosave"
        : "Save";

    public string LastModifiedDisplay => LastModified.ToString("MMM d, yyyy h:mm tt");

    public string PlayTimeDisplay
    {
        get
        {
            var t = TimeSpan.FromSeconds(PlayTimeSeconds);
            return $"{(int)t.TotalHours}h {t.Minutes}m playtime";
        }
    }
}