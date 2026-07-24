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

    public void Load()
    {
        Cells.Clear();

        if (SaveSessionManager.GetValue(_containerPath) is not JObject containerToken)
            return;

        var container = containerToken.ToObject<NmsInventoryContainer>();
        if (container is null)
            return;

        string[] unlockedPath = _containerPath.Append("hl?").ToArray();
        var unlockedPositions = SaveSessionManager.GetValue(unlockedPath) is JArray unlockedToken
            ? unlockedToken.ToObject<List<NmsGridPosition>>() ?? container.UnlockedPositions
            : container.UnlockedPositions;

        _unlockedSet = unlockedPositions.Select(p => (p.X, p.Y)).ToHashSet();

        // Track each occupied slot's original array index alongside its data -
        // needed both to stage amount edits at the right :No[i] position, and
        // (below) to re-check each one individually for a staged amount edit,
        // since a staged edit three levels under the container's own path
        // isn't visible when the container is read in bulk, as above.
        var occupiedByPosition = new Dictionary<(int X, int Y), (NmsInventorySlot Slot, int Index)>();
        for (int i = 0; i < container.OccupiedSlots.Count; i++)
        {
            var slot = container.OccupiedSlots[i];
            if (slot.Position is not null)
                occupiedByPosition[(slot.Position.X, slot.Position.Y)] = (slot, i);
        }

        Columns = _maxColumns;
        Rows = _maxRows;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                occupiedByPosition.TryGetValue((x, y), out var occupied);
                var slot = occupied.Slot;
                bool isUnlocked = _unlockedSet.Contains((x, y));

                var state = slot is not null ? InventorySlotState.Occupied
                    : isUnlocked ? InventorySlotState.UnlockedEmpty
                    : InventorySlotState.Locked;

                int amount = slot?.Amount ?? 0;
                if (slot is not null)
                {
                    var amountPath = _containerPath.Concat(new[] { ":No", occupied.Index.ToString(), "1o9" }).ToArray();
                    if (SaveSessionManager.GetValue(amountPath) is { } stagedAmount)
                        amount = stagedAmount.Value<int>();
                }

                Cells.Add(new InventorySlotViewModel
                {
                    X = x,
                    Y = y,
                    State = state,
                    ItemId = slot?.ItemId,
                    CategoryLabel = slot?.Category?.Label,
                    Amount = amount,
                    MaxAmount = slot?.MaxAmount ?? 0,
                    OccupiedIndex = occupied.Index
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

    /// <summary>Stages a new amount for an already-occupied slot, identified
    /// by its :No array index.</summary>
    public void StageAmount(int occupiedIndex, int newAmount)
    {
        var path = _containerPath.Concat(new[] { ":No", occupiedIndex.ToString(), "1o9" }).ToArray();
        SaveSessionManager.StageEdit(newAmount, path);
        Load();
    }

    /// <summary>Reverts every staged edit for this container - unlocks and
    /// amount edits alike.</summary>
    public void Revert()
    {
        SaveSessionManager.RevertEditsUnder(_containerPath);
        Load();
    }

    private void StageUnlockedPositions()
    {
        var array = new JArray(_unlockedSet.Select(p => new JObject
        {
            [">Qh"] = p.X,
            ["XJ>"] = p.Y
        }));

        SaveSessionManager.StageEdit(array, _containerPath.Append("hl?").ToArray());
    }
}