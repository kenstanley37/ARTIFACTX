using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x2E35074F8808F5B4, NameHash = 0xAFEEEF4C)]
    public class GcCustomisationPreset : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x00 */ public GcCharacterCustomisationData Data;
        [NMS(Index = 0)]
        /* 0x58 */ public NMSString0x10 Name;
        [NMS(Index = 1)]
        /* 0x68 */ public bool CanBeSeasonalStarter;
    }
}
