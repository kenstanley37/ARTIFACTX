namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xB0EC612C914FE81A, NameHash = 0x88C80333)]
    public class GcStaticTag : NMSTemplate
    {
        // size: 0xA
        public enum StaticTagEnum : uint {
            None = 0x0,
            GravityLaserGrabbable = 0x1,
            TruckCargoObject = 0x2,
            TruckCargoSpecial = 0x4,
            TruckFlatbed = 0x8,
            ScrapyardFurnace = 0x10,
            ScrapyardToxBin = 0x20,
            ScrapyardRadBin = 0x40,
            ScrapyardExpBin = 0x80,
            TruckCargoSwarm = 0x100,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public StaticTagEnum StaticTag;
    }
}
