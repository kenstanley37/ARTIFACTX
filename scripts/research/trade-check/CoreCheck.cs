using System.Reflection;
using ArtifactX.Core.Universe;
using libMBIN;
// Runs the production pipeline (GameData CatalogSteps.TradeStock -> Core StationTradeStock.ForSystem) against the captures.
// Captures from older game builds get that build's product table size (226 fewer before 2026-10-28, one more after).
public static class CoreCheck
{
    public static void Run()
    {
        var step = typeof(ArtifactX.GameData.CatalogSteps).GetMethod("TradeStock", BindingFlags.NonPublic | BindingFlags.Static)!;
        var trade = (TradeStockTables)step.Invoke(null, [new Func<string, NMSTemplate>(Paks.Decode)])!;
        Console.WriteLine($"entries {trade.ProductTableEntries}, goods {trade.TradeGoods.Length}, terminals {trade.StandardTypeBTerminals.Length} at "
            + string.Join(" / ", trade.StandardTypeBTerminals.Select(s => string.Join(",", s.Cabinets[0].LocalPosition))));
        var stations = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT");
        var dates = Check.Dates();
        int n = 0, exact = 0, hit = 0, total = 0;
        foreach (var (addrText, seen) in Check.Truth("station"))
        {
            var (code, galaxy) = PortalPositions.Generator(Convert.ToUInt64(addrText, 16));
            bool old = string.CompareOrdinal(dates[addrText], "2026-10-28T12") < 0;
            var stock = StationTradeStock.ForSystem(code, galaxy, false, stations,
                trade with { ProductTableEntries = trade.ProductTableEntries + (old ? -226 : 1) });
            if (stock is null) continue;
            var ids = stock.Select(s => s.Id).ToHashSet();
            int h = seen.Count(ids.Contains);
            n++; hit += h; total += seen.Count; if (h == seen.Count) exact++;
        }
        Console.WriteLine($"== Core: {n} stations, every captured item generated in {exact}; items {hit}/{total}");
    }
}
