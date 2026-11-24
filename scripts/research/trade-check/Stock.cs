using libMBIN.NMS.GameComponents;
// Port of NMS.exe 0x14087b4d0 (build 184271): a trade interaction's stock from its 64-bit seed.
// Write-up: docs/research/2026-11-24-STATION-TRADE-STOCK.md
public sealed class Tables
{
    public GcRealityManagerData Reality;
    public List<GcProductData>[] ProductTables;      // Main, BaseParts, ModularCustomisation, in order
    public Dictionary<string, GcProductData> Products = new(StringComparer.Ordinal);
    public Dictionary<string, GcRealitySubstanceData> Substances = new(StringComparer.Ordinal);
    public HashSet<string> NeverSellable, NeverOffered;
    public GcTradingClassTable Classes;
    public static Tables Load()
    {
        var t = new Tables();
        t.Reality = (GcRealityManagerData)Paks.Decode("METADATA/REALITY/DEFAULTREALITY.MBIN");
        t.ProductTables = t.Reality.ProductTables.Select(f =>
        {
            var p = Paks.Decode(f.Value) ?? Paks.Decode(f.Value.Replace(".TXT", ".MBIN", StringComparison.OrdinalIgnoreCase));
            Console.Error.WriteLine($"{f.Value}: {(p as GcProductTable)?.Table?.Count}");
            return (p as GcProductTable)?.Table ?? new List<GcProductData>();
        }).ToArray();
        foreach (var tab in t.ProductTables) foreach (var p in tab) t.Products.TryAdd(p.ID.Value, p);
        foreach (var s in ((GcSubstanceTable)Paks.Decode(t.Reality.SubstanceTable.Value)).Table) t.Substances.TryAdd(s.ID.Value, s);
        t.NeverSellable = t.Reality.NeverSellableItems.Select(x => x.Value).ToHashSet();
        t.NeverOffered = t.Reality.NeverOfferedForSale.Select(x => x.Value).ToHashSet();
        t.Classes = (GcTradingClassTable)Paks.Decode("METADATA/REALITY/TABLES/TRADINGCLASSDATATABLE.MBIN");
        return t;
    }
}

public sealed class Rng
{
    uint s0, s1;
    public int Draws;
    public Rng(ulong seed) { uint lo = (uint)seed; s0 = lo == 0 ? 1 : lo; s1 = ((lo >> 16) | (lo << 16)) ^ (uint)(seed >> 32) ^ lo; }
    public Rng Clone() => (Rng)MemberwiseClone();
    public uint Next() { ulong v = (ulong)s0 * 0x5A76F899UL + s1; s0 = (uint)v; s1 = (uint)(v >> 32); Draws++; return s0; }
    public int Range(int min, int max) => (int)(((long)(max - min + 1) * Next()) >> 32) + min;
    public int Index(int n) => (int)(((ulong)Next() * (ulong)n) >> 32);
}

public static class Stock
{
    // Research knobs: DrawOverride replays the trade-goods loop with a fixed draw count (other game builds had other
    // product table sizes); TablesUsed limits the loop to the first N product tables.
    public static int DrawOverride = -1, TablesUsed = 3;
    public static int LastTradeDraws, ProductsBefore;
    public static ulong Mix64(ulong x) { x = (x ^ (x >> 33)) * 0x64DD81482CBD31D7UL; x = (x ^ (x >> 33)) * 0xE36AA5C613612997UL; return x ^ (x >> 33); }

    /// <param name="shop">true for Shop/PirateShop interactions (station trade terminal).</param>
    public static List<string> Generate(Tables t, GcTradeData td, ulong seed, int wealth, int tradingClass, bool pirate, bool shop = true)
    {
        var rng = new Rng(seed);
        var stock = new List<string>(); var have = new HashSet<string>();
        bool Offered(string id) => !t.NeverOffered.Contains(id); // + difficulty / season lists (empty in normal play - TODO)
        void Add(string id) { stock.Add(id); have.Add(id); }

        int totalP = td.AlwaysPresentProducts.Count + td.OptionalProducts.Count;
        int totalS = td.AlwaysPresentSubstances.Count + td.OptionalSubstances.Count;
        int count = rng.Range(td.MinItemsForSale, td.MaxItemsForSale);
        int products = Math.Min(Math.Max((int)(count * td.PercentageOfItemsAreProducts), 0), totalP);
        int subs = Math.Min(Math.Max(count - products, 0), totalS);

        foreach (var id in td.AlwaysPresentProducts.Select(x => x.Value))
        {
            if (!t.Products.TryGetValue(id, out var p)) continue;
            // CHART_SETTLE skipped while a feature flag (0x1403f43b0) is off - not in station lists.
            rng.Next();
            if (!t.NeverSellable.Contains(id) && Offered(id) && !have.Contains(id)) Add(id);
            products--;
        }
        foreach (var id in td.AlwaysPresentSubstances.Select(x => x.Value))
        {
            if (!t.Substances.ContainsKey(id)) continue;
            rng.Next();
            if (!t.NeverSellable.Contains(id) && Offered(id) && !have.Contains(id)) Add(id);
            subs--;
        }
        int[] Shuffle(int n)
        {
            var a = Enumerable.Range(0, n).ToArray();
            for (int i = n - 1; i > 0; i--) { int j = rng.Index(i + 1); (a[i], a[j]) = (a[j], a[i]); }
            return a;
        }
        var order = Shuffle(td.OptionalProducts.Count);
        for (int k = 0; k < products && td.OptionalProducts.Count > 0; k++)
        {
            string id = td.OptionalProducts[order[k]].Value;
            if (!t.Products.TryGetValue(id, out var p) || have.Contains(id)) continue;
            rng.Next();
            if ((int)p.Category.SubstanceCategory == 8) continue;
            if (t.NeverSellable.Contains(id) || !Offered(id)) continue;
            Add(id);
        }
        if (shop && pirate)
        {
            // TODO PirateStationExtraProds (needs the string check at engine+0x57aa6e and DAT_146f8249c).
        }
        var sells = t.Classes.TradingClassesData[tradingClass].Sells.TradeCategory;
        int extra = 0;
        if (shop && !pirate) extra = rng.Range(td.MinExtraSystemProducts[wealth], td.MaxExtraSystemProducts[wealth]);
        int before = rng.Draws; var snap = rng.Clone(); ProductsBefore = stock.Count(id => t.Products.ContainsKey(id));
        foreach (var table in t.ProductTables.Take(TablesUsed))
            foreach (var p in table)
            {
                string id = p.ID.Value;
                if (have.Contains(id)) continue;
                rng.Next();
                if (extra > 0 && (int)p.Category.SubstanceCategory != 8 && !t.NeverSellable.Contains(id) && Offered(id)
                    && p.TradeCategory.TradeCategory == sells) { Add(id); extra--; }
            }
        LastTradeDraws = rng.Draws - before;
        if (DrawOverride >= 0) { rng = snap; for (int i = 0; i < DrawOverride; i++) rng.Next(); }
        order = Shuffle(td.OptionalSubstances.Count);
        for (int k = 0; k < subs && td.OptionalSubstances.Count > 0; k++)
        {
            string id = td.OptionalSubstances[order[k]].Value;
            if (!t.Substances.ContainsKey(id) || have.Contains(id)) continue;
            rng.Next();
            if (t.NeverSellable.Contains(id) || !Offered(id)) continue;
            Add(id);
        }
        return stock;
    }
}
