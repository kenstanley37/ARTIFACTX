using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xDA229C9691388E8B, NameHash = 0xC6DB83D2)]
    public class GcSolarSystemData : NMSTemplate
    {
        [NMS(Index = 26)]
        /* 0x0000 */ public GcPlanetColourData Colours;
        [NMS(Index = 20)]
        /* 0x1C00 */ public GcSpaceStationSpawnData SpaceStationSpawn;
        [NMS(Index = 28)]
        /* 0x1D40 */ public GcSpaceSkyProperties Sky;
        [NMS(Index = 5, Size = 0x8, MxmlName = "Planet Positions")]
        /* 0x1DE0 */ public Vector3f[] PlanetPositions;
        [NMS(Index = 27)]
        /* 0x1E60 */ public GcLightProperties Light;
        [NMS(Index = 9)]
        /* 0x1E90 */ public Vector3f SunPosition;
        [NMS(Index = 6, Size = 0x8, MxmlName = "Planet Generation Inputs")]
        /* 0x1EA0 */ public GcPlanetGenerationInputData[] PlanetGenerationInputs;
        [NMS(Index = 24)]
        /* 0x2160 */ public List<NMSTemplate> AsteroidGenerators;
        [NMS(Index = 10)]
        /* 0x2170 */ public NMSString0x10 AsteroidSubstanceID;
        [NMS(Index = 30)]
        /* 0x2180 */ public GcFilename HeavyAir;
        [NMS(Index = 23)]
        /* 0x2190 */ public List<GcSolarSystemLocator> Locators;
        [NMS(Index = 0)]
        /* 0x21A0 */ public GcSeed Seed;
        [NMS(Index = 32)]
        /* 0x21B0 */ public GcSeed SentinelCrashSiteShipSeed;
        [NMS(Index = 31)]
        /* 0x21C0 */ public List<GcAISpaceshipPreloadCacheData> SystemShips;
        [NMS(Index = 7, Size = 0x8, MxmlName = "Planet Orbits")]
        /* 0x21D0 */ public int[] PlanetOrbits;
        [NMS(Index = 22)]
        /* 0x21F0 */ public GcSolarSystemTraderSpawnData TraderSpawnInStations;
        [NMS(Index = 21)]
        /* 0x2204 */ public GcSolarSystemTraderSpawnData TraderSpawnOnOutposts;
        [NMS(Index = 18)]
        /* 0x2218 */ public Vector2f FlybyTimer;
        [NMS(Index = 15)]
        /* 0x2220 */ public Vector2f FreighterTimer;
        [NMS(Index = 17)]
        /* 0x2228 */ public Vector2f PlanetPirateTimer;
        [NMS(Index = 19)]
        /* 0x2230 */ public Vector2f PoliceTimer;
        [NMS(Index = 16)]
        /* 0x2238 */ public Vector2f SpacePirateTimer;
        [NMS(Index = 34)]
        /* 0x2240 */ public GcPlanetTradingData TradingData;
        // size: 0x3
        public enum AsteroidLevelEnum : uint {
            NoRares,
            SomeRares,
            LotsOfRares,
        }
        [NMS(Index = 25)]
        /* 0x2248 */ public AsteroidLevelEnum AsteroidLevel;
        [NMS(Index = 2)]
        /* 0x224C */ public GcSolarSystemClass Class;
        [NMS(Index = 35)]
        /* 0x2250 */ public GcPlayerConflictData ConflictData;
        [NMS(Index = 33)]
        /* 0x2254 */ public GcAlienRace InhabitingRace;
        [NMS(Index = 13)]
        /* 0x2258 */ public int MaxNumFreighters;
        [NMS(Index = 11)]
        /* 0x225C */ public int NumTradeRoutes;
        [NMS(Index = 12)]
        /* 0x2260 */ public int NumVisibleTradeRoutes;
        [NMS(Index = 4)]
        /* 0x2264 */ public int Planets;
        [NMS(Index = 8)]
        /* 0x2268 */ public int PrimePlanets;
        [NMS(Index = 29)]
        /* 0x226C */ public GcScreenFilters ScreenFilter;
        [NMS(Index = 3)]
        /* 0x2270 */ public GcGalaxyStarTypes StarType;
        [NMS(Index = 1)]
        /* 0x2274 */ public NMSString0x80 Name;
        [NMS(Index = 14)]
        /* 0x22F4 */ public bool StartWithFreighters;
    }
}
