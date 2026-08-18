using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xAB3D61944CE57F10, NameHash = 0x76E42785)]
    public class GcMissionConditionGrabbingRecyclableOfType : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcRecyclableType RequiredType;
    }
}
