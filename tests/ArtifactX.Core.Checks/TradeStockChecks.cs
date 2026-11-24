using ArtifactX.Core.Universe;

/// <summary>StationTradeStock's rules on made-up tables (the real tables come from the player's game data; the
/// generator was measured against 170 captured station terminals in scripts/research/trade-check).</summary>
internal static class TradeStockChecks
{
    public static void Check()
    {
        static TradeListItem[] Items(string prefix, int n) => Enumerable.Range(1, n).Select(i => new TradeListItem($"{prefix}{i}", true)).ToArray();
        var list = new TradeListData(Items("AP", 3), Items("AS", 2), Items("OP", 8), Items("OS", 10),
            MinItems: 12, MaxItems: 14, ProductShare: 0.5f, MinTradeGoods: [1, 2, 3, 0], MaxTradeGoods: [1, 2, 3, 0]);
        var goods = new[] { new TradeGoodEntry("G_A1", 0), new TradeGoodEntry("G_B1", 1), new TradeGoodEntry("G_A2", 0), new TradeGoodEntry("G_A3", 0) };
        var tables = new TradeStockTables(list, 500, goods, ["AP1", "AP2", "AP3"], NeverSellable: ["AS2"], NeverOffered: ["OS4"],
            ClassSells: [0, 1], StandardTypeBTerminals: []);

        for (ulong seed = 1; seed <= 200; seed++)
        {
            var stock = StationTradeStock.Generate(tables, list, seed * 0x9E3779B97F4A7C15UL, wealth: 1, tradingClass: 0);
            var ids = stock.Select(s => s.Id).ToList();
            Require(ids.Distinct().Count() == ids.Count, "an item is listed twice");
            Require(new[] { "AP1", "AP2", "AP3", "AS1" }.All(ids.Contains), "a fixed item is missing");
            Require(!ids.Contains("AS2") && !ids.Contains("OS4"), "a never-sold item is on sale");
            // Wealth 1 takes exactly two goods: the first two of class 0's category, in table order.
            Require(stock.Where(s => s.Kind == TradeStockKind.TradeGood).Select(s => s.Id).SequenceEqual(["G_A1", "G_A2"]), "wrong trade goods");
            int rest = stock.Count(s => s.Kind != TradeStockKind.TradeGood);
            Require(rest is >= 10 and <= 14, $"{rest} items besides trade goods (12 to 14 places, at most 2 of them never sold)");
            Require(ids.SequenceEqual(StationTradeStock.Generate(tables, list, seed * 0x9E3779B97F4A7C15UL, 1, 0).Select(s => s.Id)), "not repeatable");
        }
        Console.WriteLine("PASS: station trade stock rules (200 seeds).");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Trade stock: " + message);
    }
}
