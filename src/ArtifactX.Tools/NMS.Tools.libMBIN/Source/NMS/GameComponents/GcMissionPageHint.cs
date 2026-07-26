namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x289AA4247E44FE81, NameHash = 0x400FBCF9)]
    public class GcMissionPageHint : NMSTemplate
    {
        // size: 0xE
        public enum MissionPageHintEnum : uint {
            None,
            Suit,
            Ship,
            Weapon,
            Vehicle,
            Freighter,
            Wiki,
            Catalogue,
            MissionLog,
            Discovery,
            Journey,
            Expedition,
            Options,
            Pets,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public MissionPageHintEnum MissionPageHint;
    }
}
