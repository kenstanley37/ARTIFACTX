using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>
/// A starship size row from <c>INVENTORYTABLE.MBIN</c> <c>GenerationData.GenerationDataPerSizeType</c>.
/// </summary>
public sealed record StarshipLayoutRules(
    string SizeType,
    int SizeTypeIndex,
    int MinSlots,
    int MaxSlots,
    int MinTechSlots,
    int MaxTechSlots,
    InventoryLayoutBounds Bounds,
    InventoryLayoutBounds TechBounds);

/// <summary>
/// A starship class cost row from <c>INVENTORYTABLE.MBIN</c> <c>ShipCostData.InventoryCostData</c>.
/// </summary>
public sealed record StarshipCostRules(
    int MinSlots,
    float MinValueInMillions,
    int MaxSlots,
    float MaxValueInMillions,
    float CoolMultiplier,
    float TradeInMultiplier,
    float[] ClassMultiplier);

/// <summary>
/// One <c>GcCustomInventoryComponentData</c> node in a procedural starship scene, gated by the
/// descriptor option IDs on its ancestor scene nodes.
/// </summary>
public sealed record StarshipInventoryNodeRule(
    string[] RequiredDescriptors,
    int SizeTypeIndex,
    bool Cool);

/// <summary>
/// Deterministic inventory slot counts, broken-slot counts, full unit value, and intrinsic base stat
/// bonuses for a crashed starship site.
/// </summary>
public sealed record CrashedShipInventorySummary(
    string SizeType,
    int CargoSlots,
    int TechSlots,
    int BrokenCargoSlots,
    int BrokenTechSlots,
    long UnitValue,
    float DamageBonus,
    float ShieldBonus,
    float HyperdriveBonus,
    float ManeuverabilityBonus)
{
    public int TotalSlots => CargoSlots + TechSlots;
    public int BrokenSlots => BrokenCargoSlots + BrokenTechSlots;
    public int UnbrokenSlots => TotalSlots - BrokenSlots;

    /// <summary>Slot string shown by the Analysis Visor (<c>0x1412E2672</c>: <c>Cargo+Tech</c>).</summary>
    public string VisorSlots => $"{CargoSlots}+{TechSlots}";

    /// <summary>
    /// Slot string shown on the Compare / Claim screen (<c>0x1406B8680</c>): <c>Cargo / Tech</c> when
    /// <see cref="BrokenSlots"/> is 0 (Sentinel Interceptors), else <c>Broken / Unbroken</c>.
    /// </summary>
    public string CompareScreenSlots =>
        BrokenSlots > 0 ? $"{BrokenSlots} / {UnbrokenSlots}" : $"{CargoSlots} / {TechSlots}";
}

/// <summary>
/// Crashed starship inventory layout, broken-slot count, full unit value, and base-stat generation.
///
/// Reverse-engineered and verified against live game memory and hooks (Steam build 26295378, 2026-10-29;
/// see <c>docs/research/CRASHED_SHIPS.md</c>):
/// <list type="bullet">
/// <item><description><c>0x1404E8AE0</c> / <c>0x1404E65C0</c> / <c>0x141129D60</c>: walks active scene nodes of the starship's
/// <c>.SCENE.MBIN</c> (selected by <see cref="ShipParts.Generate"/> from the ship's model seed) and takes
/// <c>max(LayoutSizeType)</c> across all <c>GcCustomInventoryComponentData.Size</c> entries.</description></item>
/// <item><description><c>0x1404D7420</c>: Cargo (<c>MinSlots..MaxSlots</c>) and Tech (<c>MinTechSlots..MaxTechSlots</c>)
/// slot counts each roll on the first draw of a fresh MWC stream seeded with the DistressSignal site key.</description></item>
/// <item><description><c>0x1404D8D10</c>: Cargo (<c>edi = 4</c>, <c>0x1404D8DE0</c>) unlocks pairs of horizontally adjacent
/// slots (<c>x, x+1</c>) per row cycling <c>0..height</c>; Tech (<c>edi = 5</c>, <c>0x1404D9209</c>) unlocks contiguous
/// runs per row identical to <see cref="MultiToolInventoryGeneration.InitialLayout"/> with <c>cycleRows = true</c>.</description></item>
/// <item><description><c>0x1404DBCA0</c>: non-Sentinel crashed ships (<c>shipClass != 9</c>) break unlocked slots past
/// the skipped/occupied slots (<c>3</c> in Cargo, the selected installed technologies in Tech) using a 1-draw MWC seeded with
/// <c>cellIndex = y * width + x</c> against <c>CrashedShipBrokenSlotChance</c> (<c>0.75</c>). Verified 11/11 wrecks.</description></item>
/// <item><description><c>0x1404DC500</c>: full starship unit value from <c>ShipCostData.InventoryCostData[shipClass]</c>
/// and <c>CargoSlots</c>. Verified 9/9 wrecks to the exact unit.</description></item>
/// <item><description><c>0x1404D7910</c>: intrinsic base-stat bonuses from <c>ShipBaseStatsData[shipClass].BaseStatsPerClass[class]</c>
/// on a fresh MWC stream seeded with the site key. Verified 3/3 Sentinel Interceptors.</description></item>
/// </list>
/// </summary>
public static class StarshipInventoryGeneration
{
    private const ulong Multiplier = 0x5A76F899UL;
    private const double TinyDouble = 2.3283064370807974e-10;

    public static string? ScenePath(SystemStarshipType type) => type switch
    {
        SystemStarshipType.Fighter => "MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTER_PROC.SCENE.MBIN",
        SystemStarshipType.Hauler => "MODELS/COMMON/SPACECRAFT/DROPSHIPS/DROPSHIP_PROC.SCENE.MBIN",
        SystemStarshipType.Explorer => "MODELS/COMMON/SPACECRAFT/SCIENTIFIC/SCIENTIFIC_PROC.SCENE.MBIN",
        SystemStarshipType.Shuttle => "MODELS/COMMON/SPACECRAFT/SHUTTLE/SHUTTLE_PROC.SCENE.MBIN",
        SystemStarshipType.Exotic => "MODELS/COMMON/SPACECRAFT/S-CLASS/S-CLASS_PROC.SCENE.MBIN",
        SystemStarshipType.Solar => "MODELS/COMMON/SPACECRAFT/SAILSHIP/SAILSHIP_PROC.SCENE.MBIN",
        SystemStarshipType.SentinelInterceptor => "MODELS/COMMON/SPACECRAFT/SENTINELSHIP/SENTINELSHIP_PROC.SCENE.MBIN",
        _ => null,
    };

    /// <summary>
    /// Resolves a starship model's <c>GcInventoryLayoutSizeType</c> and <c>Cool</c> flag (<c>0x1404E65C0</c>)
    /// from the catalog's scene inventory rules and <see cref="ShipParts.Generate"/>.
    /// </summary>
    public static (int SizeTypeIndex, string SizeType, bool Cool)? ResolveModelSize(
        SystemStarshipType type,
        ulong modelSeed,
        Func<string, IReadOnlyList<PartGroup>?>? loadTree,
        InventoryGenerationTables? tables)
    {
        if (tables?.ShipModelInventoryRules is not { } byType
            || !byType.TryGetValue(type.ToString(), out var rules)
            || rules is not { Length: > 0 }
            || tables.ShipLayouts is not { } layouts)
            return null;

        int maxSize = -1;
        bool cool = false;

        // When every rule for this ship type is unconditional (Exotic, Solar, SentinelInterceptor),
        // the size does not depend on descriptor selection.
        if (rules.All(r => r.RequiredDescriptors.Length == 0))
        {
            foreach (var r in rules)
            {
                if (r.SizeTypeIndex > maxSize) maxSize = r.SizeTypeIndex;
                if (r.Cool) cool = true;
            }
        }
        else
        {
            if (loadTree is null || ScenePath(type) is not { } scene) return null;
            var picked = ShipParts.Generate(scene, modelSeed, loadTree)
                .Select(p => p.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (picked.Count == 0) return null;

            foreach (var r in rules)
            {
                if (r.RequiredDescriptors.All(picked.Contains))
                {
                    if (r.SizeTypeIndex > maxSize) maxSize = r.SizeTypeIndex;
                    if (r.Cool) cool = true;
                }
            }
        }

        if (maxSize < 0) return null;
        var layout = layouts.FirstOrDefault(l => l.SizeTypeIndex == maxSize);
        return layout is null ? null : (maxSize, layout.SizeType, cool);
    }

    /// <summary>
    /// Ports <c>0x1404D7420</c> (Cargo slot count roll), <c>0x1404D7520</c> (Cargo grid bounds),
    /// and <c>0x1404D8DE0</c> (category-4 Ship Cargo valid-slot bitmask pass: 2 horizontally adjacent
    /// slots per row, cycling <c>0..height</c> where <c>row == height</c> consumes one draw without
    /// adding slots to rows <c>&lt; height</c>).
    /// </summary>
    public static GeneratedMultiToolLayout? CargoLayout(ulong siteKey, StarshipLayoutRules? rules)
    {
        if (rules?.Bounds is not { } bounds || rules.MinSlots < 0 || rules.MaxSlots < rules.MinSlots || rules.MaxSlots > short.MaxValue)
            return null;

        int requested = rules.MinSlots + (int)SolarLayoutGeneration.RandomFor(siteKey)
            .Next((uint)(rules.MaxSlots - rules.MinSlots + 1));

        (int width, int height) = requested <= (long)bounds.SmallWidth * bounds.SmallHeight
            ? (bounds.SmallWidth, bounds.SmallHeight)
            : requested <= (long)bounds.StandardWidth * bounds.StandardHeight
                ? (bounds.StandardWidth, bounds.StandardHeight)
                : (bounds.LargeWidth, bounds.LargeHeight);

        if (width is < 2 or > 64 || height is < 1 or > 15 || requested > width * height)
            return null;

        var rng = SolarLayoutGeneration.RandomFor(siteKey);
        var slots = new HashSet<GeneratedInventoryPosition>();
        int remaining = requested, row = 0, attempts = 0;
        while (remaining > 0)
        {
            if (++attempts > 4096) return null;
            int x = (int)rng.Next((uint)(width - 1));
            if (row < height)
            {
                slots.Add(new GeneratedInventoryPosition(x, row));
                if (requested - slots.Count > 0)
                    slots.Add(new GeneratedInventoryPosition(x + 1, row));
            }
            remaining = requested - slots.Count;
            row++;
            if (row > height) row = 0;
        }

        return new GeneratedMultiToolLayout(width, height, requested,
            slots.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray());
    }

    /// <summary>
    /// Ports <c>0x1404D7420</c> (Tech slot count roll) and <c>0x1404D9209</c> (category-5 Ship Tech
    /// valid-slot bitmask pass, identical to <see cref="MultiToolInventoryGeneration.InitialLayout"/>
    /// with <c>cycleRows = true</c>).
    /// </summary>
    public static GeneratedMultiToolLayout? TechLayout(ulong siteKey, StarshipLayoutRules? rules)
    {
        if (rules?.TechBounds is not { } techBounds) return null;
        return MultiToolInventoryGeneration.InitialLayout(
            siteKey,
            new MultiToolLayoutRules(rules.SizeType, rules.MinTechSlots, rules.MaxTechSlots, techBounds),
            cycleRows: true);
    }

    /// <summary>
    /// Ports <c>0x1404DBCA0</c>: counts broken slots in a crashed starship inventory.
    /// In Cargo, <paramref name="occupiedOrSkippedSlots"/> is <c>3</c> (<c>skipCount = 3</c>);
    /// in Tech, it is <c>6</c> (the 6 core ship technologies placed in the first 6 unlocked Tech slots).
    /// Each remaining unlocked slot <c>(x, y)</c> seeds a 1-draw MWC from <c>cellIndex = y * width + x</c>.
    /// </summary>
    public static int BrokenSlotCount(
        GeneratedMultiToolLayout layout,
        int occupiedOrSkippedSlots,
        float brokenSlotChance = 0.75f,
        int minNonBrokenSlots = 3)
    {
        if (brokenSlotChance <= 0f || layout.UnlockedSlots is not { Length: > 0 } unlocked)
            return 0;

        int totalUnlocked = unlocked.Length;
        if (totalUnlocked <= minNonBrokenSlots) return 0;

        int broken = 0;
        for (int i = Math.Max(0, occupiedOrSkippedSlots); i < totalUnlocked; i++)
        {
            if (totalUnlocked - broken <= minNonBrokenSlots) break;
            var pos = unlocked[i];
            uint cellIndex = (uint)(pos.Y * layout.Width + pos.X);
            uint lo = cellIndex == 0 ? 1u : cellIndex;
            uint carry = ((cellIndex >> 16) | (cellIndex << 16)) ^ cellIndex;
            uint word = (uint)((ulong)lo * Multiplier + carry);
            float roll = (float)(word * TinyDouble);
            if (roll < brokenSlotChance)
                broken++;
        }
        return broken;
    }

    /// <summary>
    /// Ports <c>0x1404DC500</c>: calculates the starship's full unit value from
    /// <c>ShipCostData.InventoryCostData[shipClass]</c> and <paramref name="cargoSlots"/>.
    /// Verified 9/9 crashed starships to the exact unit.
    /// </summary>
    public static long? CalculateUnitValue(
        SystemStarshipType type,
        int classIndex,
        int cargoSlots,
        bool cool,
        InventoryGenerationTables? tables)
    {
        int shipClass = (int)type;
        if (tables?.ShipCostData is not { } costTable
            || shipClass < 0 || shipClass >= costTable.Length
            || costTable[shipClass] is not { ClassMultiplier.Length: >= 4 } d
            || classIndex is < 0 or > 3
            || d.MaxSlots <= 0 || d.MinSlots <= 0
            || d.MaxValueInMillions <= 0f || d.MinValueInMillions <= 0f)
            return null;

        float slots = MathF.Min(cargoSlots, 48f);
        const float ln2 = 0.69314718f;
        float num = MathF.Log2(d.MinValueInMillions / d.MaxValueInMillions) * ln2;
        float den = MathF.Log2((float)d.MinSlots / d.MaxSlots) * ln2;
        float exp = num / den;
        float x0 = MathF.Pow(d.MaxValueInMillions * 1_000_000f, 1f / exp);
        float baseCost = MathF.Pow((x0 / d.MaxSlots) * slots, exp);
        float coolBonus = cool ? (baseCost * d.CoolMultiplier / 100f) : 0f;
        float classBonus = baseCost * d.ClassMultiplier[classIndex] / 100f;
        float cost = baseCost + coolBonus + classBonus;
        float log10Cost = (MathF.Log2(cost) * ln2) / (MathF.Log2(10f) * ln2);
        int digits = (int)(log10Cost + 0.5f);
        float step = MathF.Pow(10f, digits) / 200f;
        float rounded = MathF.Round(cost / step) * step;
        return (long)Math.Round(rounded);
    }

    /// <summary>
    /// Ports <c>0x1404D7910</c>: rolls intrinsic base-stat bonuses for a starship of a known class
    /// (<c>C=0, B=1, A=2, S=3</c>) from a fresh MWC stream seeded with <paramref name="inventorySeed"/>.
    /// </summary>
    public static (float Damage, float Shield, float Hyperdrive, float Maneuverability)? CalculateBaseStats(
        SystemStarshipType type,
        ulong inventorySeed,
        int classIndex,
        InventoryGenerationTables? tables)
    {
        int shipClass = (int)type;
        if (tables?.ShipBaseStatsByClass is not { } classes
            || shipClass < 0 || shipClass >= classes.Length
            || classes[shipClass] is not { } ratings
            || classIndex < 0 || classIndex >= ratings.Length
            || ratings[classIndex] is not { Length: > 0 } entries)
            return null;

        var rng = SolarLayoutGeneration.RandomFor(inventorySeed);
        float dmg = 0f, shd = 0f, hd = 0f, agi = 0f;
        foreach (var entry in entries)
        {
            float roll = (float)(rng.NextWord() * TinyDouble);
            float val = entry.Min + roll * (entry.Max - entry.Min);
            switch (entry.StatId)
            {
                case "SHIP_DAMAGE":
                    dmg = val;
                    break;
                case "SHIP_SHIELD":
                    shd = val;
                    break;
                case "SHIP_HYPERDRIVE":
                case "FREI_HYPERDRIVE":
                    hd = val;
                    break;
                case "SHIP_AGILE":
                case "FREI_FLEET":
                    agi = val;
                    break;
            }
        }
        return (dmg, shd, hd, agi);
    }

    /// <summary>
    /// Computes the complete crashed-ship inventory summary (slots, broken slots, unit value, and base-stat
    /// bonuses) for a wreck at <paramref name="siteKey"/>. Returns null if the catalog does not yet have
    /// the starship layout/cost tables or part trees.
    /// </summary>
    public static CrashedShipInventorySummary? ForCrashedShip(
        SystemStarshipType type,
        ulong modelSeed,
        ulong siteKey,
        int classIndex,
        Func<string, IReadOnlyList<PartGroup>?>? loadTree,
        InventoryGenerationTables? tables,
        int nativeWealth = 1)
    {
        if (ResolveModelSize(type, modelSeed, loadTree, tables) is not { } resolved)
            return null;
        return ForCrashedShip(type, resolved, siteKey, classIndex, tables, nativeWealth);
    }

    /// <summary>
    /// Computes the complete crashed-ship inventory summary using a pre-resolved model size
    /// (so a planet pass only resolves the 21 system pool ships once).
    /// </summary>
    public static CrashedShipInventorySummary? ForCrashedShip(
        SystemStarshipType type,
        (int SizeTypeIndex, string SizeType, bool Cool) resolvedModelSize,
        ulong siteKey,
        int classIndex,
        InventoryGenerationTables? tables,
        int nativeWealth = 1)
    {
        int effectiveClass = type == SystemStarshipType.Exotic ? 3 : classIndex;
        if (effectiveClass is < 0 or > 3
            || tables?.ShipLayouts is not { } layouts
            || layouts.FirstOrDefault(l => l.SizeTypeIndex == resolvedModelSize.SizeTypeIndex) is not { } layoutRules
            || CargoLayout(siteKey, layoutRules) is not { } cargo
            || TechLayout(siteKey, layoutRules) is not { } tech
            || CalculateUnitValue(type, effectiveClass, cargo.RequestedSlots, resolvedModelSize.Cool, tables) is not { } value
            || CalculateBaseStats(type, siteKey, effectiveClass, tables) is not { } stats)
            return null;

        // Sentinel Interceptors (shipClass == 9) pass brokenSlotChance = 0 at 0x141747908.
        // Non-Sentinel wrecks place 5 Always technologies (SHIPJUMP1, LAUNCHER, HYPERDRIVE, SHIPSHIELD, SHIPGUN1)
        // plus techBudget = 1 + Next(clamp(int((TechSlots / 9) * wealthFactor + 0.5f), 1, 10)) extra technologies
        // in the first (5 + techBudget) unlocked Tech slots (0x1404D83D0..0x1404D8526).
        bool isSentinel = type == SystemStarshipType.SentinelInterceptor;
        float brokenChance = isSentinel ? 0f : (tables.CrashedShipBrokenSlotChance ?? 0.75f);
        int minUnbroken = tables.CrashedShipMinNonBrokenSlots ?? 3;
        float wealthFactor = nativeWealth switch { 0 => 0.3f, 1 => 0.6f, 2 => 1.0f, _ => 0.6f };
        int maxTechRoll = Math.Clamp((int)((tech.RequestedSlots / 9) * wealthFactor + 0.5f), 1, 10);
        int techBudget = 1 + (int)SolarLayoutGeneration.RandomFor(siteKey).Next((uint)maxTechRoll);
        int installedTechSlots = 5 + techBudget;
        int brokenCargo = isSentinel ? 0 : BrokenSlotCount(cargo, occupiedOrSkippedSlots: 3, brokenChance, minUnbroken);
        int brokenTech = isSentinel ? 0 : BrokenSlotCount(tech, occupiedOrSkippedSlots: installedTechSlots, brokenChance, minUnbroken);

        return new CrashedShipInventorySummary(
            resolvedModelSize.SizeType,
            cargo.RequestedSlots,
            tech.RequestedSlots,
            brokenCargo,
            brokenTech,
            value,
            stats.Damage,
            stats.Shield,
            stats.Hyperdrive,
            stats.Maneuverability);
    }
}
