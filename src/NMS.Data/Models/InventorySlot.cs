namespace NMS.WinUI3.Models;

public enum SlotState
{
    Locked,
    UnlockedEmpty,
    Occupied
}

public class InventorySlot
{
    public int X { get; set; }
    public int Y { get; set; }
    public SlotState State { get; set; } = SlotState.Locked;

    // Occupied Data
    public string ItemId { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int MaxQuantity { get; set; }

    // UI Helpers
    public string QuantityDisplay => State == SlotState.Occupied && MaxQuantity > 1
        ? $"{CurrentQuantity} / {MaxQuantity}"
        : CurrentQuantity > 0 ? CurrentQuantity.ToString() : string.Empty;

    public double ChargePercentage => MaxQuantity > 0 ? (double)CurrentQuantity / MaxQuantity : 0.0;
}