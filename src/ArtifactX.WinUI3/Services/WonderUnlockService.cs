using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Handles unlocking and locking of Wonders and Fishing records on the Catalog page.
/// Manages the 7 fixed-size GcWonderRecord arrays (Planet, Fauna, Flora, Mineral,
/// Treasures, Glitches, Personal), personal record names (YvA), and the 3 parallel
/// fishing arrays (ProductList, ProductCountList, LargestCatchList).
/// </summary>
public static class WonderUnlockService
{
    private static readonly string[] TreasurePrefixes =
    [
        "PROC_LOOT", // 0: Loot (Unearthed Treasure)
        "PROC_HIST", // 1: Document (Historical Document)
        "PROC_BIO",  // 2: BioSample (Biological Sample)
        "PROC_FOSS", // 3: Fossil (Fossil)
        "PROC_PLNT", // 4: Plant (Plant)
        "PROC_TOOL", // 5: Tool (Tool)
        "PROC_FARM", // 6: Farm (Farm)
        "PROC_SEA",  // 7: SeaLoot (Sea Loot)
        "PROC_FEAR", // 8: SeaHorror (Sea Horror)
        "PROC_SALV", // 9: Salvage (Salvage)
        "PROC_BONE", // 10: Bones (Bones)
        "PROC_DARK", // 11: SpaceHorror (Space Horror)
        "PROC_STAR", // 12: SpaceBones (Space Bones)
    ];

    private static readonly string[] GlitchPartIds =
    [
        "BASE_ENGINEORB",  // 0: EngineOrb (Terbium Growth)
        "BASE_BEAMSTONE",  // 1: BeamStone (Light Fissure)
        "BASE_BUBBLECLUS", // 2: BubbleCluster (Bubble Cluster)
        "BASE_MEDGEOMETR", // 3: MedGeometric (Hexplate Bush)
        "BASE_SHARD",      // 4: Shard (Glitching Separator)
        "BASE_STARJOINT",  // 5: StarJoint (Ossified Star)
        "BASE_BONEGARDEN", // 6: BoneGarden (Rattle Spine)
        "BASE_CONTOURPOD", // 7: ContourPod (Cable Pod)
        "BASE_HYDROPOD",   // 8: HydroPod (Calcishroom)
        "BASE_SHELLWHITE", // 9: ShellWhite (Capillary Shell)
        "BASE_WEIRDCUBE",  // 10: WeirdCube (Electric Cube)
    ];

    private static readonly float[] PlanetDefaultStats =
    [
        328.2f,   // 0: TemperatureMax (Hottest Temperature)
        -134.3f,  // 1: TemperatureMin (Coldest Temperature)
        219.9f,   // 2: ToxicityMax (Most Toxic Atmosphere)
        32.0f,    // 3: RadiationMax (Highest Radiation)
        0.85f,    // 4: AnomalyMax (Most Anomalous)
        550388f,  // 5: RadiusMax (Largest Planet)
        14086f,   // 6: RadiusMin (Smallest Planet)
        3519f,    // 7: AltitudeReachedMax (Highest Peak)
        1486f,    // 8: AltitudeReachedMin (Deepest Ocean)
        96.9f,    // 9: PerfectionMax (Most Hospitable)
        8.5f,     // 10: PerfectionMin (Most Inhospitable)
    ];

    private static readonly float[] CreatureDefaultStats =
    [
        8.07f,   // 0: Largest Herbivore
        0.37f,   // 1: Smallest Herbivore
        7.46f,   // 2: Largest Carnivore
        1.18f,   // 3: Smallest Carnivore
        3.37f,   // 4: Most Intelligent
        14.12f,  // 5: Most Aggressive
        3.95f,   // 6: Hottest Climate
        4.35f,   // 7: Coldest Climate
        0.98f,   // 8: Most Toxic
        3.87f,   // 9: Most Radioactive
        4.05f,   // 10: Most Anomalous
        183.5f,  // 11: Deepest Underwater
        3.22f,   // 12: Tallest Mechanical
        224.0f,  // 13: Highest Flying
        4.06f,   // 14: Deepest Subterranean
    ];

    private static readonly Dictionary<string, int> ExpectedRecordCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PlanetWonders"] = 11,
        ["CreatureWonders"] = 15,
        ["FloraWonders"] = 8,
        ["MineralWonders"] = 8,
        ["TreasureWonders"] = 13,
        ["WeirdBasePartWonders"] = 11,
        ["CustomWonders"] = 12,
    };

    /// <summary>Every save path that wonder and fishing record edits stage to.</summary>
    public static readonly string[][] EditedPaths =
    [
        NmsGameCatalogPaths.WonderRecordPaths["PlanetWonders"],
        NmsGameCatalogPaths.WonderRecordPaths["CreatureWonders"],
        NmsGameCatalogPaths.WonderRecordPaths["FloraWonders"],
        NmsGameCatalogPaths.WonderRecordPaths["MineralWonders"],
        NmsGameCatalogPaths.WonderRecordPaths["TreasureWonders"],
        NmsGameCatalogPaths.WonderRecordPaths["WeirdBasePartWonders"],
        NmsGameCatalogPaths.WonderRecordPaths["CustomWonders"],
        NmsGameCatalogPaths.PersonalRecordNamesPath,
        NmsFishingPaths.ProductListPath,
        NmsFishingPaths.ProductCountListPath,
        NmsFishingPaths.LargestCatchListPath,
    ];

    /// <summary>Reverts all staged wonder and fishing edits.</summary>
    public static void RevertEdits()
    {
        foreach (var path in EditedPaths)
            SaveSessionManager.RevertEdit(path);
    }

    /// <summary>
    /// Unlocks or locks the given wonder or fishing items and stages whole updated arrays.
    /// </summary>
    public static void Set(IEnumerable<GameCatalogItemViewModel> items, bool unlock)
    {
        var wonderItems = new List<(string CategoryType, int SlotIndex, GameCatalogItemViewModel Item)>();
        var fishItems = new List<GameCatalogItemViewModel>();

        foreach (var item in items)
        {
            if (item.GameId.StartsWith("WONDER:", StringComparison.Ordinal))
            {
                var parts = item.GameId.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int slotIndex))
                    wonderItems.Add((parts[1], slotIndex, item));
            }
            else
            {
                fishItems.Add(item);
            }
        }

        // 1. Process Wonder records by category
        foreach (var group in wonderItems.GroupBy(w => w.CategoryType, StringComparer.OrdinalIgnoreCase))
        {
            string categoryType = group.Key;
            if (!NmsGameCatalogPaths.WonderRecordPaths.TryGetValue(categoryType, out var recordPath)) continue;

            int expectedCount = ExpectedRecordCounts.GetValueOrDefault(categoryType, 12);
            var existingArray = SaveSessionManager.GetValue(recordPath) as JArray;
            var updated = existingArray is not null && existingArray.Count >= expectedCount
                ? new JArray(existingArray.Select(v => v.DeepClone()))
                : CreateDefaultWonderArray(expectedCount);

            JArray? updatedYva = null;
            if (categoryType.Equals("CustomWonders", StringComparison.OrdinalIgnoreCase))
            {
                var existingYva = SaveSessionManager.GetValue(NmsGameCatalogPaths.PersonalRecordNamesPath) as JArray;
                updatedYva = existingYva is not null && existingYva.Count >= expectedCount
                    ? new JArray(existingYva.Select(v => v.DeepClone()))
                    : CreateDefaultYvaArray(expectedCount);
            }

            ulong systemAddress = CurrentSystemAddress();

            foreach (var (_, slotIndex, item) in group)
            {
                if (slotIndex < 0 || slotIndex >= updated.Count) continue;

                item.IsUnlocked = unlock;
                if (unlock)
                {
                    updated[slotIndex] = CreateUnlockedRecord(categoryType, slotIndex, systemAddress, item);
                    if (updatedYva is not null && slotIndex < updatedYva.Count)
                    {
                        updatedYva[slotIndex] = new JObject
                        {
                            ["fH8"] = $"Personal Record {slotIndex + 1}",
                            ["7bp"] = new JObject { ["93I"] = "Planet" },
                        };
                    }
                }
                else
                {
                    updated[slotIndex] = CreateEmptyWonderRecord();
                    item.Detail = null;
                    item.Species = null;
                    if (updatedYva is not null && slotIndex < updatedYva.Count)
                    {
                        updatedYva[slotIndex] = new JObject
                        {
                            ["fH8"] = "",
                            ["7bp"] = new JObject { ["93I"] = "Custom" },
                        };
                    }
                }
            }

            SaveSessionManager.StageEdit(updated, recordPath);
            if (updatedYva is not null)
                SaveSessionManager.StageEdit(updatedYva, NmsGameCatalogPaths.PersonalRecordNamesPath);
        }

        // 2. Process Fishing records
        if (fishItems.Count > 0)
        {
            SetFishCatches(fishItems, unlock);
        }
    }

    private static void SetFishCatches(List<GameCatalogItemViewModel> fishItems, bool unlock)
    {
        var productIdsArray = SaveSessionManager.GetValue(NmsFishingPaths.ProductListPath) as JArray;
        var productCountsArray = SaveSessionManager.GetValue(NmsFishingPaths.ProductCountListPath) as JArray;
        var largestCatchesArray = SaveSessionManager.GetValue(NmsFishingPaths.LargestCatchListPath) as JArray;

        var productIds = productIdsArray?.Select(t => t.Value<string>() ?? "^").ToList()
            ?? Enumerable.Repeat("^", NmsFishingPaths.SlotCount).ToList();
        var productCounts = productCountsArray?.Select(t => t.Value<uint>()).ToList()
            ?? Enumerable.Repeat(0u, NmsFishingPaths.SlotCount).ToList();
        var largestCatches = largestCatchesArray?.Select(t => t.Value<float>()).ToList()
            ?? Enumerable.Repeat(0f, NmsFishingPaths.SlotCount).ToList();

        while (productIds.Count < NmsFishingPaths.SlotCount) productIds.Add("^");
        while (productCounts.Count < NmsFishingPaths.SlotCount) productCounts.Add(0u);
        while (largestCatches.Count < NmsFishingPaths.SlotCount) largestCatches.Add(0f);

        var caughtIdSet = new HashSet<string>(
            productIds.Where(id => id.Length > 0 && id != "^").Select(CatalogService.NormalizeId),
            StringComparer.Ordinal);

        bool changed = false;

        foreach (var item in fishItems)
        {
            item.IsUnlocked = unlock;
            string cleanId = CatalogService.NormalizeId(item.GameId);

            if (unlock)
            {
                if (!caughtIdSet.Add(cleanId)) continue;
                int emptyIndex = productIds.FindIndex(id => id.Length == 0 || id == "^");
                if (emptyIndex < 0) continue;

                productIds[emptyIndex] = "^" + cleanId;
                productCounts[emptyIndex] = 1u;
                largestCatches[emptyIndex] = 0f;
                changed = true;
            }
            else
            {
                if (!caughtIdSet.Remove(cleanId)) continue;
                int idx = productIds.FindIndex(id => CatalogService.NormalizeId(id) == cleanId);
                if (idx < 0) continue;

                productIds.RemoveAt(idx);
                productCounts.RemoveAt(idx);
                largestCatches.RemoveAt(idx);

                productIds.Add("^");
                productCounts.Add(0u);
                largestCatches.Add(0f);
                changed = true;
            }
        }

        if (changed)
        {
            SaveSessionManager.StageEdit(new JArray(productIds), NmsFishingPaths.ProductListPath);
            SaveSessionManager.StageEdit(new JArray(productCounts), NmsFishingPaths.ProductCountListPath);
            SaveSessionManager.StageEdit(new JArray(largestCatches), NmsFishingPaths.LargestCatchListPath);
        }
    }

    private static JArray CreateDefaultWonderArray(int count)
    {
        var array = new JArray();
        for (int i = 0; i < count; i++)
            array.Add(CreateEmptyWonderRecord());
        return array;
    }

    private static JArray CreateDefaultYvaArray(int count)
    {
        var array = new JArray();
        for (int i = 0; i < count; i++)
        {
            array.Add(new JObject
            {
                ["fH8"] = "",
                ["7bp"] = new JObject { ["93I"] = "Custom" },
            });
        }
        return array;
    }

    private static JObject CreateEmptyWonderRecord() => new()
    {
        ["UqY"] = new JArray(0, 0),
        ["1OQ"] = 0.0,
        ["y87"] = false,
    };

    private static JObject CreateUnlockedRecord(string categoryType, int slotIndex, ulong systemAddress, GameCatalogItemViewModel item)
    {
        if (categoryType.Equals("TreasureWonders", StringComparison.OrdinalIgnoreCase))
        {
            string prefix = slotIndex >= 0 && slotIndex < TreasurePrefixes.Length ? TreasurePrefixes[slotIndex] : "PROC_LOOT";
            string treasureId = $"{prefix}#{(slotIndex * 7919 + 12345) % 90000 + 10000:D5}";
            var (first, second) = PackAscii(treasureId);
            item.Detail = CatalogService.ProceduralProductName(treasureId) ?? item.Name;
            return new JObject
            {
                ["UqY"] = new JArray($"0x{first:X}", $"0x{second:X}"),
                ["1OQ"] = 1500000.0,
                ["y87"] = true,
            };
        }

        if (categoryType.Equals("WeirdBasePartWonders", StringComparison.OrdinalIgnoreCase))
        {
            string partId = slotIndex >= 0 && slotIndex < GlitchPartIds.Length ? GlitchPartIds[slotIndex] : "BASE_ENGINEORB";
            var (first, second) = PackAscii(partId);
            item.Detail = CatalogService.TryGet(partId)?.DisplayName ?? item.Name;
            return new JObject
            {
                ["UqY"] = new JArray($"0x{first:X}", $"0x{second:X}"),
                ["1OQ"] = 5.0,
                ["y87"] = true,
            };
        }

        int planet = (slotIndex % 6) + 1;
        ulong planetAddress = (systemAddress & ~(0xFUL << 52)) | ((ulong)planet << 52);
        ulong seed = 0x81C896F4D18A3585UL ^ ((ulong)slotIndex * 0x9E3779B97F4A7C15UL);

        float stat = categoryType switch
        {
            "PlanetWonders" => slotIndex >= 0 && slotIndex < PlanetDefaultStats.Length ? PlanetDefaultStats[slotIndex] : 100.0f,
            "CreatureWonders" => slotIndex >= 0 && slotIndex < CreatureDefaultStats.Length ? CreatureDefaultStats[slotIndex] : 5.0f,
            "FloraWonders" => 4.5f,
            "MineralWonders" => slotIndex == 3 ? 80.0f : 4.6f,
            _ => 0.0f,
        };

        item.Detail = $"{UniverseInfo.PlanetName(systemAddress, planet)} · {UniverseInfo.SystemName(systemAddress)}";

        if (categoryType.Equals("CreatureWonders", StringComparison.OrdinalIgnoreCase))
            item.Species ??= "Fauna Record";
        else if (categoryType.Equals("FloraWonders", StringComparison.OrdinalIgnoreCase))
            item.Species ??= "Flora Record";
        else if (categoryType.Equals("MineralWonders", StringComparison.OrdinalIgnoreCase))
            item.Species ??= "Mineral Record";

        return new JObject
        {
            ["UqY"] = new JArray($"0x{planetAddress:X}", $"0x{seed:X}"),
            ["1OQ"] = (double)stat,
            ["y87"] = true,
        };
    }

    private static (ulong first, ulong second) PackAscii(string id)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(id);
        byte[] buf = new byte[16];
        Array.Copy(ascii, buf, Math.Min(ascii.Length, 16));
        ulong first = BitConverter.ToUInt64(buf, 0);
        ulong second = BitConverter.ToUInt64(buf, 8);
        return (first, second);
    }

    private static ulong CurrentSystemAddress()
    {
        if (NmsSpaceStationPaths.ReadSystemAddress(SaveSessionManager.GetValue(NmsSpaceStationPaths.CurrentUniverseAddressPath)) is ulong current && current != 0)
            return current;
        return 0x1D09E8F80732UL; // Oskrem-Tat fallback
    }
}

