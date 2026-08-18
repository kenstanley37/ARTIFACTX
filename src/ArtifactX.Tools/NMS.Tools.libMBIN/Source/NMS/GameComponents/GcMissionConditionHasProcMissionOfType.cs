using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x2698AB9C80895CE3, NameHash = 0x38B5596C)]
    public class GcMissionConditionHasProcMissionOfType : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcMissionType Type;
    }
}
