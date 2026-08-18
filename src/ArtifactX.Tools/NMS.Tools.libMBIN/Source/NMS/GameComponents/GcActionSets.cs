using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x1B2E426E7989001, NameHash = 0xC212E44)]
    public class GcActionSets : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<GcActionSet> ActionSets;
    }
}
