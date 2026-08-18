namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x62CE040D4B1ED4C3, NameHash = 0x42E551BD)]
    public class GcMissionType : NMSTemplate
    {
        // size: 0x24
        public enum MissionTypeEnum : uint {
            SpaceCombat,
            GroundCombat,
            Research,
            MissingPerson,
            Repair,
            Cargo,
            Piracy,
            Photo,
            Feeding,
            Planting,
            Construction,
            LocalCorrupted,
            LocalCorruptedCombat,
            LocalSalvage,
            LocalBiomePlants,
            LocalExtreme,
            LocalBones,
            LocalInfested,
            LocalPlanetaryPirates,
            LocalPredators,
            LocalSentinels,
            BuildersLanguage,
            Fishing,
            CorvetteRobots,
            CorvetteTreeScanning,
            CorvettePredators,
            CorvetteCollectItem,
            CorvetteMultiWorld,
            CorvetteTreasure,
            CorvetteSalvage,
            CorvetteFeeding,
            CorvetteGroundCombat,
            CorvetteFiendKill,
            SwarmCombat,
            SwarmClean,
            SwarmIntel,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public MissionTypeEnum MissionType;
    }
}
