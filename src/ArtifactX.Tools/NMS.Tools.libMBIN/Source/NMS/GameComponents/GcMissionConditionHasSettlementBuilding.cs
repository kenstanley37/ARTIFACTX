using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x2FEF87FFA629C21C, NameHash = 0xD9D7367B)]
    public class GcMissionConditionHasSettlementBuilding : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcBuildingClassification BuildingClass;
        [NMS(Index = 1)]
        /* 0x1 */ public bool RequireComplete;
    }
}
