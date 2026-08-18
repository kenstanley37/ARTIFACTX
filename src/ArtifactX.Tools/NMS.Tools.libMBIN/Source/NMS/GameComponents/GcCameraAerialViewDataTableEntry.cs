using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x6E30F7CB202EE906, NameHash = 0xE15978A5)]
    public class GcCameraAerialViewDataTableEntry : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public NMSString0x10 ID;
        [NMS(Index = 1)]
        /* 0x10 */ public GcCameraAerialViewData CameraAerialViewData;
    }
}
