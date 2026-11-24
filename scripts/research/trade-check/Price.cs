using System.Numerics;
using System.Text;
using ArtifactX.Core.Universe;
using libMBIN.NMS.GameComponents;
// Price research: per item, the shop part of the buy price (0x1408a0000 -> 0x140eecda0) and the multiplier the
// game's shown price implies (price / shop part), for the two Ialenn terminals screenshotted 2026-11-24.
public static class Price
{
    record Rec(int Base, float BuyBase, float BuyMod, float High, float Low, float Station, float Influence);
    static Dictionary<string, Rec> recs = new();
    static void Load()
    {
        var reality = (GcRealityManagerData)Paks.Decode("METADATA/REALITY/DEFAULTREALITY.MBIN");
        foreach (var tab in reality.ProductTables) if (Paks.Decode(Path.ChangeExtension(Paks.Norm(tab.Value), ".MBIN")) is GcProductTable pt)
            foreach (var p in pt.Table) recs.TryAdd(p.ID.Value, new(p.BaseValue, p.Cost.BuyBaseMarkup, p.Cost.BuyMarkupMod, p.Cost.HighPriceMod, p.Cost.LowPriceMod, p.Cost.SpaceStationMarkup, p.EconomyInfluenceMultiplier));
        foreach (var s in ((GcSubstanceTable)Paks.Decode(Paks.Norm(reality.SubstanceTable.Value))).Table)
            recs.TryAdd(s.ID.Value, new(s.BaseValue, s.Cost.BuyBaseMarkup, s.Cost.BuyMarkupMod, s.Cost.HighPriceMod, s.Cost.LowPriceMod, s.Cost.SpaceStationMarkup, s.EconomyInfluenceMultiplier));
    }
    public static float R(string id, ulong shopSeed)
    {
        var b = new byte[16]; Encoding.ASCII.GetBytes(id).CopyTo(b, 0);
        ulong v = BitConverter.ToUInt64(b, 0) + shopSeed + BitConverter.ToUInt64(b, 8);
        uint lo = (uint)v; ulong st = ((ulong)(((lo >> 16) | (lo << 16)) ^ (uint)(v >> 32) ^ lo) << 32) | (lo == 0 ? 1u : lo);
        uint W() { st = (st & 0xFFFFFFFF) * 0x5A76F899UL + (st >> 32); return (uint)st; }
        uint w1 = W(), w2 = W(), w3 = W(), w4 = W();
        float F(uint w) => (float)(w * 2.3283064365386963e-10);
        return (F(w2) + F(w1) + F(w3) + F(w4)) * 0.25f;
    }
    public static void Show(string label, ulong shopSeed, (string Id, int Price)[] items)
    {
        if (recs.Count == 0) Load();
        Console.WriteLine($"== {label} shop seed {shopSeed:X16}");
        foreach (var (id, price) in items)
        {
            var q = recs[id]; float r = R(id, shopSeed);
            float baseP = q.Base * ((q.High - q.Low) * r + q.Low + 1f);
            float markup = r * q.BuyMod + q.BuyBase;
            float shop = (markup + 1f) * baseP;
            float eff = price / shop;
            float m = q.Influence == 0 ? float.NaN : (eff - (1 - q.Influence)) / q.Influence;
            float effS = price / (shop * (1 + q.Station));
            float mS = q.Influence == 0 ? float.NaN : (effS - (1 - q.Influence)) / q.Influence;
            Console.WriteLine($"  DATA {id} {price} {q.Base} {r:R} {q.BuyBase:R} {q.BuyMod:R} {q.High:R} {q.Low:R} {q.Influence:R}");
            Console.WriteLine($"  {id,-12} {price,7} base {q.Base,6} r {r:F4} shop {shop,10:F1} eff {eff:F4} M {m:F4} | +station {q.Station:F2}: eff {effS:F4} M {mS:F4}  infl {q.Influence:F2}");
        }
    }
    static void Full(string label, ulong itemSeed, bool station, ulong systemSeed, (string Id, int Price)[] items, bool stationMarkup)
    {
        var reality = (GcRealityManagerData)Paks.Decode("METADATA/REALITY/DEFAULTREALITY.MBIN");
        var classes = (GcTradingClassTable)Paks.Decode("METADATA/REALITY/TABLES/TRADINGCLASSDATATABLE.MBIN");
        var prods = new Dictionary<string, GcProductData>(); foreach (var tab in reality.ProductTables) if (Paks.Decode(Path.ChangeExtension(Paks.Norm(tab.Value), ".MBIN")) is GcProductTable pt) foreach (var p in pt.Table) prods.TryAdd(p.ID.Value, p);
        var subs = ((GcSubstanceTable)Paks.Decode(Paks.Norm(reality.SubstanceTable.Value))).Table.ToDictionary(x => x.ID.Value);
        var td = station ? reality.TradeSettings.SpaceStation : reality.TradeSettings.Shop;
        int exact = 0;
        foreach (var (id, price) in items)
        {
            prods.TryGetValue(id, out var p); subs.TryGetValue(id, out var sb);
            bool tg = (p?.TradeCategory.TradeCategory ?? sb!.TradeCategory.TradeCategory) == classes.TradingClassesData[4].Sells.TradeCategory;
            int pred = Price2.Predict(p, sb, id, itemSeed, station, systemSeed, classes, 4, td.TradeProductsPriceImprovements[2], tg, 1.5f, stationMarkup);
            if (pred == price) exact++;
            Console.WriteLine($"  {label} {id,-11} game {price,6} predicted {pred,6} {(pred == price ? "OK" : $"off {pred - price:+0;-0}")}");
        }
        Console.WriteLine($"== {label}: {exact}/{items.Length} exact (station markup {stationMarkup})");
    }

    public static void Run(string save)
    {
        // Station terminal: same placement as StationTradeStock.ForSystem, raw (unmixed) position hash.
        ulong system = 0x3F5D8FCFFB005;
        var (code, galaxy) = PortalPositions.Generator(system);
        var stations = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT");
        var pose = StationPoseGeneration.GenerateForSystem(code, galaxy, stations.SolarLayout)!;
        var world = StationCabinetLocations.ToWorld(pose, [-5.0109487f, 1.875f, -942.5f])!.Value;
        var layout = SolarLayoutGeneration.ForSystem(code, galaxy, stations.SolarLayout, true)!;
        Console.WriteLine($"system seed (layout address) {layout.Address:X}");
        (string, int)[] stationItems = [("TRA_ALLOY4", 28568), ("TRA_ALLOY3", 13630), ("TRA_ALLOY5", 49569), ("TRA_ALLOY2", 4891), ("TRA_ALLOY1", 767), ("CAVE1", 100), ("MIRROR", 16711), ("COMPUTER", 11794), ("LAND1", 19), ("GRENFUEL1", 7677), ("MICROCHIP", 27500), ("CASING", 1084), ("TECH_COMP", 73015), ("RADIO1", 529), ("WATER2", 279), ("HOT1", 86), ("WATER1", 137)];
        (string, int)[] postItems = [("TRA_ALLOY4", 23786), ("TRA_ALLOY3", 10199), ("TRA_ALLOY5", 43004), ("TRA_ALLOY2", 2926), ("TRA_ALLOY1", 371), ("CAVE1", 108), ("MAGNET", 17260), ("ALLOY1", 35038), ("ALLOY2", 47565), ("LAND1", 19), ("GRENFUEL1", 7703), ("LAUNCHFUEL", 68324), ("CASING", 1030), ("MICROCHIP", 27500), ("TOXIC1", 85), ("DUSTY1", 86), ("ASTEROID3", 703), ("WATER2", 280)];
        foreach (bool sm in new[] { false, true }) Full("station", layout.Address, true, layout.Address, stationItems, sm);
        Full("post", PostSeed(), false, layout.Address, postItems, false);
        return;
        Show("Ialenn station", MultiToolSiteGeneration.SeedForPosition(world), [("TRA_ALLOY4", 28568), ("TRA_ALLOY3", 13630), ("TRA_ALLOY5", 49569), ("TRA_ALLOY2", 4891), ("TRA_ALLOY1", 767), ("CAVE1", 100), ("MIRROR", 16711), ("COMPUTER", 11794), ("LAND1", 19), ("GRENFUEL1", 7677), ("MICROCHIP", 27500), ("CASING", 1084), ("TECH_COMP", 73015), ("RADIO1", 529), ("WATER2", 279), ("HOT1", 86), ("WATER1", 137)]);
        Show("Oodleibe Major post", PostSeed(),
            [("TRA_ALLOY4", 23786), ("TRA_ALLOY3", 10199), ("TRA_ALLOY5", 43004), ("TRA_ALLOY2", 2926), ("TRA_ALLOY1", 371), ("CAVE1", 108), ("MAGNET", 17260), ("ALLOY1", 35038), ("ALLOY2", 47565), ("LAND1", 19), ("GRENFUEL1", 7703), ("LAUNCHFUEL", 68324), ("CASING", 1030), ("MICROCHIP", 27500), ("TOXIC1", 85), ("DUSTY1", 86), ("ASTEROID3", 703), ("WATER2", 280)]);
    }
    static ulong PostSeed()
    {
        ulong system = 0x3F5D8FCFFB005; int index = 3;
        var solar = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT").SolarLayout;
        var centres = PortalPositions.Centres(system, solar)!;
        var ctx = CrashedShipSites.Prepare(system | ((ulong)index << 52), centres[index - 1], Check.Row<BiomeTables>("BIOMETABLES"),
            Check.Row<PlanetProfileTables>("PLANETPROFILE"), Check.Row<BuildingClassPlacement[]>("BUILDINGPLACEMENT"),
            Check.Row<BuildingOverrideRule[]>("BUILDINGOVERRIDES"), solar, null, null, null)!;
        var site = PlanetBuildingSites.SitesNear(CrashedShipSites.Direction(16.61, -152.18), ctx.Divisions, ctx.Radius, ctx.SectorSeed, ctx.Slots, k => k == 6, 1)[0];
        return Posts.TerminalSeed(ctx, site, 1);
    }
}
