using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xC0D9FFEECD13EF18, NameHash = 0xA547E834)]
    public class GcRealityIconTable : NMSTemplate
    {
        [NMS(Index = 15, Size = 0x7F, EnumType = typeof(GcRealityGameIcons.GameIconsEnum))]
        /* 0x0000 */ public TkTextureResource[] GameIcons;
        [NMS(Index = 5, Size = 0x11, EnumType = typeof(GcDiscoveryType.DiscoveryTypeEnum))]
        /* 0x0BE8 */ public TkTextureResource[] BinocularDiscoveryIcons;
        [NMS(Index = 3, Size = 0xB, EnumType = typeof(GcProductCategory.ProductCategoryEnum))]
        /* 0x0D80 */ public TkTextureResource[] ProductCategoryIcons;
        [NMS(Index = 4, Size = 0xA, EnumType = typeof(GcMissionFaction.MissionFactionEnum))]
        /* 0x0E88 */ public TkTextureResource[] MissionFactionIcons;
        [NMS(Index = 8, Size = 0x9, EnumType = typeof(GcAlienRace.AlienRaceEnum))]
        /* 0x0F78 */ public TkTextureResource[] DiscoveryPageRaceIcons;
        [NMS(Index = 22, Size = 0x9, EnumType = typeof(GcPetBattlerAffinity.PetBattlerAffinityEnum))]
        /* 0x1050 */ public TkTextureResource[] PetBattlerAffinityBinocsIcons;
        [NMS(Index = 27, Size = 0x9, EnumType = typeof(GcPetBattlerAffinity.PetBattlerAffinityEnum))]
        /* 0x1128 */ public TkTextureResource[] PetBattlerAffinityBuffIcons;
        [NMS(Index = 28, Size = 0x9, EnumType = typeof(GcPetBattlerAffinity.PetBattlerAffinityEnum))]
        /* 0x1200 */ public TkTextureResource[] PetBattlerAffinityDebuffIcons;
        [NMS(Index = 21, Size = 0x9, EnumType = typeof(GcPetBattlerAffinity.PetBattlerAffinityEnum))]
        /* 0x12D8 */ public TkTextureResource[] PetBattlerAffinityIcons;
        [NMS(Index = 2, Size = 0x9, EnumType = typeof(GcRealitySubstanceCategory.SubstanceCategoryEnum))]
        /* 0x13B0 */ public TkTextureResource[] SubstanceCategoryIcons;
        [NMS(Index = 24, Size = 0x8, EnumType = typeof(GcPetBattlerIconStyle.PetBattlerIconEnum))]
        /* 0x1488 */ public TkTextureResource[] PetBattlerBGMoveIcons;
        [NMS(Index = 23, Size = 0x8, EnumType = typeof(GcPetBattlerIconStyle.PetBattlerIconEnum))]
        /* 0x1548 */ public TkTextureResource[] PetBattlerBuffMoveIcons;
        [NMS(Index = 26, Size = 0x8, EnumType = typeof(GcPetBattlerIconStyle.PetBattlerIconEnum))]
        /* 0x1608 */ public TkTextureResource[] PetBattlerCoreBuffIcons;
        [NMS(Index = 25, Size = 0x8, EnumType = typeof(GcPetBattlerIconStyle.PetBattlerIconEnum))]
        /* 0x16C8 */ public TkTextureResource[] PetBattlerCoreDebuffIcons;
        [NMS(Index = 17, Size = 0x7, EnumType = typeof(GcDifficultyPresetType.DifficultyPresetTypeEnum))]
        /* 0x1788 */ public TkTextureResource[] DifficultyPresetIcons;
        [NMS(Index = 6, Size = 0x7, EnumType = typeof(GcTradingClass.TradingClassEnum))]
        /* 0x1830 */ public TkTextureResource[] DiscoveryPageTradingIcons;
        [NMS(Index = 0, Size = 0x7, EnumType = typeof(GcPlayerHazardType.HazardEnum))]
        /* 0x18D8 */ public TkTextureResource[] HazardIcons;
        [NMS(Index = 1, Size = 0x7, EnumType = typeof(GcPlayerHazardType.HazardEnum))]
        /* 0x1980 */ public TkTextureResource[] HazardIconsHUD;
        [NMS(Index = 19, Size = 0x6, EnumType = typeof(GcOptionsUIHeaderIcons.OptionsUIHeaderIconTypeEnum))]
        /* 0x1A28 */ public TkTextureResource[] OptionsUIHeaderIcons;
        [NMS(Index = 20, Size = 0x5, EnumType = typeof(GcInventoryFilterOptions.InventoryFilterEnum))]
        /* 0x1AB8 */ public TkTextureResource[] InventoryFilterIcons;
        [NMS(Index = 18, Size = 0x4, EnumType = typeof(GcDifficultyOptionGroups.DifficultyOptionGroupEnum))]
        /* 0x1B30 */ public TkTextureResource[] DifficultyUIOptionIcons;
        [NMS(Index = 7, Size = 0x4, EnumType = typeof(GcPlayerConflictData.ConflictLevelEnum))]
        /* 0x1B90 */ public TkTextureResource[] DiscoveryPageConflictIcons;
        [NMS(Index = 16, KeyField = "ID")]
        /* 0x1BF0 */ public HashMap<GcRealityIcon> MissionDetailIcons;
        [NMS(Index = 10)]
        /* 0x1C20 */ public TkTextureResource DiscoveryPageConflictUnknown;
        [NMS(Index = 11)]
        /* 0x1C38 */ public TkTextureResource DiscoveryPageRaceUnknown;
        [NMS(Index = 9)]
        /* 0x1C50 */ public TkTextureResource DiscoveryPageTradingUnknown;
        [NMS(Index = 13)]
        /* 0x1C68 */ public List<GcPlanetResourceIconLookup> PlanetResourceIconLookups;
        [NMS(Index = 14)]
        /* 0x1C78 */ public List<TkTextureResource> RepairTechIcons;
        [NMS(Index = 12)]
        /* 0x1C88 */ public List<GcPlanetResourceIconLookup> TerrainIconLookups;
    }
}
