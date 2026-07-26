using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x5EAFF55DED6C7EA8, NameHash = 0xE21986C9)]
    public class GcCustomisationUIData : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x1A, EnumType = typeof(GcCustomisationComponentData.CustomisationDataTypeEnum))]
        /* 0x0 */ public GcCustomisationUI[] CustomisationUIData;
    }
}
