namespace NMS.Core.Models;

public class SaveFileCard
{
    public string FileName { get; set; } = string.Empty;       // "save9.hg"
    public string FullPath { get; set; } = string.Empty;       // Full disk path
    public string LastModified { get; set; } = string.Empty;   // "July 12, 2026, 11:34 AM"
    public string GameVersion { get; set; } = "Unknown";       // From "8>q"
    public string Units { get; set; } = "0";                  // From "wGS"
    public string Nanites { get; set; } = "0";                // From "7QL"
    public string Quicksilver { get; set; } = "0";            // From "kN;"
}