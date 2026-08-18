using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x89D73442C027218A, NameHash = 0x7D76DCB)]
    public class TkAnimationDataTable : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<TkAnimationData> Table;
    }
}
