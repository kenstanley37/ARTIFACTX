using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Adapts the generated cabinet inventory without creating or staging a save container.</summary>
internal static class StationToolInventory
{
    public static InventoryGridSnapshot Create(GeneratedMultiToolInventory inventory)
    {
        var installed = inventory.Technologies.ToDictionary(t => t.Position);
        var unlocked = inventory.Layout.UnlockedSlots.ToHashSet();
        var supercharged = inventory.SuperchargedSlots.ToHashSet();
        var cells = new List<InventorySlotViewModel>();
        for (int y = 0; y < inventory.Layout.Height; y++)
        for (int x = 0; x < inventory.Layout.Width; x++)
        {
            var position = new GeneratedInventoryPosition(x, y);
            installed.TryGetValue(position, out var technology);
            cells.Add(new InventorySlotViewModel
            {
                X = x, Y = y,
                State = technology is not null ? InventorySlotState.Occupied
                    : unlocked.Contains(position) ? InventorySlotState.UnlockedEmpty : InventorySlotState.Locked,
                ItemId = technology?.Id,
                DisplayName = technology is null ? null : CatalogService.TryGet(technology.Id)?.DisplayName ?? "Technology not in catalog",
                IsSupercharged = supercharged.Contains(position),
                ShowAmount = false,
            });
        }
        return new InventoryGridSnapshot(inventory.Layout.Width, inventory.Layout.Height, cells);
    }
}
