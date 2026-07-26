using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x3DEC12191D436E56, NameHash = 0xC3B19E13)]
    public class GcObjectCounterVolumeComponentData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcObjectCounterVolumeType CounterVolumeType;
    }
}
