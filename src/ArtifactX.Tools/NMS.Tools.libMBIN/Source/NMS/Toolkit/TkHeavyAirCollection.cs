using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xBA66C3185413DA1A, NameHash = 0x3DCFEF03)]
    public class TkHeavyAirCollection : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<TkHeavyAirData> HeavyAirSystems;
    }
}
