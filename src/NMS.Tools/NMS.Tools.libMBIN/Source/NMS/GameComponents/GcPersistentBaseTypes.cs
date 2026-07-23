namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xC0C6BE0C9B6DDA83, NameHash = 0xF6DBD6AA)]
    public class GcPersistentBaseTypes : NMSTemplate
    {
        // size: 0xD
        public enum PersistentBaseTypesEnum : uint {
            HomePlanetBase,
            FreighterBase,
            ExternalPlanetBase,
            CivilianFreighterBase,
            FriendsPlanetBase,
            FriendsFreighterBase,
            SpaceBase,
            GeneratedPlanetBase,
            GeneratedPlanetBaseEdits,
            PlayerShipBase,
            FriendsShipBase,
            UITempShipBase,
            ShipBaseScratch,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public PersistentBaseTypesEnum PersistentBaseTypes;
    }
}
