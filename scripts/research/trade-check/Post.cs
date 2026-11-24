using System.Reflection;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS.GameComponents;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
// The trading post nearest a visor coordinate on the save's current planet, and its predicted terminal stock.
public static class Post
{
    public static void Run(string savePath, double lat, double lon)
    {
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(File.ReadAllText(savePath))) { SupportMultipleContent = true };
        var root = JObject.Load(reader);
        var ua = root["vLc"]?["6f="]?["yhJ"];
        ulong system = NmsSpaceStationPaths.ReadSystemAddress(ua) ?? throw new InvalidOperationException("no address");
        int index = ua?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        string shops = root.SelectToken("vLc.6f=.LyC.:fe.FB5.TYf")?.Value<string>() ?? "Normal";
        var step = typeof(ArtifactX.GameData.CatalogSteps).GetMethod("TradeStock", BindingFlags.NonPublic | BindingFlags.Static)!;
        var trade = (TradeStockTables)step.Invoke(null, [new Func<string, NMSTemplate>(Paks.Decode)])!;
        var reality = (GcRealityManagerData)Paks.Decode("METADATA/REALITY/DEFAULTREALITY.MBIN");
        var shop = Posts.ShopList(trade, reality.TradeSettings.Shop);
        var allProducts = new HashSet<string>();
        foreach (var tab in reality.ProductTables) if (Paks.Decode(Path.ChangeExtension(Paks.Norm(tab.Value), ".MBIN")) is GcProductTable pt) foreach (var p in pt.Table) allProducts.Add(p.ID.Value);
        trade = trade with { TableProducts = shop.AlwaysProducts.Concat(shop.OptionalProducts).Select(i => i.Id).Where(allProducts.Contains).ToArray() };
        var solar = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT").SolarLayout;
        var (code, galaxy) = PortalPositions.Generator(system);
        var a = SystemGenerator.Attributes(code, galaxy);
        int race = a.Race switch { SystemRace.Gek => 0, SystemRace.Vykeen => 1, SystemRace.Korvax => 2, _ => -1 };
        var centres = PortalPositions.Centres(system, solar)!;
        var ctx = CrashedShipSites.Prepare(system | ((ulong)index << 52), centres[index - 1], Check.Row<BiomeTables>("BIOMETABLES"),
            Check.Row<PlanetProfileTables>("PLANETPROFILE"), Check.Row<BuildingClassPlacement[]>("BUILDINGPLACEMENT"),
            Check.Row<BuildingOverrideRule[]>("BUILDINGOVERRIDES"), solar, null, null, null)!;
        var dir = CrashedShipSites.Direction(lat, lon);
        Console.WriteLine($"system {system:X} planet {index} {a.Race} {a.Economy}/{a.Wealth} shops {shops}");
        using var c = new SqliteConnection($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/ArtifactX/GameData/nms_catalog.sqlite;Mode=ReadOnly"); c.Open();
        foreach (var s in PlanetBuildingSites.SitesNear(dir, ctx.Divisions, ctx.Radius, ctx.SectorSeed, ctx.Slots, k => k == 6, 3))
        {
            var (sl, so) = s.Coordinates;
            ulong seed = ArtifactX.Core.Universe.DiscoveryKeys.Mix64(Posts.TerminalSeed(ctx, s, race));
            Console.WriteLine($"site {sl:+0.00;-0.00}, {so:+0.00;-0.00} key {s.Key:X16} builds {ctx.Builds(s)}");
            foreach (var x in StationTradeStock.Generate(trade, shop, seed, a.Wealth - 1, SystemGenerator.TradingClass(a.Economy), shops))
            {
                var cmd = c.CreateCommand(); cmd.CommandText = "SELECT NameEnglish FROM Items WHERE GameId = $id LIMIT 1"; cmd.Parameters.AddWithValue("$id", x.Id);
                Console.WriteLine($"   {x.Kind,-10} {(x.Always ? "always" : "      ")} {x.Id,-14} {cmd.ExecuteScalar()}");
            }
        }
    }
}
