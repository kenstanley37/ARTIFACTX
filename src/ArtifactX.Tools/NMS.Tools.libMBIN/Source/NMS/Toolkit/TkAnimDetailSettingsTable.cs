using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xE2B8F54FD237E43B, NameHash = 0x338F3FF)]
    public class TkAnimDetailSettingsTable : NMSTemplate
    {
        [NMS(Index = 1, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0x00 */ public TkAnimDetailSettings[] Table;
        [NMS(Index = 0)]
        /* 0x80 */ public NMSString0x10 Id;
    }
}
