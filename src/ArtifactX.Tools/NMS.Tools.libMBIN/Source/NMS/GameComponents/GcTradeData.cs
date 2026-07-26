using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x6F6E9F15CDA4A9B9, NameHash = 0x245A8607)]
    public class GcTradeData : NMSTemplate
    {
        [NMS(Index = 4)]
        /* 0x00 */ public List<NMSString0x10> AlwaysConsideredBarterProducts;
        [NMS(Index = 0)]
        /* 0x10 */ public List<NMSString0x10> AlwaysPresentProducts;
        [NMS(Index = 1)]
        /* 0x20 */ public List<NMSString0x10> AlwaysPresentSubstances;
        [NMS(Index = 2)]
        /* 0x30 */ public List<NMSString0x10> OptionalProducts;
        [NMS(Index = 3)]
        /* 0x40 */ public List<NMSString0x10> OptionalSubstances;
        [NMS(Index = 18, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0x50 */ public int[] MaxAmountOfProductAvailable;
        [NMS(Index = 20, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0x60 */ public int[] MaxAmountOfSubstanceAvailable;
        [NMS(Index = 22, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0x70 */ public int[] MaxExtraSystemProducts;
        [NMS(Index = 17, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0x80 */ public int[] MinAmountOfProductAvailable;
        [NMS(Index = 19, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0x90 */ public int[] MinAmountOfSubstanceAvailable;
        [NMS(Index = 21, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0xA0 */ public int[] MinExtraSystemProducts;
        [NMS(Index = 23, Size = 0x4, EnumType = typeof(GcWealthClass.WealthClassEnum))]
        /* 0xB0 */ public float[] TradeProductsPriceImprovements;
        [NMS(Index = 7)]
        /* 0xC0 */ public float BarterItemPreferenceFloor;
        [NMS(Index = 6)]
        /* 0xC4 */ public float BarterPriceMultiplier;
        [NMS(Index = 12)]
        /* 0xC8 */ public float BuyPriceDecreaseGreenThreshold;
        [NMS(Index = 11)]
        /* 0xCC */ public float BuyPriceIncreaseRedThreshold;
        [NMS(Index = 9)]
        /* 0xD0 */ public int MaxItemsForSale;
        [NMS(Index = 8)]
        /* 0xD4 */ public int MinItemsForSale;
        [NMS(Index = 10)]
        /* 0xD8 */ public float PercentageOfItemsAreProducts;
        [NMS(Index = 14)]
        /* 0xDC */ public float SellPriceDecreaseRedThreshold;
        [NMS(Index = 13)]
        /* 0xE0 */ public float SellPriceIncreaseGreenThreshold;
        [NMS(Index = 5)]
        /* 0xE4 */ public TkCurveType BarterAcceptanceCurve;
        [NMS(Index = 15)]
        /* 0xE5 */ public bool ShowSeasonRewards;
        [NMS(Index = 16)]
        /* 0xE6 */ public bool UseBarterForBuy;
    }
}
