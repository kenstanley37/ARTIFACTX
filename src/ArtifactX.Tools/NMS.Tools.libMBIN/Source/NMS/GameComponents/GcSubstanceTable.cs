using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x4BECE908D6111505, NameHash = 0x7A9FBCC1)]
    public class GcSubstanceTable : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public List<GcRealityCraftingRecipeData> Crafting;
        [NMS(Index = 0)]
        /* 0x10 */ public List<GcRealitySubstanceData> Table;
    }
}
