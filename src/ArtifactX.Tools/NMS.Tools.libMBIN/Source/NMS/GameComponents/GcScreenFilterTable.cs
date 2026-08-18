using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE9179656CC02E90A, NameHash = 0x4E8AAD10)]
    public class GcScreenFilterTable : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x55, EnumType = typeof(GcScreenFilters.ScreenFilterEnum))]
        /* 0x0 */ public GcScreenFilterData[] Filters;
    }
}
