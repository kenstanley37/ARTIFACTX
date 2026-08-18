namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x1A66BD114CB85379, NameHash = 0xE9C3D15D)]
    public class GcProductCategory : NMSTemplate
    {
        // size: 0xB
        public enum ProductCategoryEnum : uint {
            Component,
            Consumable,
            Tradeable,
            Curiosity,
            BuildingPart,
            Procedural,
            Emote,
            CustomisationPart,
            CreatureEgg,
            Fish,
            ExhibitBone,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public ProductCategoryEnum ProductCategory;
    }
}
