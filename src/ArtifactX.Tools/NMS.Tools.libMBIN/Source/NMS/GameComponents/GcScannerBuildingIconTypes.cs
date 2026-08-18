namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x9BB4B5FF72A31CE4, NameHash = 0xF270419E)]
    public class GcScannerBuildingIconTypes : NMSTemplate
    {
        // size: 0x25
        public enum ScanBuildingIconTypeEnum : uint {
            None,
            Generic,
            Shelter,
            Relic,
            Factory,
            Unknown,
            Distress,
            Beacon,
            Waypoint,
            SpaceStation,
            TechResource,
            FuelResource,
            MineralResource,
            SpaceAnomaly,
            SpaceAtlas,
            ExternalBase,
            PlanetBaseTerminal,
            Nexus,
            AbandonedFreighter,
            Telescope,
            Outpost,
            UpgradePod,
            Cog,
            Ruins,
            Portal,
            Library,
            Abandoned,
            SmallBuilding,
            StoryGlitch,
            GraveInCave,
            HoloHub,
            Settlement,
            DroneHive,
            SentinelDistress,
            AbandonedRobotCamp,
            ScrapYard,
            Landfill,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public ScanBuildingIconTypeEnum ScanBuildingIconType;
    }
}
