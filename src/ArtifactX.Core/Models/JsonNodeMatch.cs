namespace ArtifactX.Core.Models;

public class JsonNodeMatch
{
    public int Index { get; set; }
    public string TargetKey { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public string FullPathDisplay { get; set; } = string.Empty;
    public List<string> TreeLineage { get; set; } = new();
}