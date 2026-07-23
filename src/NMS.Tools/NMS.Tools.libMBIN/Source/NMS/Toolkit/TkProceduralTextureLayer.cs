using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x7911C22EB85521C8, NameHash = 0x835BEB69)]
    public class TkProceduralTextureLayer : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x00 */ public NMSString0x10 Group;
        [NMS(Index = 4)]
        /* 0x10 */ public NMSString0x10 LinkedLayer;
        [NMS(Index = 0)]
        /* 0x20 */ public NMSString0x10 Name;
        [NMS(Index = 5)]
        /* 0x30 */ public List<TkProceduralTexture> Textures;
        [NMS(Index = 1)]
        /* 0x40 */ public float Probability;
        [NMS(Index = 3)]
        /* 0x44 */ public bool SelectToMatchBase;
    }
}
