using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x4809B12C9D1C034A, NameHash = 0xDA4AB69C)]
    public class GcPlanetTerrainColour : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public TkPaletteTexture Palette;
        [NMS(Index = 1)]
        /* 0xC */ public int Index;
    }
}
