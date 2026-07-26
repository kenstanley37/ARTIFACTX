namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE8B4C61BFD09BDC1, NameHash = 0x9E30CF48)]
    public class GcMissionConditionFormatStat : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public NMSString0x10 Stat;
        [NMS(Index = 1)]
        /* 0x10 */ public VariableSizeString TextTagToUse;
    }
}
