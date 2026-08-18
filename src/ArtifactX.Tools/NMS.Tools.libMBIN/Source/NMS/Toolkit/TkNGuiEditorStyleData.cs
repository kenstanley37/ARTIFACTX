using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xD71D6985F1F1BDEE, NameHash = 0xE9E49954)]
    public class TkNGuiEditorStyleData : NMSTemplate
    {
        [NMS(Index = 1, Size = 0x8, MxmlName = "Skin Colours")]
        /* 0x0000 */ public TkNGuiEditorStyleColour[] SkinColours;
        [NMS(Index = 3)]
        /* 0x0480 */ public GcFilename Font;
        [NMS(Index = 7)]
        /* 0x0490 */ public List<TkNGuiLayoutShortcut> LayoutShortcuts;
        [NMS(Index = 6)]
        /* 0x04A0 */ public List<float> SnapSettings;
        [NMS(Index = 4, Size = 0x60, EnumType = typeof(TkNGuiEditorGraphicType.NGuiEditorGraphicEnum))]
        /* 0x04B0 */ public TkNGuiGraphicStyle[] GraphicStyles;
        [NMS(Index = 5, Size = 0xF, EnumType = typeof(TKNGuiEditorTextType.NGuiEditorTextEnum))]
        /* 0x94B0 */ public TkNGuiTextStyle[] TextStyles;
        [NMS(Index = 0, Size = 0x42, EnumType = typeof(TKNGuiEditorComponentSize.NGuiEditorComponentSizeEnum))]
        /* 0x9E88 */ public float[] Sizes;
        [NMS(Index = 2, MxmlName = "Skin Font Height")]
        /* 0x9F90 */ public float SkinFontHeight;
    }
}
