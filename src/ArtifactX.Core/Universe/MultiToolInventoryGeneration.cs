namespace ArtifactX.Core.Universe;

/// <summary>Inclusive coordinate bounds and maximum special-slot count from the inventory table.</summary>
public sealed record SuperchargedSlotRules(int MaxX, int MaxY, int MaximumCount);

/// <summary>One generated inventory coordinate; it may still be locked in the initial inventory.</summary>
public readonly record struct GeneratedInventoryPosition(int X, int Y);

/// <summary>Inventory-table rectangle thresholds, retaining their native small/standard/large order.</summary>
public sealed record InventoryLayoutBounds(int SmallWidth, int SmallHeight, int StandardWidth,
    int StandardHeight, int LargeWidth, int LargeHeight);

/// <summary>A weapon size's initial tech-slot count range and rectangle thresholds.</summary>
public sealed record MultiToolLayoutRules(string SizeType, int MinSlots, int MaxSlots, InventoryLayoutBounds Bounds);

/// <summary>Requested slot count and generated unlocked coordinates are deliberately separate.</summary>
public sealed record GeneratedMultiToolLayout(int Width, int Height, int RequestedSlots,
    GeneratedInventoryPosition[] UnlockedSlots);

/// <summary>One already selected technology placed in its initial unlocked inventory.</summary>
public sealed record GeneratedInstalledTechnology(string Id, GeneratedInventoryPosition Position,
    IReadOnlyList<GeneratedModuleStat>? ModuleStats = null);

/// <summary>A resolved initial inventory for a supported loadout, retaining any module stat rolls.</summary>
public sealed record GeneratedMultiToolInventory(GeneratedMultiToolLayout Layout,
    IReadOnlyList<GeneratedInstalledTechnology> Technologies, IReadOnlyList<GeneratedInventoryPosition> SuperchargedSlots);

/// <summary>Native initial multi-tool inventory generation steps. Does not edit player inventories.</summary>
public static class MultiToolInventoryGeneration
{
    /// <summary>
    /// Combines extracted inventory-tag lookup, layout, default regular selection, placement
    /// and special slots. Caller must resolve the selected model's actual inventory tag,
    /// native wealth/progression, unlocked technologies, cycling mode and component overrides.
    /// Overrides and procedural winners are unsupported and return null, never a guessed inventory.
    /// </summary>
    public static GeneratedMultiToolInventory? InitialRegularInventory(ulong inventorySeed, int ratingIndex,
        string inventoryTag, InventoryGenerationTables inventoryTables, MultiToolTechnologyTables technologyTables,
        int nativeWealth, int requiredLevel, IReadOnlySet<string> knownTechnologies, bool cycleRows,
        bool hasTechnologyOverrides)
        => InitialInventoryCore(inventorySeed, ratingIndex, inventoryTag, inventoryTables, technologyTables,
            nativeWealth, requiredLevel, knownTechnologies, cycleRows, hasTechnologyOverrides, false);

    /// <summary>
    /// Initial inventory including supported ordinary procedural modules. Runtime inputs
    /// and the selected model's inventory tag must be resolved by the caller. Component
    /// overrides, special-quality modules and missing metadata yield no inventory.
    /// </summary>
    public static GeneratedMultiToolInventory? InitialInventory(ulong inventorySeed, int ratingIndex,
        string inventoryTag, InventoryGenerationTables inventoryTables, MultiToolTechnologyTables technologyTables,
        int nativeWealth, int requiredLevel, IReadOnlySet<string> knownTechnologies, bool cycleRows,
        bool hasTechnologyOverrides)
        => InitialInventoryCore(inventorySeed, ratingIndex, inventoryTag, inventoryTables, technologyTables,
            nativeWealth, requiredLevel, knownTechnologies, cycleRows, hasTechnologyOverrides, true);

    private static GeneratedMultiToolInventory? InitialInventoryCore(ulong inventorySeed, int ratingIndex,
        string inventoryTag, InventoryGenerationTables inventoryTables, MultiToolTechnologyTables technologyTables,
        int nativeWealth, int requiredLevel, IReadOnlySet<string> knownTechnologies, bool cycleRows,
        bool hasTechnologyOverrides, bool includeProcedural)
    {
        if (hasTechnologyOverrides || inventoryTables.WeaponLayoutByInventoryTag is not { } byTag
            || !byTag.TryGetValue(inventoryTag, out var size) || inventoryTables.WeaponLayouts is not { } layouts)
            return null;
        var matching = layouts.Where(r => r.SizeType == size).ToArray();
        if (matching.Length != 1) return null;
        var layout = InitialLayout(inventorySeed, matching[0], cycleRows);
        if (layout is null) return null;
        var technologies = includeProcedural
            ? MultiToolTechnologyGeneration.SelectAndPlaceLoadout(inventorySeed,
                technologyTables, layout, nativeWealth, requiredLevel, knownTechnologies)
            : MultiToolTechnologyGeneration.SelectAndPlaceRegularLoadout(inventorySeed,
                technologyTables, layout, nativeWealth, requiredLevel, knownTechnologies);
        var special = InitialSuperchargedSlots(inventorySeed, ratingIndex, inventoryTables.WeaponSuperchargedSlots);
        return technologies is null || special is null ? null : new GeneratedMultiToolInventory(layout, technologies, special);
    }

    /// <summary>
    /// Native automatic placement at 0x1404e71c0: first unlocked, unoccupied, unbroken
    /// position in row order. Supercharged positions do not block placement. The caller
    /// supplies the selected technology order and any broken positions; this does not
    /// choose the loadout or generate module seeds. Returns null if a whole loadout cannot fit.
    /// </summary>
    public static IReadOnlyList<GeneratedInstalledTechnology>? PlaceTechnologies(GeneratedMultiToolLayout layout,
        IReadOnlyList<string> selectedTechnologyIds, IReadOnlyCollection<GeneratedInventoryPosition>? brokenPositions = null)
    {
        if (layout.Width < 1 || layout.Height < 1 || layout.UnlockedSlots is null
            || selectedTechnologyIds.Any(string.IsNullOrWhiteSpace)) return null;
        var broken = brokenPositions?.ToHashSet() ?? [];
        var available = layout.UnlockedSlots.Distinct()
            .Where(p => p.X >= 0 && p.X < layout.Width && p.Y >= 0 && p.Y < layout.Height && !broken.Contains(p))
            .OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
        if (selectedTechnologyIds.Count > available.Length) return null;
        return selectedTechnologyIds.Select((id, index) => new GeneratedInstalledTechnology(id, available[index])).ToArray();
    }

    /// <summary>
    /// Ports 0x1404ce5c0/0x1404ce6c0 and category-3 layout at 0x1404cfeb0.
    /// Count rolling and row placement each restart the inventory-seed stream. A row gets a
    /// random contiguous run; repeated passes may overlap. The runtime gate at manager
    /// +0x26380 controls cycling until the requested count is reached; its meaning remains
    /// unresolved, so the caller must supply it explicitly. This does not choose the size row.
    /// Verified 2026-10-25 against 600 native layouts (100 seeds, three size rows, both modes).
    /// WeaponSmall/cycling reproduces all 12 purchased Mass Needler unlocked coordinates.
    /// </summary>
    public static GeneratedMultiToolLayout? InitialLayout(ulong inventorySeed, MultiToolLayoutRules? rules, bool cycleRows)
    {
        if (rules is null || rules.Bounds is null || rules.MinSlots < 0 || rules.MaxSlots < rules.MinSlots || rules.MaxSlots > short.MaxValue)
            return null;
        int requested = rules.MinSlots + (int)SolarLayoutGeneration.RandomFor(inventorySeed)
            .Next((uint)(rules.MaxSlots - rules.MinSlots + 1));
        var bounds = rules.Bounds;
        (int width, int height) = requested <= (long)bounds.SmallWidth * bounds.SmallHeight
            ? (bounds.SmallWidth, bounds.SmallHeight)
            : requested <= (long)bounds.StandardWidth * bounds.StandardHeight
                ? (bounds.StandardWidth, bounds.StandardHeight) : (bounds.LargeWidth, bounds.LargeHeight);
        if (width is < 1 or > 64 || height is < 1 or > 15 || requested > width * height) return null;
        var rng = SolarLayoutGeneration.RandomFor(inventorySeed);
        var slots = new HashSet<GeneratedInventoryPosition>();
        int remaining = requested, row = 0, attempts = 0;
        while (remaining > 0 && (cycleRows || row < height))
        {
            if (++attempts > 4096) return null;
            int length = 1 + (int)rng.Next((uint)Math.Min(remaining, width));
            int start = (int)rng.Next((uint)(width - length + 1));
            for (int x = start; x < start + length; x++)
                if (row < height) slots.Add(new GeneratedInventoryPosition(x, row));
            remaining = cycleRows ? requested - slots.Count : remaining - length;
            row++;
            // Preserve the native <= comparison: cycling visits one invisible row at
            // index Height before wrapping, consuming two more draws without adding slots.
            if (cycleRows && row > height) row = 0;
        }
        return new GeneratedMultiToolLayout(width, height, requested,
            slots.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray());
    }

    /// <summary>
    /// Initial supercharged positions at NMS build 180836, 0x1404d2350 (inventory category 3).
    /// Uses the WeaponMedium generation row, inclusive X/Y bounds, independent inventory-seed
    /// MWC, two draws per attempt and retries duplicate coordinates. Class C/B/A/S gets
    /// min(class+1, table maximum) slots. Does not clip positions to currently unlocked slots.
    /// Verified 2026-10-25 against 400 native emulator outputs, and the purchased B Mass
    /// Needler's saved MMm positions (2,0), (1,3). Existing special-slot updates are out of scope.
    /// </summary>
    public static IReadOnlyList<GeneratedInventoryPosition>? InitialSuperchargedSlots(
        ulong inventorySeed, int ratingIndex, SuperchargedSlotRules? rules)
    {
        if (rules is null || ratingIndex is < 0 or > 3 || rules.MaxX is < 0 or >= int.MaxValue
            || rules.MaxY is < 0 or >= int.MaxValue || rules.MaximumCount < 0) return null;
        int count = Math.Min(ratingIndex + 1, rules.MaximumCount);
        // Native code skips placement when either configured maximum index is zero.
        if (rules.MaxX == 0 || rules.MaxY == 0) return [];
        if ((long)(rules.MaxX + 1) * (rules.MaxY + 1) < count) return null;
        var result = new List<GeneratedInventoryPosition>(count);
        var seen = new HashSet<GeneratedInventoryPosition>();
        var rng = SolarLayoutGeneration.RandomFor(inventorySeed);
        while (result.Count < count)
        {
            var position = new GeneratedInventoryPosition(
                (int)rng.Next((uint)rules.MaxX + 1), (int)rng.Next((uint)rules.MaxY + 1));
            if (seen.Add(position)) result.Add(position);
        }
        return result;
    }
}
