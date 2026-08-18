using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x1A1D8EF2063F8D78, NameHash = 0x6D1FFAE5)]
    public class TkNGuiTextStyle : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x00 */ public TkNGuiTextStyleData Active;
        [NMS(Index = 0)]
        /* 0x38 */ public TkNGuiTextStyleData Default;
        [NMS(Index = 1)]
        /* 0x70 */ public TkNGuiTextStyleData Highlight;
    }
}
