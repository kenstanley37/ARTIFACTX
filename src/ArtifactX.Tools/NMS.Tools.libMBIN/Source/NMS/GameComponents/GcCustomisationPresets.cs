using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xDF89E9D6FD2B544E, NameHash = 0xFE6613FE)]
    public class GcCustomisationPresets : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public List<GcCustomisationDescriptorGroupFallbackData> DescriptorGroupFallbackMap;
        [NMS(Index = 0)]
        /* 0x10 */ public List<GcCustomisationPreset> Presets;
    }
}
