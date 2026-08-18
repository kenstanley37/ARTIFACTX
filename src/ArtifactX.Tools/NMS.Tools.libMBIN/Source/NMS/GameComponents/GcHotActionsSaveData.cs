using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xB1452287E2BCAC62, NameHash = 0x13A770D3)]
    public class GcHotActionsSaveData : NMSTemplate
    {
        [NMS(Index = 0, Size = 0xA)]
        /* 0x0 */ public GcQuickMenuActionSaveData[] KeyActions;
    }
}
