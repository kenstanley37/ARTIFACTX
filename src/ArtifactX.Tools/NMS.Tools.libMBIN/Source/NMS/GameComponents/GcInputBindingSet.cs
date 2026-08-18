using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xB7C931EBE912BF19, NameHash = 0x8D898CAF)]
    public class GcInputBindingSet : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public List<GcInputBinding> InputBindings;
        [NMS(Index = 0)]
        /* 0x10 */ public GcActionSetType Type;
    }
}
