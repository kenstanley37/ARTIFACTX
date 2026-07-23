using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x1055E512DB0FAAA2, NameHash = 0xEDA97B90)]
    public class GcRealityManagerData : NMSTemplate
    {
        [NMS(Index = 67, Size = 0x9, EnumType = typeof(GcRealitySubstanceCategory.SubstanceCategoryEnum))]
        /* 0x0000 */ public Colour[] SubstanceCategoryColours;
        [NMS(Index = 65, Size = 0x7, EnumType = typeof(GcPlayerHazardType.HazardEnum))]
        /* 0x0090 */ public Colour[] HazardColours;
        [NMS(Index = 66, Size = 0x3, EnumType = typeof(GcRarity.RarityEnum))]
        /* 0x0100 */ public Colour[] RarityColours;
        [NMS(Index = 64)]
        /* 0x0130 */ public GcRealityIconTable Icons;
        [NMS(Index = 76)]
        /* 0x1DC8 */ public GcTradeSettings TradeSettings;
        [NMS(Index = 69, Size = 0xD0, EnumType = typeof(GcStatsTypes.StatsTypeEnum))]
        /* 0x3640 */ public TkTextureResource[] StatCategoryIcons;
        [NMS(Index = 70, Size = 0xD0, EnumType = typeof(GcStatsTypes.StatsTypeEnum))]
        /* 0x49C0 */ public TkTextureResource[] StatTechPackageIcons;
        [NMS(Index = 59, Size = 0x24, EnumType = typeof(GcMissionType.MissionTypeEnum))]
        /* 0x5D40 */ public GcNumberedTextList[] MissionNameAdjectives;
        [NMS(Index = 58, Size = 0x24, EnumType = typeof(GcMissionType.MissionTypeEnum))]
        /* 0x60A0 */ public GcNumberedTextList[] MissionNameFormats;
        [NMS(Index = 60, Size = 0x24, EnumType = typeof(GcMissionType.MissionTypeEnum))]
        /* 0x6400 */ public GcNumberedTextList[] MissionNameNouns;
        [NMS(Index = 5)]
        /* 0x6760 */ public GcSubstanceSecondaryBiome SubstanceSecondaryBiome;
        [NMS(Index = 52, Size = 0x7, EnumType = typeof(GcShipWeapons.ShipWeaponEnum))]
        /* 0x6980 */ public GcShipWeaponData[] ShipWeapons;
        [NMS(Index = 53, Size = 0x15, EnumType = typeof(GcPlayerWeapons.WeaponModeEnum))]
        /* 0x6B40 */ public GcPlayerWeaponData[] PlayerWeapons;
        [NMS(Index = 56, Size = 0xA, EnumType = typeof(GcMissionFaction.MissionFactionEnum))]
        /* 0x6C90 */ public NMSString0x20A[] FactionNames;
        [NMS(Index = 75, Size = 0xA, EnumType = typeof(GcMissionFaction.MissionFactionEnum))]
        /* 0x6DD0 */ public GcRepShopData[] RepShops;
        [NMS(Index = 74, Size = 0x11, EnumType = typeof(GcBiomeType.BiomeEnum))]
        /* 0x6F10 */ public GcTechList[] PlanetTechShops;
        [NMS(Index = 57, Size = 0xA, EnumType = typeof(GcMissionFaction.MissionFactionEnum))]
        /* 0x7020 */ public GcNumberedTextList[] FactionClients;
        [NMS(Index = 68, Size = 0x9, EnumType = typeof(GcRealitySubstanceCategory.SubstanceCategoryEnum))]
        /* 0x7110 */ public TkTextureResource[] SubstanceChargeIcons;
        [NMS(Index = 61, Size = 0xB)]
        /* 0x71E8 */ public TkIdArray[] MissionBoardRewardOptions;
        [NMS(Index = 55, Size = 0xA, EnumType = typeof(GcMissionFaction.MissionFactionEnum))]
        /* 0x7298 */ public NMSString0x10[] FactionStandingIDs;
        [NMS(Index = 54, Size = 0x7, EnumType = typeof(GcVehicleType.VehicleTypeEnum))]
        /* 0x7338 */ public TkIdArray[] DefaultVehicleLoadout;
        [NMS(Index = 92, Size = 0x5, EnumType = typeof(GcCatalogueGroups.CatalogueGroupEnum))]
        /* 0x73A8 */ public GcFilename[] Catalogues;
        // size: 0x5
        public enum StatsEnum {
            Suit,
            Weapon,
            Ship,
            Freighter,
            Vehicle,
        }
        [NMS(Index = 72, Size = 0x5, EnumType = typeof(StatsEnum))]
        /* 0x73F8 */ public GcStats[] Stats;
        [NMS(Index = 8, Size = 0x3, EnumType = typeof(GcProductTableType.ProductTableTypeEnum))]
        /* 0x7448 */ public GcFilename[] ProductTables;
        [NMS(Index = 83)]
        /* 0x7478 */ public GcInventoryLayout ShipCargoOnlyStartingLayout;
        [NMS(Index = 81)]
        /* 0x7490 */ public GcInventoryLayout ShipStartingLayout;
        [NMS(Index = 82)]
        /* 0x74A8 */ public GcInventoryLayout ShipTechOnlyStartingLayout;
        [NMS(Index = 80)]
        /* 0x74C0 */ public GcInventoryLayout SuitCargoStartingSlotLayout;
        [NMS(Index = 78)]
        /* 0x74D8 */ public GcInventoryLayout SuitStartingSlotLayout;
        [NMS(Index = 79)]
        /* 0x74F0 */ public GcInventoryLayout SuitTechOnlyStartingSlotLayout;
        [NMS(Index = 26)]
        /* 0x7508 */ public List<GcFilename> AlienPuzzleTables;
        [NMS(Index = 25)]
        /* 0x7518 */ public GcFilename AlienWordsTable;
        [NMS(Index = 20)]
        /* 0x7528 */ public GcFilename BaitDataTable;
        [NMS(Index = 62)]
        /* 0x7538 */ public List<GcRewardMissionOverride> BuilderMissionRewardOverrides;
        [NMS(Index = 49)]
        /* 0x7548 */ public GcFilename CombatEffectsTable;
        [NMS(Index = 13)]
        /* 0x7558 */ public GcFilename ConsumableItemTable;
        [NMS(Index = 42)]
        /* 0x7568 */ public GcFilename CostTable;
        [NMS(Index = 30)]
        /* 0x7578 */ public List<GcDamageMultiplierLookup> DamageMultiplierTable;
        [NMS(Index = 36)]
        /* 0x7588 */ public GcFilename DamageTable;
        [NMS(Index = 16)]
        /* 0x7598 */ public GcFilename DialogClearanceTable;
        [NMS(Index = 33)]
        /* 0x75A8 */ public GcFilename DiscoveryRewardTable;
        [NMS(Index = 90)]
        /* 0x75B8 */ public List<GcFiendCrimeSpawnTable> FiendCrimeSpawnTable;
        [NMS(Index = 19)]
        /* 0x75C8 */ public GcFilename FishDataTable;
        [NMS(Index = 91)]
        /* 0x75D8 */ public List<GcIDPair> FreighterBaseItemPairs;
        [NMS(Index = 51)]
        /* 0x75E8 */ public List<GcFreighterCargoOption> FreighterCargoOptions;
        [NMS(Index = 21)]
        /* 0x75F8 */ public GcFilename GameTableDiceDataTable;
        [NMS(Index = 31)]
        /* 0x7608 */ public GcFilename HistoricalSeasonDataTable;
        [NMS(Index = 35)]
        /* 0x7618 */ public GcFilename InventoryTable;
        [NMS(Index = 17)]
        /* 0x7628 */ public GcFilename ItemDescriptionOverrideTable;
        [NMS(Index = 11)]
        /* 0x7638 */ public GcFilename LegacyItemConversionTable;
        [NMS(Index = 12)]
        /* 0x7648 */ public List<TkRawID> LegacyRepairTable;
        [NMS(Index = 45)]
        /* 0x7658 */ public GcFilename MaintenanceGroupsTable;
        [NMS(Index = 18)]
        /* 0x7668 */ public GcFilename MaintenanceOverrideTable;
        [NMS(Index = 88)]
        /* 0x7678 */ public List<NMSString0x10> NeverOfferedForSale;
        [NMS(Index = 87)]
        /* 0x7688 */ public List<NMSString0x10> NeverSellableItems;
        [NMS(Index = 23)]
        /* 0x7698 */ public GcFilename PetBattlerMoveSetsTable;
        [NMS(Index = 22)]
        /* 0x76A8 */ public GcFilename PetBattlerMovesTable;
        [NMS(Index = 24)]
        /* 0x76B8 */ public GcFilename PetShopItemTable;
        [NMS(Index = 77)]
        /* 0x76C8 */ public List<NMSString0x10> PirateStationExtraProds;
        [NMS(Index = 48)]
        /* 0x76D8 */ public GcFilename PlayerWeaponPropertiesTable;
        [NMS(Index = 9)]
        /* 0x76E8 */ public GcFilename ProceduralProductTable;
        [NMS(Index = 10)]
        /* 0x76F8 */ public GcFilename ProceduralTechnologyTable;
        [NMS(Index = 37)]
        /* 0x7708 */ public GcFilename PurchaseableBuildingBlueprintsTable;
        [NMS(Index = 38)]
        /* 0x7718 */ public GcFilename PurchaseableSpecialsTable;
        [NMS(Index = 14)]
        /* 0x7728 */ public GcFilename RecipeTable;
        [NMS(Index = 32)]
        /* 0x7738 */ public GcFilename RewardTable;
        [NMS(Index = 47)]
        /* 0x7748 */ public GcFilename SettlementPerksTable;
        [NMS(Index = 73)]
        /* 0x7758 */ public GcTechList StationTechShops;
        [NMS(Index = 34)]
        /* 0x7768 */ public GcFilename StatRewardsTable;
        [NMS(Index = 15)]
        /* 0x7778 */ public GcFilename StoriesTable;
        [NMS(Index = 4)]
        /* 0x7788 */ public List<GcSubstanceSecondaryLookup> SubstanceSecondaryLookups;
        [NMS(Index = 7)]
        /* 0x7798 */ public GcFilename SubstanceTable;
        [NMS(Index = 86)]
        /* 0x77A8 */ public List<int> SuitCargoUpgradePrices;
        [NMS(Index = 85)]
        /* 0x77B8 */ public List<int> SuitTechOnlyUpgradePrices;
        [NMS(Index = 84)]
        /* 0x77C8 */ public List<int> SuitUpgradePrices;
        [NMS(Index = 50)]
        /* 0x77D8 */ public GcFilename TechBoxTable;
        [NMS(Index = 6)]
        /* 0x77E8 */ public GcFilename TechnologyTable;
        [NMS(Index = 44)]
        /* 0x77F8 */ public GcFilename TradingClassDataTable;
        [NMS(Index = 43)]
        /* 0x7808 */ public GcFilename TradingCostTable;
        [NMS(Index = 46)]
        /* 0x7818 */ public GcFilename UnlockableItemTrees;
        [NMS(Index = 41)]
        /* 0x7828 */ public GcFilename UnlockablePlatformRewardsTable;
        [NMS(Index = 39)]
        /* 0x7838 */ public GcFilename UnlockableSeasonRewardsTable;
        [NMS(Index = 40)]
        /* 0x7848 */ public GcFilename UnlockableTwitchRewardsTable;
        [NMS(Index = 71, Size = 0xD0, EnumType = typeof(GcStatsTypes.StatsTypeEnum))]
        /* 0x7858 */ public GcMinMaxFloat[] FoodStatValues;
        [NMS(Index = 28, Size = 0x9D, EnumType = typeof(GcInteractionType.InteractionTypeEnum))]
        /* 0x7ED8 */ public GcAlienPuzzleTableIndex[] InteractionPuzzlesIndexTypes;
        [NMS(Index = 2, Size = 0x11, EnumType = typeof(GcDiscoveryType.DiscoveryTypeEnum))]
        /* 0x814C */ public GcDiscoveryWorth[] DiscoveryWorth;
        [NMS(Index = 89, Size = 0x5)]
        /* 0x8328 */ public float[] NormalisedPriceLimits;
        [NMS(Index = 3, Size = 0x4, EnumType = typeof(GcCreatureSizeClasses.CreatureSizeClassEnum))]
        /* 0x833C */ public float[] CreatureDiscoverySizeMultiplier;
        [NMS(Index = 63, Size = 0x3, EnumType = typeof(GcRarity.RarityEnum))]
        /* 0x834C */ public float[] WeightedTextWeights;
        [NMS(Index = 0)]
        /* 0x8358 */ public ushort HomeRealityIteration;
        [NMS(Index = 1)]
        /* 0x835A */ public ushort RealityIteration;
        [NMS(Index = 27, Size = 0x9D, EnumType = typeof(GcInteractionType.InteractionTypeEnum))]
        /* 0x835C */ public bool[] LoopInteractionPuzzles;
        [NMS(Index = 29, Size = 0x7, EnumType = typeof(GcWeightingCurve.WeightingCurveEnum))]
        /* 0x83F9 */ public TkCurveType[] WeightingCurves;
    }
}
