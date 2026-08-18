using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x77D20DAF9EE72365, NameHash = 0x1EF39842)]
    public class GcNGuiPreset : NMSTemplate
    {
        [NMS(Index = 3, Size = 0xA)]
        /* 0x0000 */ public GcNGuiPresetText[] Text;
        [NMS(Index = 2, Size = 0xA)]
        /* 0x19A0 */ public GcNGuiPresetGraphic[] Graphic;
        [NMS(Index = 1, Size = 0xA)]
        /* 0x2CB0 */ public GcNGuiPresetGraphic[] Layer;
        [NMS(Index = 4, MxmlName = "Spacing Layout")]
        /* 0x3FC0 */ public GcNGuiLayoutData SpacingLayout;
        [NMS(Index = 0)]
        /* 0x4008 */ public GcFilename Font;
    }
}
