namespace ArtifactX.GameData.Models;

public class PakInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Hash { get; set; }
}
