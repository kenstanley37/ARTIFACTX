using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x6ACE7A5763500AB7, NameHash = 0xF5EE5140)]
    public class GcPhysicsCollisionTable : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<GcPhysicsCollisionGroupCollidesWith> CollisionTable;
    }
}
