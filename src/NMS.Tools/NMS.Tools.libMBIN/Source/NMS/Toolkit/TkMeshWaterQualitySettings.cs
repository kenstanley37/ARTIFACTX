using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xEC8106E627E57657, NameHash = 0xF584C2AF)]
    public class TkMeshWaterQualitySettings : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0x00 */ public TkMeshWaterQualitySettingData[] MeshWaterQualitySettings;
        [NMS(Index = 1, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0xB0 */ public TkMeshWaterReflectionQualitySettingData[] MeshWaterReflectionQualitySettings;
    }
}
