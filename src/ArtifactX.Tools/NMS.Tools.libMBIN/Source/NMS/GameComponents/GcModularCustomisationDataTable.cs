using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x9E202874488E4AB4, NameHash = 0x54887B8D)]
    public class GcModularCustomisationDataTable : NMSTemplate
    {
        [NMS(Index = 0, Size = 0xB, EnumType = typeof(GcModularCustomisationResourceType.ModularCustomisationResourceTypeEnum))]
        /* 0x0000 */ public GcModularCustomisationConfig[] ModularCustomisationConfigs;
        [NMS(Index = 2, Size = 0xB, EnumType = typeof(GcModularCustomisationResourceType.ModularCustomisationResourceTypeEnum))]
        /* 0x1C30 */ public GcModularCustomisationProductLookupList[] ProductLookupLists;
        [NMS(Index = 1)]
        /* 0x1CE0 */ public List<GcModularCustomisationSlottableItemList> SharedSlottableItemLists;
    }
}
