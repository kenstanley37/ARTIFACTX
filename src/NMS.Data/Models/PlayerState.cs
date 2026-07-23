namespace NMS.Data.Models;

public class PlayerState
{
    public int Id { get; set; }
    public int SaveSessionId { get; set; }
    public string GameVersionToken { get; set; } = "Unknown";
    // Unified Game Currencies
    public long Units { get; set; }
    public long Nanites { get; set; }
    public long Quicksilver { get; set; }

    // Relationship mapping
    public SaveSession? SaveSession { get; set; }
}