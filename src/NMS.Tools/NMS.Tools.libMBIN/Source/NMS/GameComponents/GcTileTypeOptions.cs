using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xCF9D100C807AAB76, NameHash = 0x119404EE)]
    public class GcTileTypeOptions : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<TkPaletteTexture> Options;
    }
}
