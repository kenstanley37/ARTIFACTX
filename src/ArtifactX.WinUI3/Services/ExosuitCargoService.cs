using System.Linq;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Helpers.Inventory;
using ArtifactX.WinUI3.ViewModels;

namespace ArtifactX.WinUI3.Services;

/// <summary>What happened when an item was put in the Exosuit.</summary>
public enum ExosuitAddResult { Added, Full, NoSave, Unknown }

/// <summary>
/// Puts one of an item in the Exosuit's cargo, staged like any edit (whole slot
/// array, Reset undoes it, written only on Save, which is blocked while the game
/// runs). It tops up an existing stack of the item that has room, else takes the
/// first free unlocked slot, with the same stack cap the Exosuit page uses
/// (StackCaps). Used by the Frigates page's Feed Vessel list.
/// </summary>
public static class ExosuitCargoService
{
    public static ExosuitAddResult AddOne(string itemId)
    {
        if (!SaveSessionManager.IsSaveLoaded) return ExosuitAddResult.NoSave;
        if (CatalogService.TryGetStackInfo(itemId) is not var (category, maxStack)) return ExosuitAddResult.Unknown;
        int cap = StackCaps.For(category, maxStack, StackCaps.CargoProductMultiplier);

        var cargo = new InventoryGridViewModel(NmsInventoryContainer.ExosuitCargoPath, ExosuitCapacity.CargoColumns, ExosuitCapacity.CargoRows);
        cargo.Load();
        string id = itemId.TrimStart('^');
        var stack = cargo.Cells.FirstOrDefault(c => c.IsOccupied && c.Amount < c.MaxAmount
            && string.Equals(c.ItemId?.TrimStart('^'), id, System.StringComparison.OrdinalIgnoreCase));
        if (stack is not null)
        {
            cargo.StageAmount(stack.X, stack.Y, stack.Amount + 1);
            return ExosuitAddResult.Added;
        }
        return cargo.AddItemsToFreeSlots([(id, category, 1, cap)]) > 0 ? ExosuitAddResult.Added : ExosuitAddResult.Full;
    }
}
