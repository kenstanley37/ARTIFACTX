namespace ArtifactX.WinUI3.ViewModels;

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
    public bool IsSupercharged { get; init; }

    /// <summary>b76 in the save. True on every occupied slot we'd ever seen until
    /// a real test case: a freshly-added item still missing its required parts
    /// showed b76=false, eVk=1.0, which flipped back to true/0.0 after repairing
    /// it in-game. Independent of Amount/MaxAmount (which barely moved in that
    /// same test - likely a charge value for this item, not a repair state).</summary>
    public bool IsFunctional { get; init; } = true;

    /// <summary>eVk in the save - moved in lockstep with IsFunctional in the one
    /// confirmed test (1.0 when broken, 0.0 when repaired), so likely a
    /// malfunction-severity float rather than a flat flag, though only two
    /// data points exist so far (0.0 and 1.0, nothing in between observed).</summary>
    public double MalfunctionSeverity { get; init; }

    public bool IsOccupied => State == InventorySlotState.Occupied;

    public double PixelX => X * 68;
    public double PixelY => Y * 68;

    public string ShortLabel => IsOccupied ? (ItemId?.TrimStart('^') ?? "") : "";

    public string DisplayText => !IsOccupied ? ""
        : MaxAmount > 1 ? $"{Amount}/{MaxAmount}"
        : Amount.ToString();
}