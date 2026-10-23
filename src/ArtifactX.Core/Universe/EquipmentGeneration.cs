namespace ArtifactX.Core.Universe;

/// <summary>
/// A single base stat roll range from GcInventoryTable (Metadata/Reality/Tables/InventoryTable.mbin),
/// in the exact array order the game's PRNG steps through in GenerateBaseStats (0x1404ceab0).
/// </summary>
public sealed record BaseStatRange(string StatId, float Min, float Max);

/// <summary>
/// Inventory generation tables extracted from the player's own game files
/// (metadata/reality/tables/inventorytable.mbin and metadata/simulation/environment/planetbuildingtable.mbin).
/// Stored in catalog category "PlanetGeneration" under GameId "INVENTORYGENERATION".
/// </summary>
public sealed record InventoryGenerationTables(
    BaseStatRange[][] ShipSClassBaseStats,
    BaseStatRange[][] WeaponSClassBaseStats,
    float[][] ClassProbabilities,
    int[] ShipSClassMaxCargo,
    int[] ShipSClassMaxTech,
    int WeaponSClassMaxTech,
    Dictionary<string, float[]>? BuildingDensities = null);

/// <summary>
/// Deterministically rolled S-Class base stats (% bonus) and max upgrade slot capacities for a starship seed.
/// </summary>
public sealed record ShipSClassStats(
    float Damage,
    float Shield,
    float Hyperdrive,
    float Maneuverability,
    int MaxCargoSlots,
    int MaxTechSlots)
{
    public string StatsSummary =>
        $"Damage +{Damage:F1}% · Shield +{Shield:F1}% · Hyperdrive +{Hyperdrive:F1}% · Maneuver +{Maneuverability:F1}%";

    public string CompactStatsSummary =>
        $"DMG +{Damage:F1}% · SHD +{Shield:F1}% · HD +{Hyperdrive:F1}% · MNV +{Maneuverability:F1}%";

    public string SlotsSummary =>
        MaxCargoSlots > 0 && MaxTechSlots > 0 ? $"Max S-Class Slots: {MaxCargoSlots} Cargo · {MaxTechSlots} Tech" : "";
}

/// <summary>
/// Deterministically rolled S-Class base stats (% bonus) and max upgrade slot capacity for a multi-tool seed.
/// </summary>
public sealed record MultiToolSClassStats(
    float Damage,
    float Mining,
    float Scanner,
    int MaxTechSlots)
{
    public string StatsSummary =>
        $"Damage +{Damage:F1}% · Mining +{Mining:F1}% · Scanner +{Scanner:F1}%";

    public string CompactStatsSummary =>
        $"DMG +{Damage:F1}% · MIN +{Mining:F1}% · SCN +{Scanner:F1}%";

    public string SlotsSummary =>
        MaxTechSlots > 0 ? $"Max S-Class Slots: {MaxTechSlots} Tech" : "";
}

/// <summary>
/// Deterministic S-Class base stat calculator, class spawn probability resolver, and planetary
/// building availability rules.
///
/// Reverse-engineered from NMS.exe (build 180836, 2026-10-23):
/// - GenerateBaseStats at 0x1404ceab0: when UseSeedValue (GpI[0]) is true, reseeds MWC PRNG from
///   the 64-bit model seed:
///     s0 = (uint)seed == 0 ? 1u : (uint)seed
///     s1 = (((uint)seed >> 16) | ((uint)seed &lt;&lt; 16)) ^ (uint)(seed >> 32) ^ (uint)seed
///   and steps once per GcInventoryGenerationBaseStatDataEntry in BaseStatsPerClass[classIndex]:
///     t = (ulong)s0 * 0x5A76F899UL + s1; s0 = (uint)t; s1 = (uint)(t >> 32);
///     stat = min + ((float)((double)s0 * 2.3283064370807974e-10)) * (max - min)
/// - Verified 100.0000% bit-exact against untouched starships and multi-tools in real saves
///   (Slot 1, Slot 2, Slot 8, 2026-10-23).
/// - Note: UpgradeBaseStats (0x1404ced30) runs when upgrading class or loading an edited item whose
///   existing base stats exceed the newly rolled range, taking maxss(existingStat, newlyRolledStat).
/// </summary>
public static class EquipmentGeneration
{
    private const ulong Multiplier = 0x5A76F899UL;
    private const double TinyDouble = 2.3283064370807974e-10;

    /// <summary>
    /// Fallback S-Class stat ranges from GcInventoryTable (used only before the catalog row is loaded).
    /// Order matches GcInventoryTable.ShipBaseStatsData[shipClass].BaseStatsPerClass[3].BaseStats.
    /// </summary>
    private static readonly BaseStatRange[][] DefaultShipSClassStats =
    [
        // 0: Freighter
        [new("FREI_HYPERDRIVE", 60f, 80f), new("FREI_FLEET", 40f, 60f)],
        // 1: Dropship (Hauler)
        [new("SHIP_DAMAGE", 15f, 30f), new("SHIP_SHIELD", 95f, 125f), new("SHIP_HYPERDRIVE", 45f, 58f), new("SHIP_AGILE", 10f, 15f)],
        // 2: Fighter
        [new("SHIP_DAMAGE", 70f, 90f), new("SHIP_SHIELD", 24f, 38f), new("SHIP_HYPERDRIVE", 0f, 0f), new("SHIP_AGILE", 35f, 45f)],
        // 3: Scientific (Explorer)
        [new("SHIP_DAMAGE", 0f, 0f), new("SHIP_SHIELD", 30f, 40f), new("SHIP_HYPERDRIVE", 90f, 120f), new("SHIP_AGILE", 25f, 32f)],
        // 4: Shuttle
        [new("SHIP_DAMAGE", 25f, 30f), new("SHIP_SHIELD", 25f, 30f), new("SHIP_HYPERDRIVE", 25f, 30f), new("SHIP_AGILE", 15f, 35f)],
        // 5: PlayerFreighter
        [],
        // 6: Royal (Exotic)
        [new("SHIP_DAMAGE", 50f, 75f), new("SHIP_SHIELD", 70f, 90f), new("SHIP_HYPERDRIVE", 75f, 95f), new("SHIP_AGILE", 40f, 50f)],
        // 7: Alien (Living)
        [new("SHIP_DAMAGE", 50f, 75f), new("SHIP_SHIELD", 15f, 38f), new("SHIP_HYPERDRIVE", 75f, 95f), new("ALIEN_SHIP", 1f, 1f), new("SHIP_AGILE", 10f, 15f)],
        // 8: Sail (Solar)
        [new("SHIP_DAMAGE", 60f, 75f), new("SHIP_SHIELD", 23f, 50f), new("SHIP_HYPERDRIVE", 38f, 50f), new("SHIP_AGILE", 35f, 40f)],
        // 9: Robot (Sentinel Interceptor)
        [new("SHIP_DAMAGE", 60f, 80f), new("SHIP_SHIELD", 20f, 35f), new("SHIP_HYPERDRIVE", 5f, 10f), new("SHIP_AGILE", 40f, 50f), new("ROBOT_SHIP", 1f, 1f)],
        // 10: Corvette
        [new("SHIP_DAMAGE", 15f, 30f), new("SHIP_SHIELD", 95f, 125f), new("SHIP_HYPERDRIVE", 45f, 58f), new("SHIP_AGILE", 10f, 15f)],
    ];

    private static readonly int[] DefaultShipMaxCargo = [120, 120, 105, 110, 110, 0, 105, 120, 105, 120, 120];
    private static readonly int[] DefaultShipMaxTech = [60, 60, 60, 60, 60, 0, 60, 60, 60, 60, 60];

    /// <summary>
    /// Fallback S-Class stat ranges from GcInventoryTable.WeaponBaseStatsData[weaponClass].BaseStatsPerClass[3].BaseStats.
    /// </summary>
    private static readonly BaseStatRange[][] DefaultWeaponSClassStats =
    [
        // 0: Pistol
        [new("WEAPON_DAMAGE", 0f, 0f), new("WEAPON_MINING", 20f, 35f), new("WEAPON_SCAN", 45f, 50f)],
        // 1: Rifle
        [new("WEAPON_DAMAGE", 15f, 20f), new("WEAPON_MINING", 0f, 0f), new("WEAPON_SCAN", 15f, 20f)],
        // 2: Pristine (Experimental)
        [new("WEAPON_DAMAGE", 15f, 25f), new("WEAPON_MINING", 25f, 30f), new("WEAPON_SCAN", 100f, 100f)],
        // 3: Alien
        [new("WEAPON_DAMAGE", 25f, 35f), new("WEAPON_MINING", 15f, 20f), new("WEAPON_SCAN", 50f, 60f)],
        // 4: Royal (Exotic)
        [new("WEAPON_DAMAGE", 15f, 25f), new("WEAPON_MINING", 25f, 30f), new("WEAPON_SCAN", 100f, 100f)],
        // 5: Robot (Sentinel)
        [new("WEAPON_DAMAGE", 32f, 50f), new("WEAPON_MINING", 10f, 15f), new("WEAPON_SCAN", 45f, 55f)],
        // 6: Atlas (Atlantid)
        [new("WEAPON_DAMAGE", 10f, 20f), new("WEAPON_MINING", 35f, 50f), new("WEAPON_SCAN", 40f, 50f)],
        // 7: AtlasYellow
        [new("WEAPON_DAMAGE", 10f, 20f), new("WEAPON_MINING", 35f, 50f), new("WEAPON_SCAN", 40f, 50f)],
        // 8: AtlasBlue
        [new("WEAPON_DAMAGE", 10f, 20f), new("WEAPON_MINING", 35f, 50f), new("WEAPON_SCAN", 40f, 50f)],
        // 9: Staff
        [new("WEAPON_DAMAGE", 25f, 50f), new("WEAPON_MINING", 10f, 15f), new("WEAPON_SCAN", 40f, 50f)],
    ];

    /// <summary>
    /// Calculates the exact S-Class base stats and max slot capacities for a starship seed.
    /// </summary>
    public static ShipSClassStats CalculateShipSClassStats(
        SystemStarshipType shipType,
        ulong seed,
        InventoryGenerationTables? tables = null)
    {
        int classIdx = (int)shipType;
        var source = tables?.ShipSClassBaseStats ?? DefaultShipSClassStats;
        var entries = (classIdx >= 0 && classIdx < source.Length) ? source[classIdx] : [];

        uint lo = (uint)seed;
        uint hi = (uint)(seed >> 32);
        uint s0 = lo == 0 ? 1u : lo;
        uint s1 = ((lo >> 16) | (lo << 16)) ^ hi ^ lo;

        float dmg = 0f, shd = 0f, hd = 0f, agi = 0f;
        foreach (var entry in entries)
        {
            ulong t = (ulong)s0 * Multiplier + s1;
            s0 = (uint)t;
            s1 = (uint)(t >> 32);
            float roll = (float)((double)s0 * TinyDouble);
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

        var maxCargoArr = tables?.ShipSClassMaxCargo ?? DefaultShipMaxCargo;
        var maxTechArr = tables?.ShipSClassMaxTech ?? DefaultShipMaxTech;
        int maxCargo = (classIdx >= 0 && classIdx < maxCargoArr.Length) ? maxCargoArr[classIdx] : 105;
        int maxTech = (classIdx >= 0 && classIdx < maxTechArr.Length) ? maxTechArr[classIdx] : 60;

        return new ShipSClassStats(dmg, shd, hd, agi, maxCargo, maxTech);
    }

    /// <summary>
    /// Maps a Multi-Tool kind label ("Pistol", "Rifle", "Experimental", "Alien", "Exotic", "Sentinel", "Atlantid", "Staff")
    /// to GcWeaponClasses.WeaponStatClassEnum index.
    /// </summary>
    public static int WeaponStatClassIndex(string kind) => kind switch
    {
        "Pistol" => 0,
        "Rifle" => 1,
        "Experimental" or "Pristine" => 2,
        "Alien" => 3,
        "Exotic" or "Royal" => 4,
        "Sentinel" or "Robot" => 5,
        "Atlantid" or "Atlas" => 6,
        "Staff" => 9,
        _ => 1, // Default "Standard" before kind is resolved uses Rifle
    };

    /// <summary>
    /// Calculates the exact S-Class base stats and max tech slot capacity for a multi-tool seed and kind.
    /// </summary>
    public static MultiToolSClassStats CalculateMultiToolSClassStats(
        string kind,
        ulong seed,
        InventoryGenerationTables? tables = null)
    {
        int classIdx = WeaponStatClassIndex(kind);
        var source = tables?.WeaponSClassBaseStats ?? DefaultWeaponSClassStats;
        var entries = (classIdx >= 0 && classIdx < source.Length) ? source[classIdx] : [];

        uint lo = (uint)seed;
        uint hi = (uint)(seed >> 32);
        uint s0 = lo == 0 ? 1u : lo;
        uint s1 = ((lo >> 16) | (lo << 16)) ^ hi ^ lo;

        float dmg = 0f, min = 0f, scn = 0f;
        foreach (var entry in entries)
        {
            ulong t = (ulong)s0 * Multiplier + s1;
            s0 = (uint)t;
            s1 = (uint)(t >> 32);
            float roll = (float)((double)s0 * TinyDouble);
            float val = entry.Min + roll * (entry.Max - entry.Min);

            switch (entry.StatId)
            {
                case "WEAPON_DAMAGE":
                    dmg = val;
                    break;
                case "WEAPON_MINING":
                    min = val;
                    break;
                case "WEAPON_SCAN":
                    scn = val;
                    break;
            }
        }

        int maxTech = tables?.WeaponSClassMaxTech ?? 60;
        return new MultiToolSClassStats(dmg, min, scn, maxTech);
    }

    /// <summary>
    /// Returns the system's class probability array [C, B, A, S] in percent (0..100).
    /// <paramref name="wealth"/> is 1 (Low), 2 (Medium), or 3 (High) from SystemGenerator/SystemFacts.
    /// </summary>
    public static (float C, float B, float A, float S) GetClassProbabilities(
        int wealth,
        bool isPirate,
        InventoryGenerationTables? tables = null)
    {
        int idx = isPirate ? 3 : Math.Clamp(wealth - 1, 0, 2);
        if (tables?.ClassProbabilities is { } probs && idx >= 0 && idx < probs.Length && probs[idx].Length >= 4)
        {
            var p = probs[idx];
            // In GcInventoryTable, Pirate (index 3) stores [85, 5, 5, 5] or [5, 5, 5, 5] where C is remainder (85%)
            float s = p[3], a = p[2], b = p[1];
            float c = isPirate && p[0] < 50f ? (100f - s - a - b) : p[0];
            return (c, b, a, s);
        }

        return idx switch
        {
            0 => (60f, 30f, 10f, 0f),
            1 => (49f, 35f, 15f, 1f),
            2 => (30f, 40f, 28f, 2f),
            _ => (85f, 5f, 5f, 5f),
        };
    }

    /// <summary>
    /// Human-readable banner explaining S-Class spawn probabilities and ship traffic rules for the system.
    /// </summary>
    public static string FormatSystemShipRules(
        int wealth,
        string wealthLabel,
        bool isPirate,
        bool isUncharted,
        bool isAbandoned,
        bool hasCorruptedPlanet,
        InventoryGenerationTables? tables = null)
    {
        if (isUncharted)
        {
            return "Uncharted system (no Space Station or Trading Posts): ships do NOT land in traffic here. " +
                   "These native starship models can only be found at planetary Crash Sites (Distress Signals)" +
                   (hasCorruptedPlanet ? " or Dissonance Resonator crash sites for the Sentinel Interceptor." : ".");
        }

        if (isAbandoned)
        {
            return "Abandoned system (no NPC ship traffic at the Space Station or Trading Posts): " +
                   "these native starships can only be found at planetary Crash Sites (Distress Signals)" +
                   (hasCorruptedPlanet ? " or Dissonance Resonator crash sites for the Sentinel Interceptor." : ".");
        }

        var (c, b, a, s) = GetClassProbabilities(wealth, isPirate, tables);
        string tier = isPirate
            ? "Outlaw / Pirate Economy"
            : $"{wealthLabel} Economy";

        string standardS = s <= 0f
            ? "0% S-Class for standard ships (upgradeable with Nanites; Exotic is still 100% S-Class)"
            : $"{s:0}% S-Class · {a:0}% A-Class · {b:0}% B-Class · {c:0}% C-Class";

        return $"{tier} — Spawn chances: {standardS}. The system's Exotic (#21) is always S-Class whenever it spawns.";
    }

    /// <summary>
    /// Per-ship S-Class spawn chance and where to find it in the system.
    /// </summary>
    public static string FormatShipSpawnChance(
        SystemStarshipType type,
        int wealth,
        bool isPirate,
        bool isUncharted,
        bool isAbandoned,
        InventoryGenerationTables? tables = null)
    {
        if (type == SystemStarshipType.Exotic)
        {
            if (isUncharted || isAbandoned)
                return "Always S-Class (100% S) · Crashed ship sites only (no NPC traffic)";
            return "Always S-Class (100% S, ~1–2% wave spawn) · Space Station, Trading Posts & Crash Sites";
        }

        var (_, _, a, s) = GetClassProbabilities(wealth, isPirate, tables);

        if (type == SystemStarshipType.SentinelInterceptor)
        {
            return s <= 0f
                ? $"0% natural S-Class ({a:0}% A-Class) · Crashed on Dissonant planet (Echo Locator / Carrier AI Fragment)"
                : $"{s:0}% S-Class ({a:0}% A-Class) · Crashed on Dissonant planet (Echo Locator / Carrier AI Fragment)";
        }

        if (isUncharted || isAbandoned)
        {
            return s <= 0f
                ? $"0% natural S-Class ({a:0}% A-Class) · Crashed ship sites only"
                : $"{s:0}% S-Class ({a:0}% A-Class) · Crashed ship sites only";
        }

        return s <= 0f
            ? $"0% natural S-Class ({a:0}% A-Class) · Space Station, Trading Posts & Crash Sites"
            : $"{s:0}% S-Class ({a:0}% A-Class) · Space Station, Trading Posts & Crash Sites";
    }

    /// <summary>
    /// Explains how and where to find a specific Multi-Tool in its pool.
    /// </summary>
    public static string FormatMultiToolLocationGuide(
        string kind,
        bool isRoyal,
        bool isStationPool,
        string sourceLabel,
        bool isUncharted,
        bool isAbandoned,
        bool isPirate)
    {
        if (isRoyal || string.Equals(kind, "Exotic", StringComparison.OrdinalIgnoreCase))
        {
            if (isUncharted)
                return "Unobtainable in Uncharted systems (no Sentinel Pillars spawn)";
            string reloadTarget = isStationPool ? "the Space Station" : sourceLabel;
            return $"Found at Sentinel Pillars (use a Sentinel Boundary Map). Save & reload on {reloadTarget} first to lock this pool.";
        }

        if (isUncharted)
            return "Unobtainable in Uncharted systems (no Space Station or Minor Settlements)";

        if (isAbandoned)
            return "No weapon cabinets in Abandoned systems (Space Station & Minor Settlements have no working cabinets)";

        string reload = isStationPool ? "the Space Station" : sourceLabel;
        return $"Found in Minor Settlement & Space Station weapon cabinets. Save & reload on {reload} to activate this pool.";
    }

    /// <summary>
    /// Maps a planet's GcBiomeType (and whether its subtype is Infested) to GcBuildingDensityLevels index:
    /// 0=Dead, 1=Low, 2=Mid, 3=Full, 4=Weird, 5=HalfWeird (Infested), 6=Waterworld, 7=GasGiant.
    /// </summary>
    public static int GetBuildingDensityColumn(int biome, bool isInfested = false)
    {
        if (biome == 6) return 0;  // Dead
        if (biome == 7 || biome == 8 || biome == 9 || biome == 10) return 4; // Weird / Anomaly (Red/Green/Blue)
        if (biome == 14) return 6; // Waterworld
        if (biome == 15) return 7; // GasGiant
        if (isInfested) return 5;  // HalfWeird / Infested
        return 2;                  // Mid / Standard inhabited biome
    }

    /// <summary>
    /// Checks whether a given GcBuildingClassification can procedurally spawn on a planet with the given attributes,
    /// using the density table from planetbuildingtable.mbin.
    /// </summary>
    public static bool CanBuildingSpawnOnPlanet(
        string buildingClass,
        int biome,
        bool isUncharted,
        bool isAbandoned,
        bool hasCorruptedSentinels,
        InventoryGenerationTables? tables = null)
    {
        int col = GetBuildingDensityColumn(biome);

        if (buildingClass == "AbandonedRobotCamp" || buildingClass == "SentinelDistressSignal")
            return hasCorruptedSentinels && !isUncharted;

        if (buildingClass == "Shop" || buildingClass == "Outpost")
        {
            if (isUncharted || isAbandoned) return false;
            if (tables?.BuildingDensities is { } d && d.TryGetValue(buildingClass, out var row) && col < row.Length)
                return row[col] > 0f;
            // Fallback from planetbuildingtable.mbin: Shop and Outpost have 0 density on Dead (0), Weird (4), GasGiant (7), and Outpost is 0 on Waterworld (6)
            if (col is 0 or 4 or 7) return false;
            if (buildingClass == "Outpost" && col == 6) return false;
            return true;
        }

        if (buildingClass == "DroneHive")
        {
            if (isUncharted) return false;
            if (tables?.BuildingDensities is { } d && d.TryGetValue(buildingClass, out var row) && col < row.Length)
                return row[col] > 0f;
            // Fallback from planetbuildingtable.mbin: DroneHive has 0 density on Dead (0), Weird (4), GasGiant (7)
            return col is not (0 or 4 or 7);
        }

        if (buildingClass == "DistressSignal")
        {
            if (tables?.BuildingDensities is { } d && d.TryGetValue(buildingClass, out var row) && col < row.Length)
                return row[col] > 0f;
            return true;
        }

        return true;
    }
}
