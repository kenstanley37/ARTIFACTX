using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xA2CD023F151141D9, NameHash = 0x6EDC332)]
    public class GcProductTable : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<GcProductData> Table;
    }
}
