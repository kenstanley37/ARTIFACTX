namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x9B16CF5706E43D7B, NameHash = 0x1B307F85)]
    public class GcAsteroidGenerationData : NMSTemplate
    {
        [NMS(Index = 4, MxmlName = "Noise Range")]
        /* 0x00 */ public Vector2f NoiseRange;
        [NMS(Index = 1, MxmlName = "Scale Variance")]
        /* 0x08 */ public Vector2f ScaleVariance;
        [NMS(Index = 5, MxmlName = "Fade Range")]
        /* 0x10 */ public float FadeRange;
        [NMS(Index = 2)]
        /* 0x14 */ public int Health;
        [NMS(Index = 6, MxmlName = "Noise Scale")]
        /* 0x18 */ public float NoiseScale;
        [NMS(Index = 0)]
        /* 0x1C */ public float Scale;
        [NMS(Index = 3)]
        /* 0x20 */ public float Spacing;
    }
}
