namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xAE691EDC9CCFC00C, NameHash = 0x9D056F8C)]
    public class GcRecyclableType : NMSTemplate
    {
        // size: 0x5
        public enum RecyclableTypeEnum : uint {
            Scrap,
            Toxic,
            Radioactive,
            Explosive,
            TruckFurnace,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public RecyclableTypeEnum RecyclableType;
    }
}
