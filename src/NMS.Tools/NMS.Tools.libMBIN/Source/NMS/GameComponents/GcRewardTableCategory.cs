using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xC0237A3FB66D9F88, NameHash = 0x29A7763F)]
    public class GcRewardTableCategory : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x3, EnumType = typeof(GcSizeIndicator.SizeIndicatorEnum))]
        /* 0x0 */ public GcRewardTableItemList[] Sizes;
    }
}
