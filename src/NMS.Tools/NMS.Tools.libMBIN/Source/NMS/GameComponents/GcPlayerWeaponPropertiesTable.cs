using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x68660E0C85EDFAEA, NameHash = 0x3CBA48CA)]
    public class GcPlayerWeaponPropertiesTable : NMSTemplate
    {
        [NMS(Index = 1, Size = 0x15, EnumType = typeof(GcPlayerWeapons.WeaponModeEnum))]
        /* 0x0000 */ public GcPlayerWeaponPropertiesData[] PropertiesData;
        [NMS(Index = 0)]
        /* 0x1B90 */ public GcCamouflageData CamouflageData;
    }
}
