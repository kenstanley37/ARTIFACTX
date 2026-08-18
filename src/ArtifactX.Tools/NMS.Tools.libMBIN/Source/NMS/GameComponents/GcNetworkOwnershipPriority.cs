namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x59488768BCEFCC30, NameHash = 0x94497FE7)]
    public class GcNetworkOwnershipPriority : NMSTemplate
    {
        // size: 0x5
        public enum NetworkOwnershipPriorityEnum : byte {
            Lowest,
            CargoInScrapyard,
            CargoOnTruckBed,
            CargoGrabbedByGravLaser,
            Highest,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public NetworkOwnershipPriorityEnum NetworkOwnershipPriority;
    }
}
