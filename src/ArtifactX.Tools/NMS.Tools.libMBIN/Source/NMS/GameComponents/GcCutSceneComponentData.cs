using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x136F52440B6DECD0, NameHash = 0x7F665957)]
    public class GcCutSceneComponentData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcCutSceneData CutSceneData;
    }
}
