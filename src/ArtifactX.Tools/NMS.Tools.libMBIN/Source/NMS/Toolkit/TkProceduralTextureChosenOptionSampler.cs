using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x7C0289DE0D40D19E, NameHash = 0xEDB31590)]
    public class TkProceduralTextureChosenOptionSampler : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<TkProceduralTextureChosenOption> Options;
    }
}
