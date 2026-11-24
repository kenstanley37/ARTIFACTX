public static class Stats
{
    public static void Run()
    {
        var t = Tables.Load();
        var all = t.ProductTables.SelectMany((tab, i) => tab.Select(p => (i, p))).ToList();
        Console.WriteLine($"entries {all.Count}, distinct ids {all.Select(x => x.p.ID.Value).Distinct().Count()}");
        for (int i = 0; i < 3; i++)
        {
            var tab = t.ProductTables[i];
            Console.WriteLine($"table {i}: {tab.Count}, distinct {tab.Select(p => p.ID.Value).Distinct().Count()}, empty id {tab.Count(p => string.IsNullOrEmpty(p.ID.Value))}");
            foreach (var g in tab.GroupBy(p => p.Category.SubstanceCategory)) Console.WriteLine($"   cat {g.Key}: {g.Count()}");
            foreach (var g in tab.GroupBy(p => p.TradeCategory.TradeCategory)) Console.WriteLine($"   trade {g.Key}: {g.Count()}");
        }
    }
}
