using ArtifactX.Core.Universe;
using libMBIN.NMS.GameComponents;
// Full buy-price model (0x1408a0000 -> 0x140eec9e0 -> 0x140eecda0) vs the 35 screenshot prices.
public static class Price2
{
    public static int Predict(GcProductData? p, GcRealitySubstanceData? s, string id, ulong itemSeed, bool station, ulong systemSeed,
        GcTradingClassTable classes, int tradingClass, float improvement, bool tradeGood, float buyFactor, bool stationMarkup)
    {
        int baseValue = p?.BaseValue ?? s!.BaseValue; var c = p?.Cost ?? s!.Cost; float infl = p?.EconomyInfluenceMultiplier ?? s!.EconomyInfluenceMultiplier;
        float r = Price.R(id, itemSeed);
        float basePrice = baseValue * ((c.HighPriceMod - c.LowPriceMod) * r + c.LowPriceMod + 1f);
        float markup = r * c.BuyMarkupMod + c.BuyBaseMarkup;
        if (!tradeGood) markup *= buyFactor;
        float price = (markup + 1f) * basePrice;
        float mult = 1f;
        if (tradeGood)
        {
            ulong m = DiscoveryKeys.Mix64(systemSeed); uint lo = (uint)m; ulong st = ((ulong)(((lo >> 16) | (lo << 16)) ^ (uint)(m >> 32) ^ lo) << 32) | (lo == 0 ? 1u : lo);
            st = (st & 0xFFFFFFFF) * 0x5A76F899UL + (st >> 32);
            float u = (float)((double)(uint)st * 2.3283064365386963e-10);
            var k = classes.TradingClassesData[tradingClass];
            mult = u * (k.MaxSellingPriceMultiplier - k.MinSellingPriceMultiplier) + k.MinSellingPriceMultiplier - improvement;
        }
        mult = mult + (1f - infl) * (1f - mult);
        mult = Math.Clamp(mult, classes.MinTradingMultiplier, classes.MaxTradingMultiplier);
        price *= mult;
        if (station && stationMarkup) price *= c.SpaceStationMarkup + 1f;
        return (int)price;
    }
}
