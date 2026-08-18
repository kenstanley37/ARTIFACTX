using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE608EE95D0F82318, NameHash = 0xFC3967D1)]
    public class GcShipOwnershipComponentData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcSpaceshipComponentData Data;
    }
}
