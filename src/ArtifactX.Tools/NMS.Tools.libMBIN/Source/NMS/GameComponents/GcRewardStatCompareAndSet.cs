namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x91A1F21C4A05BEDC, NameHash = 0x20E1705E)]
    public class GcRewardStatCompareAndSet : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public NMSString0x10 CompareAndSetStat;
        [NMS(Index = 0)]
        /* 0x10 */ public NMSString0x10 CoreStat;
    }
}
