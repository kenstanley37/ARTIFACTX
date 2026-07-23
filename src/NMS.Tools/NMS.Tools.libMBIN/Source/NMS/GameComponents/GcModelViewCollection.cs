using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x99703C75B6364FD, NameHash = 0xA5C6254C)]
    public class GcModelViewCollection : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x34, EnumType = typeof(GcModelViews.ModelViewsEnum))]
        /* 0x0 */ public TkModelRendererData[] ModelViewData;
    }
}
