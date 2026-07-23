using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x7CE131DC42B0C189, NameHash = 0xBB983831)]
    public class GcRepairTechData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x000 */ public GcMaintenanceContainer MaintenanceContainer;
        [NMS(Index = 3)]
        /* 0x1A0 */ public GcInventoryIndex InventoryIndex;
        [NMS(Index = 2)]
        /* 0x1A8 */ public int InventorySubIndex;
        [NMS(Index = 1)]
        /* 0x1AC */ public int InventoryType;
    }
}
