namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x6DEB6C57B1F85C51, NameHash = 0xA18094D9)]
    public class GcBaseBuildingSecondaryMode : NMSTemplate
    {
        // size: 0x1
        public enum BaseBuildingSecondaryModeEnum : uint {
            ShipStructural,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public BaseBuildingSecondaryModeEnum BaseBuildingSecondaryMode;
    }
}
