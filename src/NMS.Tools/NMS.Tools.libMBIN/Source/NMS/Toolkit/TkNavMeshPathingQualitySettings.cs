using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x525831296D80C2DE, NameHash = 0xF3B44A34)]
    public class TkNavMeshPathingQualitySettings : NMSTemplate
    {
        [NMS(Index = 3)]
        /* 0x00 */ public TkNavMeshVelocitySamplingParams VelocitySamplingParams;
        [NMS(Index = 2)]
        /* 0x38 */ public float CollisionQueryRange;
        [NMS(Index = 0)]
        /* 0x3C */ public float HeuristicScale;
        [NMS(Index = 1)]
        /* 0x40 */ public bool UseRaycastShortcuts;
    }
}
