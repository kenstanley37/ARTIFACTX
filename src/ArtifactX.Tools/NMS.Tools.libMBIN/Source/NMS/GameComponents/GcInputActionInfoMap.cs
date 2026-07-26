using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x4B918DE751B061D2, NameHash = 0x25517A76)]
    public class GcInputActionInfoMap : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x12C, EnumType = typeof(GcInputActions.InputActionEnum))]
        /* 0x0 */ public GcInputActionInfo[] ActionMap;
    }
}
