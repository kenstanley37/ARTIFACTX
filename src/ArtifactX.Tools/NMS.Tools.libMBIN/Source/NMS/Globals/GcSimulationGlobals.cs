using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0x5CDB38B1D1D2DFCB, NameHash = 0xA8487890)]
    public class GcSimulationGlobals : NMSTemplate
    {
        [NMS(Index = 7)]
        /* 0x000 */ public GcFilename AbandonedSpaceStationFile;
        [NMS(Index = 10)]
        /* 0x010 */ public List<GcFilename> AtlasStationAnomalies;
        [NMS(Index = 11)]
        /* 0x020 */ public GcFilename BackgroundSwarmHive;
        [NMS(Index = 9)]
        /* 0x030 */ public List<GcFilename> BlackHoleAnomalies;
        [NMS(Index = 36)]
        /* 0x040 */ public GcFilename BlackHoleTunnelFile;
        [NMS(Index = 16)]
        /* 0x050 */ public GcFilename HeavyAirAbandonedFreighter;
        [NMS(Index = 14)]
        /* 0x060 */ public GcFilename HeavyAirCave;
        [NMS(Index = 17)]
        /* 0x070 */ public GcFilename HeavyAirSpaceStormDefault;
        [NMS(Index = 18)]
        /* 0x080 */ public List<GcSpaceStormData> HeavyAirSpaceStormList;
        [NMS(Index = 15)]
        /* 0x090 */ public GcFilename HeavyAirUnderwater;
        [NMS(Index = 1)]
        /* 0x0A0 */ public List<GcMultitoolPoolData> MultitoolPool;
        [NMS(Index = 5)]
        /* 0x0B0 */ public GcFilename NexusExteriorFile;
        [NMS(Index = 4)]
        /* 0x0C0 */ public GcFilename NexusFile;
        [NMS(Index = 12)]
        /* 0x0D0 */ public GcFilename None;
        [NMS(Index = 8)]
        /* 0x0E0 */ public GcFilename PirateSystemSpaceStationFile;
        [NMS(Index = 6)]
        /* 0x0F0 */ public GcFilename PlaceMarkerFile;
        [NMS(Index = 13)]
        /* 0x100 */ public GcFilename PlacementDroneFile;
        [NMS(Index = 21)]
        /* 0x110 */ public GcFilename PlanetAtmosphereFile;
        [NMS(Index = 22)]
        /* 0x120 */ public GcFilename PlanetAtmosphereMaterialFile;
        [NMS(Index = 23)]
        /* 0x130 */ public GcFilename PlanetGasGiantAtmosphereFile;
        [NMS(Index = 24)]
        /* 0x140 */ public GcFilename PlanetGasGiantAtmosphereMaterialFile;
        [NMS(Index = 27)]
        /* 0x150 */ public GcFilename PlanetMaterialFile;
        [NMS(Index = 25)]
        /* 0x160 */ public GcFilename PlanetRingFile;
        [NMS(Index = 26)]
        /* 0x170 */ public GcFilename PlanetRingMaterialFile;
        [NMS(Index = 28)]
        /* 0x180 */ public List<GcFilename> PlanetTerrainMaterials;
        [NMS(Index = 39)]
        /* 0x190 */ public GcFilename PortalStoryTunnelFile;
        [NMS(Index = 38)]
        /* 0x1A0 */ public GcFilename PortalTunnelFile;
        [NMS(Index = 20)]
        /* 0x1B0 */ public List<GcFilename> PrefetchMaterialResources;
        [NMS(Index = 19)]
        /* 0x1C0 */ public List<GcFilename> PrefetchScenegraphResources;
        [NMS(Index = 29)]
        /* 0x1D0 */ public List<GcFilename> PrefetchTextureResources;
        [NMS(Index = 3)]
        /* 0x1E0 */ public GcFilename SpaceStationFile;
        [NMS(Index = 0)]
        /* 0x1F0 */ public GcFilename StartingSceneFile;
        [NMS(Index = 37)]
        /* 0x200 */ public GcFilename TeleportTunnelFile;
        [NMS(Index = 35)]
        /* 0x210 */ public GcFilename WarpTunnelFile;
        [NMS(Index = 2)]
        /* 0x220 */ public ulong ProceduralBuildingsGenerationSeed;
        [NMS(Index = 33)]
        /* 0x228 */ public float GasGiantFadeDistanceEnd;
        [NMS(Index = 32)]
        /* 0x22C */ public float GasGiantFadeDistanceStart;
        [NMS(Index = 30)]
        /* 0x230 */ public float GasGiantFlowSpeed;
        [NMS(Index = 31)]
        /* 0x234 */ public float GasGiantFlowStrength;
        [NMS(Index = 34)]
        /* 0x238 */ public float WarpTunnelScale;
    }
}
