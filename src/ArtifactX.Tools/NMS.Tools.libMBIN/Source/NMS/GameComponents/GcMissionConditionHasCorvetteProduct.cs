using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x621D89AACB47848D, NameHash = 0x7F140632)]
    public class GcMissionConditionHasCorvetteProduct : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x0 */ public int Amount;
        [NMS(Index = 1)]
        /* 0x4 */ public GcCorvettePartCategory PartType;
        [NMS(Index = 3)]
        /* 0x8 */ public TkEqualityEnum Test;
        [NMS(Index = 0)]
        /* 0xC */ public bool SpecificPartType;
    }
}
