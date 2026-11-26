using System.Numerics;
using System.Reflection;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
// Planet trading posts (building class 6, Outpost): every Outpost site of a captured planet, the terminal's world
// position (site raised to the Far-region spawn height, building axes from the site key, race scene's terminal offset),
// its stock from TradeSettings.Shop; reports the sites whose stock holds every captured item.
public static class Posts
{
    // Terminal_Tradingpost in TRADINGPOST{TRADER,WARRIOR,SCIENTIFIC}.SCENE (Gek, Vy'keen, Korvax), from `outpost`.
    static readonly Vector3[] Terminal = [new(37.399998f, 17.795189f, -3.905937f), new(37.399998f, 17.795189f, -3.287193f), new(37.399998f, 17.795189f, -3.779248f)];

    // Same lookups as CatalogSteps.TradeStock: an id the product or substance table lacks is passed over without a draw.
    public static TradeListData ShopList(TradeStockTables t, GcTradeData d)
    {
        var reality = (GcRealityManagerData)Paks.Decode("METADATA/REALITY/DEFAULTREALITY.MBIN");
        var products = new Dictionary<string, GcProductData>();
        foreach (var tab in reality.ProductTables) if (Paks.Decode(Path.ChangeExtension(Paks.Norm(tab.Value), ".MBIN")) is GcProductTable pt) foreach (var p in pt.Table) products.TryAdd(p.ID.Value, p);
        var substances = ((GcSubstanceTable)Paks.Decode(Paks.Norm(reality.SubstanceTable.Value))).Table.Select(x => x.ID.Value).ToHashSet();
        TradeListItem P(libMBIN.NMS.NMSString0x10 id) => new(id.Value, products.TryGetValue(id.Value, out var p),
            p?.Category.SubstanceCategory == GcRealitySubstanceCategory.SubstanceCategoryEnum.BuildingPart);
        TradeListItem S(libMBIN.NMS.NMSString0x10 id) => new(id.Value, substances.Contains(id.Value));
        return new TradeListData(d.AlwaysPresentProducts.Select(P).ToArray(), d.AlwaysPresentSubstances.Select(S).ToArray(),
            d.OptionalProducts.Select(P).ToArray(), d.OptionalSubstances.Select(S).ToArray(), d.MinItemsForSale, d.MaxItemsForSale,
            d.PercentageOfItemsAreProducts, d.MinExtraSystemProducts.ToArray(), d.MaxExtraSystemProducts.ToArray());
    }

    static (double X, double Y, double Z) N((double X, double Y, double Z) v) { double l = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z); return (v.X / l, v.Y / l, v.Z / l); }
    static (double X, double Y, double Z) X((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static ulong TerminalSeed(PlanetSiteContext ctx, BuildingSite site, int race, double yawMode = 0)
    {
        var sp = ((double)site.Point.X, (double)site.Point.Y, (double)site.Point.Z);
        var (_, spawn) = PlanetShopCabinetGenerator.SpawnHeight(ctx.Terrain!, ctx.Radius, sp, sp);
        var (_, f, _) = BuildingClusterPlacement.Facing(sp, site.Key);
        var up = N(sp);
        var side = N(X(up, f));
        var fwd = X(side, up);
        var l = Terminal[race];
        double r = ctx.Radius + spawn;
        var c = ctx.Centre;
        var w = new Vector3(
            (float)(up.X * r + side.X * l.X + up.X * l.Y + fwd.X * l.Z + c.X),
            (float)(up.Y * r + side.Y * l.X + up.Y * l.Y + fwd.Y * l.Z + c.Y),
            (float)(up.Z * r + side.Z * l.X + up.Z * l.Y + fwd.Z * l.Z + c.Z));
        return MultiToolSiteGeneration.SeedForPosition(w);
    }

    public static void Run()
    {
        var step = typeof(ArtifactX.GameData.CatalogSteps).GetMethod("TradeStock", BindingFlags.NonPublic | BindingFlags.Static)!;
        var trade = (TradeStockTables)step.Invoke(null, [new Func<string, NMSTemplate>(Paks.Decode)])!;
        var reality = (GcRealityManagerData)Paks.Decode("METADATA/REALITY/DEFAULTREALITY.MBIN");
        var shop = ShopList(trade, reality.TradeSettings.Shop);
        var allProducts = new HashSet<string>(trade.TableProducts);
        foreach (var tab in reality.ProductTables) if (Paks.Decode(Path.ChangeExtension(Paks.Norm(tab.Value), ".MBIN")) is GcProductTable pt) foreach (var p in pt.Table) allProducts.Add(p.ID.Value);
        trade = trade with { TableProducts = shop.AlwaysProducts.Concat(shop.OptionalProducts).Select(i => i.Id).Where(allProducts.Contains).ToArray() };
        var biomes = Check.Row<BiomeTables>("BIOMETABLES");
        var profiles = Check.Row<PlanetProfileTables>("PLANETPROFILE");
        var placement = Check.Row<BuildingClassPlacement[]>("BUILDINGPLACEMENT");
        var overrides = Check.Row<BuildingOverrideRule[]>("BUILDINGOVERRIDES");
        var solar = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT").SolarLayout;
        var dates = Check.Dates(planets: true);
        int tested = 0, found = 0;
        foreach (var place in new[] { "planet:1", "planet:2", "planet:3", "planet:4", "planet:5", "planet:6" })
        foreach (var (addrText, seen) in Check.Truth(place))
        {
            int index = int.Parse(place[7..]);
            ulong system = Convert.ToUInt64(addrText, 16);
            var (code, galaxy) = PortalPositions.Generator(system);
            var a = SystemGenerator.Attributes(code, galaxy);
            if (a.Pirate || a.Abandoned || a.Uncharted) continue;
            int race = a.Race switch { SystemRace.Gek => 0, SystemRace.Vykeen => 1, SystemRace.Korvax => 2, _ => -1 };
            if (race < 0 || PortalPositions.Centres(system, solar) is not { } centres || index > centres.Length) continue;
            ulong planet = system | ((ulong)index << 52);
            var ctx = CrashedShipSites.Prepare(planet, centres[index - 1], biomes, profiles, placement, overrides, solar, null, null, null);
            if (ctx?.Terrain is null) { Console.WriteLine($"{addrText} {place}: no context"); continue; }
            bool old = string.CompareOrdinal(dates[(addrText, place)], "2026-10-28T12") < 0;
            var t = trade with { ProductTableEntries = trade.ProductTableEntries + (old ? -228 : -1) };
            var sites = new List<BuildingSite>(); var keys = new HashSet<ulong>();
            for (int face = 0; face < 6; face++)
                foreach (var s in PlanetBuildingSites.Sites(face, ctx.Divisions, ctx.Radius, ctx.SectorSeed, ctx.Slots, c => c == 6))
                    if (keys.Add(s.Key)) sites.Add(s);
            var hits = new List<string>();
            foreach (var s in sites)
            {
                ulong seed = ArtifactX.Core.Universe.DiscoveryKeys.Mix64(TerminalSeed(ctx, s, race));
                var stock = StationTradeStock.Generate(t, shop, seed, a.Wealth - 1, SystemGenerator.TradingClass(a.Economy), "Normal").Select(x => x.Id).ToHashSet();
                if (seen.All(stock.Contains)) { var (lat, lon) = s.Coordinates; hits.Add($"{lat:F2},{lon:F2}{(ctx.Builds(s) ? "" : " (ground test fails)")}"); }
            }
            tested++; if (hits.Count > 0) found++;
            Console.WriteLine($"{dates[(addrText, place)]} {addrText} {place} {a.Race} captured {seen.Count}, {sites.Count} outpost sites, full matches: {string.Join(" ; ", hits)}");
        }
        Console.WriteLine($"== {found}/{tested} captured trading posts have a site whose stock holds every captured item");
    }
}
