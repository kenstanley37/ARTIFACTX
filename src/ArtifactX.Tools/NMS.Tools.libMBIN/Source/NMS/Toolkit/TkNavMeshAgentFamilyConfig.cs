namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xCF2D8938DB332F00, NameHash = 0xE6019495)]
    public class TkNavMeshAgentFamilyConfig : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public float LowHeightThreshold;
        [NMS(Index = 2)]
        /* 0x04 */ public float MaxAgentHeight;
        [NMS(Index = 0)]
        /* 0x08 */ public float MaxAgentRadius;
        [NMS(Index = 3)]
        /* 0x0C */ public float MaxStepHeight;
        [NMS(Index = 4)]
        /* 0x10 */ public float MinModifierInclusionSize;
    }
}
