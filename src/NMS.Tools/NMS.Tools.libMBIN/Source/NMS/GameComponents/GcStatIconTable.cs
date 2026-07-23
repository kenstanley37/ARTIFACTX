using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x6FAFD84404F24C47, NameHash = 0xE97F7AA9)]
    public class GcStatIconTable : NMSTemplate
    {
        [NMS(Index = 0, Size = 0xD0, EnumType = typeof(GcStatsTypes.StatsTypeEnum))]
        /* 0x0 */ public GcFilename[] StatIcons;
    }
}
