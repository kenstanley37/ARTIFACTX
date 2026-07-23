using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x4A29526AC7A5FD71, NameHash = 0xA6E31F07)]
    public class GcPlayerControlInput : NMSTemplate
    {
        [NMS(Index = 3)]
        /* 0x00 */ public List<NMSTemplate> Inputs;
        [NMS(Index = 2)]
        /* 0x10 */ public List<GcInputActions> InterceptInputBlackList;
        [NMS(Index = 1)]
        /* 0x20 */ public List<GcInputActions> InterceptInputWhitelist;
        [NMS(Index = 0)]
        /* 0x30 */ public bool InterceptAllInputs;
    }
}
