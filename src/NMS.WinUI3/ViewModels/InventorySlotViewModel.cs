namespace NMS.WinUI3.ViewModels;

public enum InventorySlotState
{
    Locked,
    UnlockedEmpty,
    Occupied
}

public sealed class InventorySlotViewModel
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public InventorySlotState State { get; init; }
    public string? ItemId { get; init; }
    public string? CategoryLabel { get; init; }
    public int Amount { get; init; }
    public int MaxAmount { get; init; }

    public bool IsOccupied => State == InventorySlotState.Occupied;

    public double PixelX => X * 68;
    public double PixelY => Y * 68;

    public string ShortLabel => IsOccupied ? (ItemId?.TrimStart('^') ?? "") : "";

    public string DisplayText => !IsOccupied ? ""
        : MaxAmount > 1 ? $"{Amount}/{MaxAmount}"
        : Amount.ToString();
}