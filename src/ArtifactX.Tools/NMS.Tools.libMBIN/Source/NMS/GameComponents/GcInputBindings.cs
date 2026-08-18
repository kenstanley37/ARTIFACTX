using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xAC866027178D6DF0, NameHash = 0xE7C17DC9)]
    public class GcInputBindings : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<GcInputBindingSet> InputBindingSets;
    }
}
