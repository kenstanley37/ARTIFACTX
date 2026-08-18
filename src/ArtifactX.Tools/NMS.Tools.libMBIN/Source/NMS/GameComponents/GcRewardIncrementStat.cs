namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xDE8122FCB8975263, NameHash = 0xEBF030D0)]
    public class GcRewardIncrementStat : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public NMSString0x10 Stat;
        [NMS(Index = 1)]
        /* 0x10 */ public int Amount;
    }
}
