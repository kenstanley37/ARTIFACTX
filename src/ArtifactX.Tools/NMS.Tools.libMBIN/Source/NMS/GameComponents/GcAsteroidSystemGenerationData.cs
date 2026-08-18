using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x51715613B03329C7, NameHash = 0x95D309FC)]
    public class GcAsteroidSystemGenerationData : NMSTemplate
    {
        [NMS(Index = 0, MxmlName = "Common Asteroid Data")]
        /* 0x00 */ public GcAsteroidGenerationData CommonAsteroidData;
        [NMS(Index = 2, MxmlName = "Large Asteroid Data")]
        /* 0x24 */ public GcAsteroidGenerationData LargeAsteroidData;
        [NMS(Index = 3, MxmlName = "Rare Asteroid Data")]
        /* 0x48 */ public GcAsteroidGenerationData RareAsteroidData;
        [NMS(Index = 1, MxmlName = "Ring Asteroid Data")]
        /* 0x6C */ public GcAsteroidGenerationData RingAsteroidData;
    }
}
