using System;

namespace ArtifactX.WinUI3.Models;

public class SaveSlotOverview
{
    public int SlotId { get; set; }

    // Original .hg file path
    public string OriginalFile { get; set; } = string.Empty;

    // Path to decrypted JSON in working directory
    public string WorkingJsonPath { get; set; } = string.Empty;

    // File timestamp
    public DateTime LastModified { get; set; }

    // Known-safe fields from your tested models
    public string GameMode { get; set; } = "UNKNOWN";
    public long Units { get; set; }
    public long Nanites { get; set; }
    public long Quicksilver { get; set; }

    public string PlayerName { get; set; } = "Unknown";
    public int GalaxyIndex { get; set; }

    // Optional: UI convenience property
    public string DisplayName => $"Slot {SlotId}: {PlayerName}";
}