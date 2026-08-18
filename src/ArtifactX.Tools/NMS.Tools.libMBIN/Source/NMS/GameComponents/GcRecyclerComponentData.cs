using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x94F9E89AA57C1DDF, NameHash = 0xC3CD194A)]
    public class GcRecyclerComponentData : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public NMSString0x10 PlayerDamage;
        [NMS(Index = 0)]
        /* 0x10 */ public GcRecyclableType RecycleType;
    }
}
