using NMS.Core.NmsModels;

namespace NMS.Data.Models;

public class SaveSession
{
    public int Id { get; set; }
    public string OriginalFilePath { get; set; } = string.Empty;
    public DateTime LastBackupTime { get; set; }
    public string GameVersionToken { get; set; } = string.Empty; // Maps our parsed "8>q" or similar tag

    // Navigation property for currency values
    public PlayerState? PlayerState { get; set; }

    public List<InventorySlot> InventorySlots { get; set; } = new();
}