using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xA2F20A7AAE7C631A, NameHash = 0x3D61DD26)]
    public class GcBaseBuildingPartsTable : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<GcBaseBuildingPart> Parts;
    }
}
