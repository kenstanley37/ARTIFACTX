using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xAEAA4D62B55D84D4, NameHash = 0xCF6D9992)]
    public class TkAnimationNotifies : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public List<TkAnimationNotify> Notifies;
    }
}
