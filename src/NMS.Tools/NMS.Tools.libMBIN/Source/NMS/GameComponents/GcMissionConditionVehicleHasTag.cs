using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE4C67CC0576C2955, NameHash = 0x8CC0B45F)]
    public class GcMissionConditionVehicleHasTag : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x00 */ public NMSString0x10 CustomiserGroupToHighlight;
        [NMS(Index = 1)]
        /* 0x10 */ public GcStaticTag Tag;
        [NMS(Index = 0)]
        /* 0x14 */ public GcVehicleType Type;
    }
}
