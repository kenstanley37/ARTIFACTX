using CommunityToolkit.Mvvm.ComponentModel;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System;
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
    /// Confirmed via a reference-tool diff: it's an independent field, not derived from any
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

    /// <summary>Re-locks an EMPTY slot. Occupied slots are refused - an item
    /// left in a locked slot is unusable in-game and risks being dropped.</summary>
    public void LockSlot(int x, int y)
    {
        if (Cells.Any(c => c.IsOccupied && c.X == x && c.Y == y)) return;
        if (!_unlockedSet.Remove((x, y))) return;
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

    /// <summary>Sets every occupied slot's Amount to its own MaxAmount at
    /// once - same "max out" convenience as InventorySlotGrid's per-slot Max
    /// Qty flyout action, just applied to the whole container in one edit
    /// (e.g. topping off every ore stack in a storage container at once).</summary>
    public void MaxAllQuantities()
    {
        bool changed = false;
        var entries = Cells.Where(c => c.IsOccupied).Select(c =>
        {
            if (c.Amount != c.MaxAmount) changed = true;
            return (c.X, c.Y, c.ItemId, Amount: c.MaxAmount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity);
        }).ToList();

        if (!changed) return;
        StageWholeArray(entries);
        Load();
    }

    /// <summary>Collapses every item that appears in more than one slot into a
    /// single stack, kept at that item's first slot (reading order). The kept
    /// stack's amount is the combined total, capped at the item's own max -
    /// one full stack is plenty, and more can always be duplicated. A working
    /// copy is preferred over a damaged one. Returns how many slots were freed.</summary>
    public int MergeDuplicates()
    {
        var occupied = Cells.Where(c => c.IsOccupied).OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        var merged = new List<(int X, int Y, string? ItemId, int Amount, int MaxAmount, string? CategoryLabel, bool IsFunctional, double MalfunctionSeverity)>();
        int freed = 0;

        foreach (var group in occupied.GroupBy(c => c.ItemId))
        {
            var cells = group.ToList();
            var first = cells[0];
            if (cells.Count == 1)
            {
                merged.Add((first.X, first.Y, first.ItemId, first.Amount, first.MaxAmount, first.CategoryLabel, first.IsFunctional, first.MalfunctionSeverity));
                continue;
            }

            var best = cells.FirstOrDefault(c => c.IsFunctional && c.MalfunctionSeverity <= 0) ?? first;
            int max = cells.Max(c => c.MaxAmount);
            long total = cells.Sum(c => (long)c.Amount);
            int amount = max > 0 ? (int)System.Math.Min(total, max) : (int)System.Math.Min(total, int.MaxValue);

            merged.Add((first.X, first.Y, first.ItemId, amount, max, first.CategoryLabel, best.IsFunctional, best.MalfunctionSeverity));
            freed += cells.Count - 1;
        }

        if (freed == 0) return 0;
        StageWholeArray(merged);
        Load();
        return freed;
    }

    /// <summary>Re-packs every item into the unlocked slots in reading order,
    /// grouped by kind (technology, product, substance) and then by name, so
    /// identical items end up next to each other. Returns false when nothing
    /// moved.</summary>
    public bool SortItems()
    {
        var occupied = Cells.Where(c => c.IsOccupied).ToList();
        if (occupied.Count == 0) return false;

        static int KindOrder(string? category) => category switch
        {
            "Technology" => 0,
            "Product" => 1,
            "Substance" => 2,
            _ => 3,
        };

        var sorted = occupied
            .OrderBy(c => KindOrder(c.CategoryLabel))
            .ThenBy(c => CatalogService.TryGet(c.ItemId)?.DisplayName ?? c.ItemId, System.StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.ItemId, System.StringComparer.Ordinal)
            .ThenByDescending(c => c.Amount)
            .ToList();
        var slots = _unlockedSet.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
        if (slots.Count < sorted.Count) return false;

        var entries = sorted.Select((c, i) => (slots[i].X, slots[i].Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity)).ToList();
        if (entries.Zip(sorted).All(t => t.First.X == t.Second.X && t.First.Y == t.Second.Y)) return false;

        StageWholeArray(entries);
        Load();
        return true;
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

    /// <summary>Puts each item into a free unlocked slot, in one write. Returns
    /// how many fit; the rest are left out. With `groupOf` (item id -> its
    /// type, e.g. the tech's main stat) each item goes next to the most items
    /// of its own type, so technology gets the game's adjacency boost (same-type
    /// tech side by side lights up and boosts each other - user, 2026-10-13);
    /// otherwise, or with no such neighbour, the top-left free slot.</summary>
    public int AddItemsToFreeSlots(IEnumerable<(string ItemId, string? CategoryLabel, int Amount, int MaxAmount)> items,
        Func<string, string?>? groupOf = null)
    {
        var free = Cells.Where(c => c.State == InventorySlotState.UnlockedEmpty)
            .OrderBy(c => c.Y).ThenBy(c => c.X)
            .Select(c => (c.X, c.Y))
            .ToList();
        // Type of what's in each slot so far (existing items, then each one placed).
        var typeAt = new Dictionary<(int, int), string?>();
        if (groupOf is not null)
            foreach (var c in Cells.Where(c => c.IsOccupied && c.ItemId is not null))
                typeAt[(c.X, c.Y)] = groupOf(c.ItemId!.TrimStart('^'));

        var added = new List<(int X, int Y, string? ItemId, int Amount, int MaxAmount, string? CategoryLabel, bool IsFunctional, double MalfunctionSeverity)>();
        foreach (var item in items)
        {
            if (free.Count == 0) break;
            string id = item.ItemId.TrimStart('^');
            string? type = groupOf?.Invoke(id);
            var slot = groupOf is null ? free[0] : PickSlot(free, typeAt, type);
            free.Remove(slot);
            typeAt[slot] = type;
            added.Add((slot.X, slot.Y, "^" + id, item.Amount, item.MaxAmount, item.CategoryLabel, true, 0.0));
        }
        if (added.Count == 0) return 0;

        var entries = Cells.Where(c => c.IsOccupied)
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity))
            .Concat(added);
        StageWholeArray(entries);
        Load();
        return added.Count;
    }

    /// <summary>The free slot for an item of `type`: touching as many of its
    /// own type as possible and as few others as possible, so a type with
    /// nothing placed yet starts where it borders the least other tech and
    /// doesn't take the room other families need to grow (first try dropped
    /// them in the first free slot, mid-family, 2026-10-13). `free` is
    /// top-left first, so ties stay top-left.</summary>
    private static (int X, int Y) PickSlot(List<(int X, int Y)> free, Dictionary<(int, int), string?> typeAt, string? type)
    {
        (int Same, int Other) Neighbours((int X, int Y) s)
        {
            int same = 0, other = 0;
            foreach (var n in new[] { (s.X - 1, s.Y), (s.X + 1, s.Y), (s.X, s.Y - 1), (s.X, s.Y + 1) })
                if (typeAt.TryGetValue(n, out var t))
                {
                    if (type is not null && t == type) same++;
                    else other++;
                }
            return (same, other);
        }
        return free.OrderByDescending(s => Neighbours(s).Same)
            .ThenBy(s => Neighbours(s).Other)
            .First();
    }

    /// <summary>Rearranges the items into one block per type (`groupOf`, e.g.
    /// the tech's main stat), so same-type tech sits together and gets the
    /// game's adjacency boost - for families that got boxed in as tech was
    /// added (user, 2026-10-13). Items in supercharged slots stay put (a
    /// player puts their best tech there on purpose): their families grow out
    /// from them first. Every other family, biggest first, takes one connected
    /// block - the free cells nearest (breadth-first) to the first free slot,
    /// in reading order, that has room for all of it. Two earlier tries
    /// (greedy growth, then a snake path) both split families round the
    /// supercharged slots on a real ship; this one was simulated on that ship
    /// and ~13,000 random layouts first: families only split when no
    /// connected room is left (0.13%). Untyped items fill in last. Only
    /// positions change, in one write. Returns how many items moved.</summary>
    public int ArrangeForAdjacency(Func<string, string?> groupOf)
    {
        var occupied = Cells.Where(c => c.IsOccupied && c.ItemId is not null).ToList();
        var staying = occupied.Where(c => c.IsSupercharged).ToList();
        var moving = occupied.Where(c => !c.IsSupercharged).ToList();
        if (moving.Count == 0) return 0;

        var typeOf = occupied.ToDictionary(c => (c.X, c.Y), c => groupOf(c.ItemId!.TrimStart('^')));
        var typeAt = staying.ToDictionary(c => ((int, int))(c.X, c.Y), c => typeOf[(c.X, c.Y)]);
        var free = Cells.Where(c => c.State == InventorySlotState.UnlockedEmpty || (c.IsOccupied && !c.IsSupercharged))
            .OrderBy(c => c.Y).ThenBy(c => c.X)
            .Select(c => (c.X, c.Y))
            .ToList();

        var anchoredTypes = staying.Select(c => typeOf[(c.X, c.Y)]).Where(t => t is not null).ToHashSet();
        var families = moving
            .Where(c => typeOf[(c.X, c.Y)] is not null)
            .GroupBy(c => typeOf[(c.X, c.Y)]!)
            .Select(g => (Type: g.Key, Items: g.OrderBy(c => c.Y).ThenBy(c => c.X).ToList()))
            .OrderBy(f => anchoredTypes.Contains(f.Type) ? 0 : 1)
            .ThenByDescending(f => f.Items.Count)
            .ToList();

        var entries = staying
            .Select(c => (c.X, c.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity))
            .ToList();
        int moved = 0;
        void Put(InventorySlotViewModel c, (int X, int Y) slot)
        {
            free.Remove(slot);
            typeAt[slot] = typeOf[(c.X, c.Y)];
            if (slot != (c.X, c.Y)) moved++;
            entries.Add((slot.X, slot.Y, c.ItemId, c.Amount, c.MaxAmount, c.CategoryLabel, c.IsFunctional, c.MalfunctionSeverity));
        }

        // Free cells reachable from `starts` through free cells, nearest first.
        List<(int X, int Y)> Nearest(IEnumerable<(int X, int Y)> starts, int count)
        {
            var freeSet = free.ToHashSet();
            var queue = new Queue<(int X, int Y)>(starts);
            var seen = new HashSet<(int X, int Y)>(starts);
            var found = new List<(int X, int Y)>();
            while (queue.Count > 0 && found.Count < count)
            {
                var (x, y) = queue.Dequeue();
                foreach (var n in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    if (freeSet.Contains(n) && seen.Add(n))
                    {
                        queue.Enqueue(n);
                        found.Add(n);
                        if (found.Count == count) break;
                    }
            }
            return found;
        }

        foreach (var family in families)
        {
            List<(int X, int Y)> block;
            if (anchoredTypes.Contains(family.Type))
                // Grow out from its supercharged member(s).
                block = Nearest(staying.Where(c => typeOf[(c.X, c.Y)] == family.Type).Select(c => (c.X, c.Y)), family.Items.Count);
            else
                // The first free slot with room for the whole family in one piece.
                block = free.Select(seed => Nearest(new[] { seed }, family.Items.Count - 1).Prepend(seed).ToList())
                    .FirstOrDefault(b => b.Count == family.Items.Count) ?? new();
            int i = 0;
            foreach (var c in family.Items)
                // No room left in one piece: next to its own type where possible.
                Put(c, i < block.Count ? block[i++] : PickSlot(free, typeAt, family.Type));
        }
        // Untyped items in whatever is left.
        foreach (var c in moving.Where(c => typeOf[(c.X, c.Y)] is null))
            Put(c, free[0]);

        if (moved == 0) return 0;
        StageWholeArray(entries);
        Load();
        return moved;
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
    public NmsLoadoutTemplate ExtractTemplate(string name, string? sourceToolName, string sourceKind)
    {
        var template = new NmsLoadoutTemplate
        {
            Name = name,
            SourceToolName = sourceToolName,
            SourceKind = sourceKind,
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
        // 5tH is the game's "added automatically" flag: true on the tech a
        // corvette's built parts put in its tech grid (every one of the 172
        // such items in the real saves, 2026-10-13; false or absent on every
        // other ship). Rewriting the array used to reset it to false for all
        // of them - keep it for any item id that already had it.
        var automatic = (SaveSessionManager.GetValue(NoPath) as JArray ?? new JArray())
            .OfType<JObject>()
            .Where(o => o["5tH"]?.Type == JTokenType.Boolean && o["5tH"]!.Value<bool>())
            .Select(o => o["b2n"]?.Value<string>())
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);

        var array = new JArray(entries.Select(e => new JObject
        {
            ["Vn8"] = new JObject { ["elv"] = e.CategoryLabel },
            ["b2n"] = e.ItemId,
            ["1o9"] = e.Amount,
            ["F9q"] = e.MaxAmount,
            ["eVk"] = e.MalfunctionSeverity,
            ["b76"] = e.IsFunctional,
            ["5tH"] = e.ItemId is not null && automatic.Contains(e.ItemId),
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