using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x38CF8ECA91D18042, NameHash = 0xEE278077)]
    public class GcActionSetAction : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x0 */ public GcInputActions Action;
        [NMS(Index = 0)]
        /* 0x4 */ public GcActionUseType Status;
    }
}
