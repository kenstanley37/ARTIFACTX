using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x9400CC71E0B90451, NameHash = 0xDF397906)]
    public class TkNavModifierComponentData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public TkNavMeshInclusionParams NavMeshInclusion;
    }
}
