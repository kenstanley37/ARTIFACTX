using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0x28825B6E8E051A79, NameHash = 0xCA24B3F1)]
    public class GcBuildableShipGlobals : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x000 */ public GcRewardSpecificShip DefaultCorvette;
        [NMS(Index = 0, Size = 0xD, EnumType = typeof(GcCorvettePartCategory.CorvettePartCategoryEnum))]
        /* 0x250 */ public NMSString0x20A[] PartTagLocIDs;
        [NMS(Index = 3)]
        /* 0x3F0 */ public List<GcFilename> InitialLayouts;
        [NMS(Index = 1, Size = 0xD, EnumType = typeof(GcCorvettePartCategory.CorvettePartCategoryEnum))]
        /* 0x400 */ public int[] PartFXLimits;
        [NMS(Index = 6, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0x434 */ public float[] InteriorVisibilityDistance;
        [NMS(Index = 5)]
        /* 0x444 */ public int ComplexityLimitWarning;
        [NMS(Index = 4)]
        /* 0x448 */ public int ComplexityLimitWarningNX;
        [NMS(Index = 7)]
        /* 0x44C */ public float SpawnOnRemoteCorvetteRequiredPartsRenderingDistance;
    }
}
