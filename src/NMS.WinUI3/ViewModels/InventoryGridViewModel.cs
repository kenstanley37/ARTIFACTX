using CommunityToolkit.Mvvm.ComponentModel;
using NMS.Core.NmsModels;
using NMS.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NMS.WinUI3.ViewModels;

/// <summary>
/// Loads one inventory container from the active save and renders the FULL
/// possible grid (up to max capacity), not just the bounding box of currently
/// unlocked positions - so locked slots the player hasn't purchased yet still
/// show up and can be unlocked from here.
/// </summary>
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

        // Read hl? through the path-aware getter specifically - a staged-but-
        // not-yet-committed unlock only shows up if we ask at its own exact
        // path, since staged edits aren't merged into parent objects when the
        // parent itself is read in bulk (as above).
        string[] unlockedPath = _containerPath.Append("hl?").ToArray();
        var unlockedPositions = SaveSessionManager.GetValue(unlockedPath) is JArray unlockedToken
            ? unlockedToken.ToObject<List<NmsGridPosition>>() ?? container.UnlockedPositions
            : container.UnlockedPositions;

        _unlockedSet = unlockedPositions.Select(p => (p.X, p.Y)).ToHashSet();
        var occupiedByPosition = container.OccupiedSlots
            .Where(s => s.Position is not null)
            .ToDictionary(s => (s.Position!.X, s.Position!.Y));

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
    }

    /// <summary>Stages one newly-unlocked position by writing the entire
    /// updated position list back to the container's hl? path. Nothing
    /// touches disk until the (not-yet-built) global Save commits it.</summary>
    public void UnlockSlot(int x, int y)
    {
        if (!_unlockedSet.Add((x, y))) return;
        StageUnlockedPositions();
        Load();
    }

    /// <summary>Unlocks every currently-locked cell in the rendered grid at once.</summary>
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