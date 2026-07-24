using CommunityToolkit.Mvvm.ComponentModel;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NMS.WinUI3.ViewModels;

public partial class InventoryGridViewModel : ObservableObject
{
    private readonly string[] _containerPath;
    private readonly int _maxColumns;
    private readonly int _maxRows;
    private HashSet<(int X, int Y)> _unlockedSet = new();

    public ObservableCollection<InventorySlotViewModel> Cells { get; } = new();

    [ObservableProperty]
    private int columns;

    [ObservableProperty]
    private int rows;

    [ObservableProperty]
    private bool hasLocalChanges;

    public InventoryGridViewModel(string[] containerPath, int maxColumns, int maxRows)
    {
        _containerPath = containerPath;
        _maxColumns = maxColumns;
        _maxRows = maxRows;
    }

    public double GridPixelWidth => Columns * 68;
    public double GridPixelHeight => Rows * 68;

    partial void OnColumnsChanged(int value) => OnPropertyChanged(nameof(GridPixelWidth));
    partial void OnRowsChanged(int value) => OnPropertyChanged(nameof(GridPixelHeight));

    private string[] NoPath => _containerPath.Append(":No").ToArray();
    private string[] UnlockedPath => _containerPath.Append("hl?").ToArray();

    public void Load()
    {
        Cells.Clear();

        if (SaveSessionManager.GetValue(_containerPath) is not JObject containerToken)
            return;

        var container = containerToken.ToObject<NmsInventoryContainer>();
        if (container is null)
            return;

        // Both unlocked positions AND occupied slots check for a staged
        // whole-array override first - this is the single source of truth
        // for every edit type (unlock, amount change, duplicate), since all
        // of them now stage a complete, freshly-rebuilt array rather than
        // touching one element. That's what avoids index drift when a
        // duplicate changes the array's length/order out from under a
        // previously-staged edit.
        var unlockedPositions = SaveSessionManager.GetValue(UnlockedPath) is JArray unlockedToken
            ? unlockedToken.ToObject<List<NmsGridPosition>>() ?? container.UnlockedPositions
            : container.UnlockedPositions;
        _unlockedSet = unlockedPositions.Select(p => (p.X, p.Y)).ToHashSet();

        var occupiedSlots = SaveSessionManager.GetValue(NoPath) is JArray noToken
            ? noToken.ToObject<List<NmsInventorySlot>>() ?? container.OccupiedSlots
            : container.OccupiedSlots;

        var occupiedByPosition = new Dictionary<(int X, int Y), NmsInventorySlot>();
        foreach (var slot in occupiedSlots)
        {
            if (slot.Position is not null)
                occupiedByPosition[(slot.Position.X, slot.Position.Y)] = slot;
        }

        Columns = _maxColumns;
        Rows = _maxRows;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                occupiedByPosition.TryGetValue((x, y), out var slot);
                bool isUnlocked = _unlockedSet.Contains((x, y));

                var state = slot is not null ? InventorySlotState.Occupied
                    : isUnlocked ? InventorySlotState.UnlockedEmpty
                    : InventorySlotState.Locked;

                Cells.Add(new InventorySlotViewModel
                {
                    X = x,
                    Y = y,
                    State = state,
                    ItemId = slot?.ItemId,
                    CategoryLabel = slot?.Category?.Label,
                    Amount = slot?.Amount ?? 0,
                    MaxAmount = slot?.MaxAmount ?? 0
                });
            }
        }

        HasLocalChanges = SaveSessionManager.HasStagedEditsUnder(_containerPath);
    }

    public void UnlockSlot(int x, int y)
    {
        if (!_unlockedSet.Add((x, y))) return;
        StageUnlockedPositions();
        Load();
    }

    public void UnlockAll()
    {
        bool changed = false;
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
                changed |= _unlockedSet.Add((x, y));

        if (!changed) return;
        StageUnlockedPositions();
        Load();
    }

    /// <summary>Stages a new amount for the item at (x, y) by rebuilding the
    /// whole :No array from current cell state, keyed by grid position - not
    /// by the item's original array index, which can't be trusted to stay
    /// stable once duplicates start changing the array's length.</summary>
    public void StageAmount(int x, int y, int newAmount)
    {
        var target = Cells.FirstOrDefault(c => c.X == x && c.Y == y && c.IsOccupied);
        if (target is null) return;

        var entries = Cells.Where(c => c.IsOccupied).Select(c =>
            (c.X, c.Y) == (x, y)
                ? (c.X, c.Y, c.ItemId, Amount: newAmount, c.MaxAmount, c.CategoryLabel)
                : (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel));

        StageWholeArray(entries);
        Load();
    }

    /// <summary>Duplicates an occupied slot's item into the next free
    /// unlocked-empty cell in this same container. Returns false if no free
    /// slot exists.</summary>
    public bool DuplicateSlot(InventorySlotViewModel sourceCell)
    {
        if (!sourceCell.IsOccupied) return false;

        var target = Cells.FirstOrDefault(c => c.State == InventorySlotState.UnlockedEmpty);
        if (target is null) return false;

        var entries = Cells.Where(c => c.IsOccupied)
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel))
            .Append((target.X, target.Y, sourceCell.ItemId, sourceCell.Amount, sourceCell.MaxAmount, sourceCell.CategoryLabel));

        StageWholeArray(entries);
        Load();
        return true;
    }

    /// <summary>Reverts every staged edit for this container - unlocks and
    /// occupied-slot edits alike.</summary>
    public void Revert()
    {
        SaveSessionManager.RevertEditsUnder(_containerPath);
        Load();
    }

    // Flag defaults (b76: true, 5tH: false, eVk: 0.0) match every real
    // occupied slot observed so far - not currently tracked per-cell, so
    // any rebuild applies them uniformly rather than preserving a slot's
    // actual original values, if they ever differ.
    private void StageWholeArray(IEnumerable<(int X, int Y, string? ItemId, int Amount, int MaxAmount, string? CategoryLabel)> entries)
    {
        var array = new JArray(entries.Select(e => new JObject
        {
            ["Vn8"] = new JObject { ["elv"] = e.CategoryLabel },
            ["b2n"] = e.ItemId,
            ["1o9"] = e.Amount,
            ["F9q"] = e.MaxAmount,
            ["eVk"] = 0.0,
            ["b76"] = true,
            ["5tH"] = false,
            ["3ZH"] = new JObject { [">Qh"] = e.X, ["XJ>"] = e.Y }
        }));

        SaveSessionManager.StageEdit(array, NoPath);
    }

    private void StageUnlockedPositions()
    {
        var array = new JArray(_unlockedSet.Select(p => new JObject
        {
            [">Qh"] = p.X,
            ["XJ>"] = p.Y
        }));

        SaveSessionManager.StageEdit(array, UnlockedPath);
    }
}