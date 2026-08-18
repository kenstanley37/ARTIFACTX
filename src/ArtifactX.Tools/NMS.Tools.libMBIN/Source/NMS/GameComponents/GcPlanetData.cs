using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xA479F24F88E79FFC, NameHash = 0x17A032B6)]
    public class GcPlanetData : NMSTemplate
    {
        [NMS(Index = 12)]
        /* 0x0000 */ public GcPlanetColourData Colours;
        [NMS(Index = 14)]
        /* 0x1C00 */ public GcPlanetWeatherData Weather;
        [NMS(Index = 13, Size = 0x17)]
        /* 0x1D80 */ public Colour[] TileColours;
        [NMS(Index = 27)]
        /* 0x1EF0 */ public GcPlanetRingData Rings;
        [NMS(Index = 18)]
        /* 0x1F50 */ public TkVoxelGeneratorData Terrain;
        [NMS(Index = 24)]
        /* 0x30A0 */ public GcPlanetGenerationIntermediateData GenerationData;
        [NMS(Index = 21)]
        /* 0x31F8 */ public GcEnvironmentSpawnData SpawnData;
        [NMS(Index = 23)]
        /* 0x3258 */ public GcPlanetBuildingData BuildingData;
        [NMS(Index = 15)]
        /* 0x32A8 */ public GcPlanetCloudProperties Clouds;
        [NMS(Index = 6)]
        /* 0x32F0 */ public NMSString0x10 CommonSubstanceID;
        [NMS(Index = 11)]
        /* 0x3300 */ public List<NMSString0x10> CreatureIDs;
        [NMS(Index = 10)]
        /* 0x3310 */ public List<GcPlanetDataResourceHint> ExtraResourceHints;
        [NMS(Index = 8)]
        /* 0x3320 */ public NMSString0x10 RareSubstanceID;
        [NMS(Index = 17)]
        /* 0x3330 */ public GcFilename TerrainFile;
        [NMS(Index = 20)]
        /* 0x3340 */ public List<int> TileTypeIndices;
        [NMS(Index = 7)]
        /* 0x3350 */ public NMSString0x10 UncommonSubstanceID;
        [NMS(Index = 3)]
        /* 0x3360 */ public GcPlanetHazardData Hazard;
        [NMS(Index = 25, Size = 0x4, EnumType = typeof(GcCombatTimerDifficultyOption.CombatTimerDifficultyOptionEnum))]
        /* 0x33D8 */ public GcPlanetGroundCombatData[] GroundCombatDataPerDifficulty;
        [NMS(Index = 16)]
        /* 0x3438 */ public GcPlanetWaterData Water;
        [NMS(Index = 5)]
        /* 0x3448 */ public GcBuildingDensityLevels BuildingLevel;
        [NMS(Index = 2)]
        /* 0x344C */ public GcPlanetLife CreatureLife;
        [NMS(Index = 31)]
        /* 0x3450 */ public float FuelMultiplier;
        [NMS(Index = 22)]
        /* 0x3454 */ public GcAlienRace InhabitingRace;
        [NMS(Index = 1)]
        /* 0x3458 */ public GcPlanetLife Life;
        [NMS(Index = 32)]
        /* 0x345C */ public int PlanetIndex;
        // size: 0x2
        public enum ResourceLevelEnum : uint {
            Low,
            High,
        }
        [NMS(Index = 4)]
        /* 0x3460 */ public ResourceLevelEnum ResourceLevel;
        [NMS(Index = 19)]
        /* 0x3464 */ public int TileTypeSet;
        [NMS(Index = 26)]
        /* 0x3468 */ public GcPlanetInfo PlanetInfo;
        [NMS(Index = 0)]
        /* 0x396E */ public NMSString0x80 Name;
        [NMS(Index = 9)]
        /* 0x39EE */ public bool HasScrap;
        [NMS(Index = 29)]
        /* 0x39EF */ public bool InAbandonedSystem;
        [NMS(Index = 28)]
        /* 0x39F0 */ public bool InEmptySystem;
        [NMS(Index = 30)]
        /* 0x39F1 */ public bool InGasGiantSystem;
    }
}
