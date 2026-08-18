using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE7E919F3E6A320B1, NameHash = 0x4550DAE9)]
    public class GcRecyclableComponentData : NMSTemplate
    {
        [NMS(Index = 0, Size = 0x5, EnumType = typeof(GcRecyclableType.RecyclableTypeEnum))]
        /* 0x0 */ public GcRecyclableReward[] RecyclerReward;
    }
}
