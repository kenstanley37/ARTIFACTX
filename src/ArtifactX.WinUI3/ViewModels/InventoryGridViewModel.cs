using CommunityToolkit.Mvvm.ComponentModel;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

public partial class InventoryGridViewModel : ObservableObject
{
    private readonly string[] _containerPath;
    private readonly int _maxColumns;
    private readonly int _maxRows;
    private HashSet<(int X, int Y)> _unlockedSet = new();
    private HashSet<(int X, int Y)> _superchargedSet = new();

    public ObservableCollection<InventorySlotViewModel> Cells { get; } = new();

    [ObservableProperty]
    private int columns;

    [ObservableProperty]
    private int rows;

    [ObservableProperty]
    private bool hasLocalChanges;

    /// <summary>The container's rarity/quality rating (S/A/B/C) - a plain literal
    /// string at B@N.1o6, sibling of :No/hl?/etc within this same container.
    /// Confirmed via NomNom diff: it's an independent field, not derived from any
    /// procedural seed (the top-level @EL identity hash) - so setting it directly
    /// is safe and doesn't touch appearance/stats seeding at all.</summary>
    [ObservableProperty]
    private string? currentClass;

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
    private string[] SuperchargedPath => _containerPath.Append("MMm").ToArray();
    private string[] ClassPath => _containerPath.Append("B@N").Append("1o6").ToArray();

    public void Load()
    {
        Cells.Clear();

        if (SaveSessionManager.GetValue(_containerPath) is not JObject containerToken)
            return;

        var container = containerToken.ToObject<NmsInventoryContainer>();
        if (container is null)
            return;

        CurrentClass = SaveSessionManager.GetValue(ClassPath)?.Value<string>()
            ?? containerToken["B@N"]?["1o6"]?.Value<string>();

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

        // MMm has no typed model yet - each entry wraps its position under "3ZH"
        // alongside a "Vn8"/"TechBonus" tag, unlike hl?'s flat position list - so
        // this reads the raw token directly rather than going through a container
        // property, exactly like NoPath/UnlockedPath do before their ToObject<T>() call.
        var superchargedToken = SaveSessionManager.GetValue(SuperchargedPath) as JArray
            ?? containerToken["MMm"] as JArray;

        _superchargedSet = (superchargedToken ?? new JArray())
            .Select(entry => entry["3ZH"])
            .Where(pos => pos is not null)
            .Select(pos => (X: (int)pos![">Qh"]!, Y: (int)pos["XJ>"]!))
            .ToHashSet();

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
                    MaxAmount = slot?.MaxAmount ?? 0,
                    IsSupercharged = _superchargedSet.Contains((x, y)),
                    IsFunctional = slot?.FlagB76 ?? true,
                    MalfunctionSeverity = slot?.FlagEvk ?? 0.0
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

    /// <summary>Flips the supercharged flag for one slot. Only meaningful on an
    /// unlocked slot (occupied or empty) - locked slots can't be supercharged
    /// in-game, so callers shouldn't offer this on locked cells.</summary>
    public void ToggleSupercharge(int x, int y)
    {
        if (!_unlockedSet.Contains((x, y))) return;

        if (!_superchargedSet.Remove((x, y)))
            _superchargedSet.Add((x, y));

        StageSuperchargedPositions();
        Load();
    }

    public void SuperchargeAll()
    {
        bool changed = false;
        foreach (var pos in _unlockedSet)
            changed |= _superchargedSet.Add(pos);

        if (!changed) return;
        StageSuperchargedPositions();
        Load();
    }

    /// <summary>Sets one occupied slot's IsFunctional/MalfunctionSeverity (b76/eVk)
    /// back to healthy (true/0.0), confirmed via a real before/after test: a
    /// freshly-added item missing required parts showed b76=false, eVk=1.0, which
    /// flipped to true/0.0 after repairing it in-game. Deliberately does NOT touch
    /// Amount/MaxAmount - those barely moved in that same test (likely a charge
    /// value for that particular item, not a repair state), so forcing them to
    /// MaxAmount would have been guessing at the wrong field, the same mistake
    /// this replaces.</summary>
    public void Repair(int x, int y)
    {
        var target = Cells.FirstOrDefault(c => c.X == x && c.Y == y && c.IsOccupied);
        if (target is null || (target.IsFunctional && target.MalfunctionSeverity == 0.0)) return;

        var entries = Cells.Where(c => c.IsOccupied).Select(c =>
            (c.X, c.Y) == (x, y)
                ? (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, IsFunctional: true, MalfunctionSeverity: 0.0)
                : (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity));

        StageWholeArray(entries);
        Load();
    }

    /// <summary>Repairs (see Repair) every occupied slot in this container at once.</summary>
    public void RepairAll()
    {
        bool changed = false;
        var entries = Cells.Where(c => c.IsOccupied).Select(c =>
        {
            if (!c.IsFunctional || c.MalfunctionSeverity != 0.0) changed = true;
            return (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, IsFunctional: true, MalfunctionSeverity: 0.0);
        }).ToList();

        if (!changed) return;
        StageWholeArray(entries);
        Load();
    }

    /// <summary>Sets this container's rarity/quality class (S/A/B/C). A plain
    /// single-letter string write - confirmed independent of the identity seed
    /// (@EL), so this can't corrupt or desync the item's appearance/stats.</summary>
    public void SetClass(string classLetter)
    {
        SaveSessionManager.StageEdit(classLetter, ClassPath);
        Load();
    }

    /// <summary>Stages a new amount for the item at (x, y) by rebuilding the
    /// whole :No array from current cell state, keyed by grid position - not
    /// by the item's original array index, which can't be trusted to stay
    /// stable once duplicates start changing the array's length. Preserves the
    /// slot's actual IsFunctional/MalfunctionSeverity rather than resetting them -
    /// editing amount shouldn't silently "cure" an unrelated malfunction state.</summary>
    public void StageAmount(int x, int y, int newAmount)
    {
        var target = Cells.FirstOrDefault(c => c.X == x && c.Y == y && c.IsOccupied);
        if (target is null) return;

        var entries = Cells.Where(c => c.IsOccupied).Select(c =>
            (c.X, c.Y) == (x, y)
                ? (c.X, c.Y, c.ItemId, Amount: newAmount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity)
                : (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity));

        StageWholeArray(entries);
        Load();
    }

    /// <summary>Duplicates an occupied slot's item into the next free
    /// unlocked-empty cell in this same container. Returns false if no free
    /// slot exists. The duplicate starts healthy (true/0.0) regardless of the
    /// source's actual state - a fresh copy shouldn't inherit a malfunction.</summary>
    public bool DuplicateSlot(InventorySlotViewModel sourceCell)
    {
        if (!sourceCell.IsOccupied) return false;

        var target = Cells.FirstOrDefault(c => c.State == InventorySlotState.UnlockedEmpty);
        if (target is null) return false;

        var entries = Cells.Where(c => c.IsOccupied)
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity))
            .Append((target.X, target.Y, sourceCell.ItemId, sourceCell.Amount, sourceCell.MaxAmount, sourceCell.CategoryLabel, true, 0.0));

        StageWholeArray(entries);
        Load();
        return true;
    }

    /// <summary>Places a new item at (x, y), overwriting whatever was there.
    /// Returns false if the slot isn't unlocked. Used by both the catalog
    /// search "Add item" flow and cross-grid drag/drop. Starts healthy (true/0.0),
    /// same reasoning as DuplicateSlot.</summary>
    public bool AddItem(int x, int y, string itemId, string? categoryLabel, int amount, int maxAmount)
    {
        if (!_unlockedSet.Contains((x, y))) return false;

        // Every item reference in the save file carries a leading "^" (e.g. "^LASER")
        // - it's a save-file convention, not part of the raw catalog GameId, which is
        // why CatalogService.TryGet strips it back off before cache lookups. Catalog
        // search results never have it, so it has to be added back here or the game's
        // parser rejects the whole save as corrupt (confirmed the hard way - this was
        // exactly the "player state read as fresh/new" bug).
        string normalizedItemId = itemId.StartsWith('^') ? itemId : "^" + itemId;

        var entries = Cells.Where(c => c.IsOccupied && (c.X, c.Y) != (x, y))
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity))
            .Append((x, y, normalizedItemId, amount, maxAmount, categoryLabel, true, 0.0));

        StageWholeArray(entries);
        Load();
        return true;
    }

    /// <summary>Clears whatever item occupies (x, y). No-op if already empty.</summary>
    public void RemoveItem(int x, int y)
    {
        var entries = Cells.Where(c => c.IsOccupied && (c.X, c.Y) != (x, y))
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity));

        StageWholeArray(entries);
        Load();
    }

    /// <summary>Moves an occupied slot to another unlocked position within this
    /// same grid, swapping the two items if the destination is also occupied.
    /// Returns false if the source is empty or the destination isn't unlocked.
    /// Preserves each item's own IsFunctional/MalfunctionSeverity through the
    /// move/swap - relocating an item shouldn't change its condition.</summary>
    public bool MoveItem(int fromX, int fromY, int toX, int toY)
    {
        if (fromX == toX && fromY == toY) return false;
        if (!_unlockedSet.Contains((toX, toY))) return false;

        var source = Cells.FirstOrDefault(c => c.X == fromX && c.Y == fromY && c.IsOccupied);
        if (source is null) return false;

        var target = Cells.FirstOrDefault(c => c.X == toX && c.Y == toY);

        var entries = Cells.Where(c => c.IsOccupied && (c.X, c.Y) != (fromX, fromY) && (c.X, c.Y) != (toX, toY))
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity))
            .Append((toX, toY, source.ItemId, source.Amount, source.MaxAmount, source.CategoryLabel, source.IsFunctional, source.MalfunctionSeverity));

        if (target is { IsOccupied: true })
            entries = entries.Append((fromX, fromY, target.ItemId, target.Amount, target.MaxAmount, target.CategoryLabel, target.IsFunctional, target.MalfunctionSeverity));

        StageWholeArray(entries);
        Load();
        return true;
    }

    /// <summary>Reverts every staged edit for this container - unlocks and
    /// occupied-slot edits alike.</summary>
    /// <summary>Replaces this container's entire tech loadout - :No AND whichever
    /// hl? positions are needed to fit it - with a copy of another tool's,
    /// optionally also matching its Class. Positions transfer directly (1:1):
    /// every multi-tool shares the same 10x6 grid regardless of class - unlocking
    /// is a separate progression from class rating, not gated by it, so no
    /// remapping is needed. Unlocked positions are UNIONED with whatever this
    /// container already had, never re-locking anything the target didn't need
    /// for this specific stack. Caller is responsible for warning the user about
    /// newly-unlocked slots and any class difference before calling this -
    /// this method just performs the copy once confirmed.</summary>
    public void CopyTechStackFrom(InventoryGridViewModel source, bool alsoMatchClass)
    {
        var sourceEntries = source.Cells
            .Where(c => c.IsOccupied)
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity))
            .ToList();

        bool unlockChanged = false;
        foreach (var entry in sourceEntries)
            unlockChanged |= _unlockedSet.Add((entry.X, entry.Y));

        if (unlockChanged)
            StageUnlockedPositions();

        StageWholeArray(sourceEntries);

        if (alsoMatchClass && !string.IsNullOrEmpty(source.CurrentClass))
            SaveSessionManager.StageEdit(source.CurrentClass, ClassPath);

        Load();
    }

    /// <summary>Captures this container's current occupied cells + the positions
    /// they need unlocked + Class into a standalone template, independent of
    /// this specific tool - the whole point is for it to outlive the tool it
    /// was captured from, so a build can be re-applied to a brand-new tool
    /// later instead of re-buying every piece of tech again.</summary>
    public NmsLoadoutTemplate ExtractTemplate(string name, string? sourceToolName)
    {
        var template = new NmsLoadoutTemplate
        {
            Name = name,
            SourceToolName = sourceToolName,
            SourceClass = CurrentClass
        };

        foreach (var cell in Cells.Where(c => c.IsOccupied))
        {
            template.TechItems.Add(new NmsLoadoutTechItem
            {
                X = cell.X,
                Y = cell.Y,
                ItemId = cell.ItemId,
                Amount = cell.Amount,
                MaxAmount = cell.MaxAmount,
                CategoryLabel = cell.CategoryLabel,
                IsFunctional = cell.IsFunctional,
                MalfunctionSeverity = cell.MalfunctionSeverity
            });

            // Only positions that actually hold an item need to be reproduced as
            // "must be unlocked" on the target - an unlocked-but-empty slot in
            // the source doesn't need to come along for the ride.
            template.UnlockedPositions.Add(new NmsLoadoutPosition { X = cell.X, Y = cell.Y });
        }

        return template;
    }

    /// <summary>Applies a saved template to this container - same semantics as
    /// CopyTechStackFrom: replaces :No entirely, unions in whatever hl?
    /// positions the template needs (never re-locking anything else), and only
    /// changes Class if the caller opts in.</summary>
    public void ApplyTemplate(NmsLoadoutTemplate template, bool alsoMatchClass)
    {
        var entries = template.TechItems
            .Select(i => (i.X, i.Y, i.ItemId, i.Amount, i.MaxAmount, i.CategoryLabel, i.IsFunctional, i.MalfunctionSeverity))
            .ToList();

        bool unlockChanged = false;
        foreach (var pos in template.UnlockedPositions)
            unlockChanged |= _unlockedSet.Add((pos.X, pos.Y));

        if (unlockChanged)
            StageUnlockedPositions();

        StageWholeArray(entries);

        if (alsoMatchClass && !string.IsNullOrEmpty(template.SourceClass))
            SaveSessionManager.StageEdit(template.SourceClass, ClassPath);

        Load();
    }

    public void Revert()
    {
        SaveSessionManager.RevertEditsUnder(_containerPath);
        Load();
    }

    /// <summary>Rebuilds the whole :No array from the given entries, preserving
    /// each one's actual IsFunctional/MalfunctionSeverity (b76/eVk) rather than
    /// hardcoding true/0.0 the way this used to. That old behavior silently
    /// "cured" any malfunctioning item sitting in the container every time ANY
    /// unrelated edit (add, move, remove, amount change) touched the array -
    /// confirmed via a real test showing b76/eVk genuinely can differ
    /// (false/1.0 on an item missing required parts, true/0.0 once repaired).</summary>
    private void StageWholeArray(IEnumerable<(int X, int Y, string? ItemId, int Amount, int MaxAmount, string? CategoryLabel, bool IsFunctional, double MalfunctionSeverity)> entries)
    {
        var array = new JArray(entries.Select(e => new JObject
        {
            ["Vn8"] = new JObject { ["elv"] = e.CategoryLabel },
            ["b2n"] = e.ItemId,
            ["1o9"] = e.Amount,
            ["F9q"] = e.MaxAmount,
            ["eVk"] = e.MalfunctionSeverity,
            ["b76"] = e.IsFunctional,
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

    private void StageSuperchargedPositions()
    {
        var array = new JArray(_superchargedSet.Select(p => new JObject
        {
            ["Vn8"] = new JObject { ["QA1"] = "TechBonus" },
            ["3ZH"] = new JObject { [">Qh"] = p.X, ["XJ>"] = p.Y }
        }));

        SaveSessionManager.StageEdit(array, SuperchargedPath);
    }
}