using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x8EF9BEFE26AB2F67, NameHash = 0xBA9A777F)]
    public class GcTagComponentData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcStaticTag StaticTags;
    }
}
