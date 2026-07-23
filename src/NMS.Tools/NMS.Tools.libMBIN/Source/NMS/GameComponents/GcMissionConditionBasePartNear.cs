namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x3B95D01DB178DAE2, NameHash = 0xE90A0099)]
    public class GcMissionConditionBasePartNear : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public NMSString0x10 PartID;
        [NMS(Index = 2)]
        /* 0x10 */ public float Distance;
        [NMS(Index = 1)]
        /* 0x14 */ public bool TakeIDFromSeasonData;
    }
}
