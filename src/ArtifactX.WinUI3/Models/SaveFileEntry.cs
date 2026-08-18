using System;

namespace ArtifactX.WinUI3.Models;

public sealed class SaveFileEntry
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public string WorkingJsonPath { get; init; } = string.Empty;
    public DateTime LastModified { get; init; }
    public string SaveName { get; init; } = "Unnamed Save";
    public string SaveType { get; init; } = "Unknown";
    public int PlayTimeSeconds { get; init; }
    public int GalaxyIndex { get; init; } = -1;
    public bool IsReadable { get; init; } = true;
    public string GameMode { get; init; } = "Unknown";

    public bool HasKnownGameMode => IsReadable && GameMode != "Unknown";

    public string LastModifiedDisplay => LastModified.ToString("MMM d, yyyy h:mm tt");

    public string PlayTimeDisplay
    {
        get
        {
            var t = TimeSpan.FromSeconds(PlayTimeSeconds);
            return $"{(int)t.TotalHours}h {t.Minutes}m playtime";
        }
    }

    /// <summary>Timestamp + playtime on one line, for compact card layouts
    /// that can't afford a row each.</summary>
    public string SummaryDisplay => $"{LastModifiedDisplay} · {PlayTimeDisplay}";
}