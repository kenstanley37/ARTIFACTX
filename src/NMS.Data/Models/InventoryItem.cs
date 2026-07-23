using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NMS.Data.Models;

public class InventoryItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string ItemId { get; set; } = string.Empty;

    public int SlotIndex { get; set; }
    public int Quantity { get; set; }

    public int SaveSessionId { get; set; }

    [ForeignKey(nameof(SaveSessionId))]
    public SaveSession? Session { get; set; }
}