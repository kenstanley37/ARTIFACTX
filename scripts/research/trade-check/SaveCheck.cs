using System.Reflection;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using libMBIN;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
// The station stock of a decrypted save's current system, with names from the catalog: compare with the game's screen.
public static class SaveCheck
{
    public static void Run(string savePath, int offset)
    {
        var text = File.ReadAllText(savePath);
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(text)) { SupportMultipleContent = true };
        var root = JObject.Load(reader);
        ulong address = NmsSpaceStationPaths.ReadSystemAddress(root["vLc"]?["6f="]?["yhJ"]) ?? throw new InvalidOperationException("no address");
        string shops = root.SelectToken("vLc.6f=.LyC.:fe.FB5.TYf")?.Value<string>() ?? "Normal";
        var step = typeof(ArtifactX.GameData.CatalogSteps).GetMethod("TradeStock", BindingFlags.NonPublic | BindingFlags.Static)!;
        var trade = (TradeStockTables)step.Invoke(null, [new Func<string, NMSTemplate>(Paks.Decode)])!;
        var stations = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT");
        var (code, galaxy) = PortalPositions.Generator(address);
        var stock = StationTradeStock.ForSystem(code, galaxy, false, shops, stations, trade with { ProductTableEntries = trade.ProductTableEntries + offset });
        Console.WriteLine($"system {address:X} shops {shops} entries {trade.ProductTableEntries + offset}");
        using var c = new SqliteConnection($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/ArtifactX/GameData/nms_catalog.sqlite;Mode=ReadOnly"); c.Open();
        foreach (var s in stock ?? [])
        {
            var cmd = c.CreateCommand(); cmd.CommandText = "SELECT NameEnglish FROM Items WHERE GameId = $id LIMIT 1"; cmd.Parameters.AddWithValue("$id", s.Id);
            Console.WriteLine($"  {s.Kind,-10} {(s.Always ? "always" : "      ")} {s.Id,-16} {cmd.ExecuteScalar()}");
        }
    }
}
