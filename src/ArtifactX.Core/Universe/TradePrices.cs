using System.Text;

namespace ArtifactX.Core.Universe;

/// <summary>
/// What a trade terminal charges for an item, as NMS.exe works it out (build 184271, Ghidra 2026-11-24:
/// 0x1408a0000 -> 0x140eec9e0 -> 0x140eecda0; docs/research/2026-11-24-STATION-TRADE-STOCK.md, "Buy prices").
/// Confirmed the same day on Ialenn's station and the Oodleibe Major trading post (Slot 8, build 185034, Normal
/// Currency Cost): all 35 prices on the terminals match to the unit.
/// </summary>
public static class TradePrices
{
    /// <summary>
    /// The buy price. <paramref name="shopSeed"/>: the system seed (the packed system address) at a space station, the
    /// terminal's raw interaction seed elsewhere. <paramref name="systemSeed"/>: the system seed, which draws the
    /// economy multiplier every trade good of the system shares. <paramref name="improvement"/>: the trade list's
    /// TradeProductsPriceImprovements for the system's wealth. <paramref name="buyMarkup"/>: the save's Currency Cost
    /// TradeBuyPriceMarkupMod. Null when the tables lack the item.
    /// </summary>
    public static int? Buy(TradeStockTables tables, string id, ulong shopSeed, ulong systemSeed, int tradingClass,
        float improvement, float buyMarkup, bool station)
    {
        if (tables.Prices?.TryGetValue(id, out var item) is not true || tables.ClassPrices is not { } classes
            || tradingClass < 0 || tradingClass >= classes.Length) return null;
        float r = Spread(id, shopSeed);
        float basePrice = item.BaseValue * ((item.HighPriceMod - item.LowPriceMod) * r + item.LowPriceMod + 1f);
        bool tradeGood = item.TradeCategory == tables.ClassSells[tradingClass];
        float markup = r * item.BuyMarkupMod + item.BuyBaseMarkup;
        // 0x140eecda0 scales the markup by the Currency Cost factor except in mode 2 (the system's own trade goods).
        if (!tradeGood) markup *= buyMarkup;
        float price = (markup + 1f) * basePrice;
        float multiplier = 1f;
        if (tradeGood)
        {
            // 0x140eec9e0 mode 2: one draw from the Mix64-ed system seed, lerp of the class's selling range, minus the
            // wealth improvement.
            var rng = new UniverseRandom(Start(DiscoveryKeys.Mix64(systemSeed)));
            float u = (float)(rng.NextWord() * 2.3283064365386963e-10);
            var range = classes[tradingClass];
            multiplier = u * (range.MaxSelling - range.MinSelling) + range.MinSelling - improvement;
        }
        multiplier += (1f - item.EconomyInfluence) * (1f - multiplier);
        multiplier = Math.Clamp(multiplier, tables.MinTradingMultiplier, tables.MaxTradingMultiplier);
        price *= multiplier;
        // Stations add SpaceStationMarkup (0x140eec9e0's station flag); no screenshot item had a nonzero markup yet.
        if (station) price *= item.SpaceStationMarkup + 1f;
        return (int)price;
    }

    /// <summary>0x1408a0000: the item's spread, the mean of four words of the stream started (unmixed) from the shop
    /// seed plus the item id's two 8-byte halves.</summary>
    internal static float Spread(string id, ulong shopSeed)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        Encoding.ASCII.GetBytes(id.AsSpan(0, Math.Min(id.Length, 16)), bytes);
        ulong seed = BitConverter.ToUInt64(bytes[..8]) + shopSeed + BitConverter.ToUInt64(bytes[8..]);
        var rng = new UniverseRandom(Start(seed));
        static float F(uint word) => (float)(word * 2.3283064365386963e-10);
        float w1 = F(rng.NextWord()), w2 = F(rng.NextWord()), w3 = F(rng.NextWord()), w4 = F(rng.NextWord());
        return (w2 + w1 + w3 + w4) * 0.25f;
    }

    /// <summary>The stream's start for a 64-bit seed: low word (1 when zero), rol16(low) ^ high ^ low.</summary>
    internal static ulong Start(ulong seed)
    {
        uint low = (uint)seed;
        uint carry = ((low >> 16) | (low << 16)) ^ (uint)(seed >> 32) ^ low;
        return ((ulong)carry << 32) | (low == 0 ? 1u : low);
    }
}
