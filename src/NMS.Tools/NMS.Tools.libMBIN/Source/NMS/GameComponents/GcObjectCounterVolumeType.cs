namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x58101CAEFA75A59D, NameHash = 0x5E967809)]
    public class GcObjectCounterVolumeType : NMSTemplate
    {
        // size: 0x3
        public enum CounterVolumeEnum : uint {
            Vehicle,
            ScrapYard,
            ScrapYardFullBounds,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public CounterVolumeEnum CounterVolume;
    }
}
