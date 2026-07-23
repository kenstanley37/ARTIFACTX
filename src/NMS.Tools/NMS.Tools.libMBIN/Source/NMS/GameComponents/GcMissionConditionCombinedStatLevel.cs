using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x312A44BF71690DDF, NameHash = 0x9D01DDF7)]
    public class GcMissionConditionCombinedStatLevel : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public List<NMSString0x10> Stats;
        [NMS(Index = 1)]
        /* 0x10 */ public int Amount;
        [NMS(Index = 2)]
        /* 0x14 */ public int Modulo;
        [NMS(Index = 3)]
        /* 0x18 */ public TkEqualityEnum Test;
    }
}
