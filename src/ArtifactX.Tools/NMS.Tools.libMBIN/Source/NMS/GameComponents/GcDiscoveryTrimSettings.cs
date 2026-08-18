using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x64FBA56934146728, NameHash = 0x5CDDC22A)]
    public class GcDiscoveryTrimSettings : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x000 */ public GcBaseSearchFilter BaseSearchFilter;
        [NMS(Index = 2, Size = 0x8, EnumType = typeof(GcDiscoveryTrimScoringCategory.DiscoveryTrimScoringCategoryEnum))]
        /* 0x0C0 */ public GcDiscoveryTrimScoringRules[] DiscoveryTrimScoringRules;
        [NMS(Index = 3, Size = 0x8, EnumType = typeof(GcDiscoveryTrimScoringCategory.DiscoveryTrimScoringCategoryEnum))]
        /* 0x120 */ public float[] DiscoveryTrimScoringWeights;
        [NMS(Index = 0, Size = 0x4, EnumType = typeof(GcDiscoveryTrimGroup.DiscoveryTrimGroupEnum))]
        /* 0x140 */ public int[] DiscoveryTrimGroupMaxCounts;
    }
}
