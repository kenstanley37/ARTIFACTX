namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xD26F78F45065C818, NameHash = 0x35BA4E99)]
    public class GcDiscoveryType : NMSTemplate
    {
        // size: 0x11
        public enum DiscoveryTypeEnum : uint {
            Unknown,
            SolarSystem,
            Planet,
            Animal,
            Flora,
            Mineral,
            Sector,
            Building,
            Interactable,
            Sentinel,
            Starship,
            Artifact,
            Mystery,
            Treasure,
            Control,
            HarvestPlant,
            FriendlyDrone,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public DiscoveryTypeEnum DiscoveryType;
    }
}
