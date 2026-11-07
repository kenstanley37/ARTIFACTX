using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Display-only inventory cells with no save path or editing operations.</summary>
public sealed record InventoryGridSnapshot(int Columns, int Rows, IReadOnlyList<InventorySlotViewModel> Cells);
