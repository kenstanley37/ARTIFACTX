using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x45C4F32A1B5F00E8, NameHash = 0xBB7EFD7B)]
    public class GcSelectableObjectSpawnData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcResourceElement Resource;
    }
}
