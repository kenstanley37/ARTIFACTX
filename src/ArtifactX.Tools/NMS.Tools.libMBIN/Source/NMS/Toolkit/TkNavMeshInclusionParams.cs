using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xFF34CBB68DDAF678, NameHash = 0xA35B0A28)]
    public class TkNavMeshInclusionParams : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x0 */ public TkNavMeshAreaType AreaType;
        [NMS(Index = 1)]
        /* 0x1 */ public TkNavMeshInclusionType InclusionType;
        // size: 0x3
        public enum NavMeshInclusionHintEnum : byte {
            Auto,
            AlwaysInclude,
            NeverInclude,
        }
        [NMS(Index = 0)]
        /* 0x2 */ public NavMeshInclusionHintEnum NavMeshInclusionHint;
    }
}
