using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x9E0AD952DE93C4CC, NameHash = 0x3165E96F)]
    public class GcRewardSpecificPetEgg : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x000 */ public GcPetData EggData;
        [NMS(Index = 0)]
        /* 0x2F8 */ public bool ImmediatelyHatchable;
    }
}
