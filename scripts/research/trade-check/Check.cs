using System.Numerics;
using System.Text.Json;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;
using libMBIN.NMS.GameComponents;
public static class Check
{
    // The captured terminal stock (market_items) was dropped from user.db in migration 22; the pre-migration backup keeps it.
    static string UserDb => Environment.GetEnvironmentVariable("USERDB") ?? $"{AppData}/ArtifactX/Backups/user-db/user-20261124-154156-before-v22.db";
    static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static T Row<T>(string id)
    {
        using var c = new SqliteConnection($"Data Source={AppData}/ArtifactX/GameData/nms_catalog.sqlite;Mode=ReadOnly"); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories k ON k.Id = i.CategoryId WHERE k.TemplateType = 'PlanetGeneration' AND i.GameId = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return JsonSerializer.Deserialize<T>((string)cmd.ExecuteScalar()!)!;
    }
    public static Dictionary<string, HashSet<string>> Truth(string place)
    {
        var d = new Dictionary<string, HashSet<string>>();
        using var c = new SqliteConnection($"Data Source={UserDb};Mode=ReadOnly"); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "select system_address, item_id from market_items where deleted=0 and place=$p";
        cmd.Parameters.AddWithValue("$p", place);
        using var r = cmd.ExecuteReader();
        while (r.Read()) { if (!d.TryGetValue(r.GetString(0), out var s)) d[r.GetString(0)] = s = new(); s.Add(r.GetString(1)); }
        return d;
    }
    public static Dictionary<string, string> Dates()
    {
        var d = new Dictionary<string, string>();
        using var c = new SqliteConnection($"Data Source={UserDb};Mode=ReadOnly"); c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "select system_address, min(substr(seen_at,1,16)) from market_items where deleted=0 and place='station' group by system_address";
        using var r = cmd.ExecuteReader(); while (r.Read()) d[r.GetString(0)] = r.GetString(1);
        return d;
    }
    public static readonly int[] ClassOfEconomy = { -1, 2, 4, 5, 0, 3, 1, 6 }; // SystemEconomy -> GcTradingClass

    public static Vector3 World(StationPose pose, Vector3 local)
    {
        Vector3 f = Vector3.Normalize(pose.Facing), r = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, f)), u = Vector3.Cross(f, r);
        return local.X * r + local.Y * u + local.Z * f + pose.Position;
    }

    public static void Run(Vector3 local, string mode)
    {
        var t = Tables.Load();
        if (Environment.GetEnvironmentVariable("TABLES") is { } tu) Stock.TablesUsed = int.Parse(tu);
        if (Environment.GetEnvironmentVariable("DRAWS") is { } dr) Stock.DrawOverride = int.Parse(dr);
        var stations = Row<StationEquipmentTables>("STATIONEQUIPMENT");
        var truth = Truth("station"); var dates = Dates();
        int n = 0, full = 0, totalHit = 0, totalSeen = 0;
        var missCounts = new Dictionary<string, int>();
        foreach (var (addrText, seen) in truth.OrderBy(k => k.Key))
        {
            ulong address = Convert.ToUInt64(addrText, 16);
            var (code, galaxy) = PortalPositions.Generator(address);
            var a = SystemGenerator.Attributes(code, galaxy);
            if (a.Abandoned || a.Uncharted) { Console.WriteLine($"{addrText} skip abandoned/uncharted"); continue; }
            var pose = StationPoseGeneration.GenerateForSystem(code, galaxy, stations.SolarLayout);
            if (pose is null) { Console.WriteLine($"{addrText} no pose"); continue; }
            var world = World(pose, local);
            ulong site = MultiToolSiteGeneration.SeedForPosition(world);
            ulong seed = mode == "nomix" ? site : Stock.Mix64(site);
            int wealth = a.Pirate ? 3 : a.Wealth - 1;
            var td = a.Pirate ? t.Reality.TradeSettings.SmugglerStation : t.Reality.TradeSettings.SpaceStation;
            Stock.DrawOverride = -1;
            var predicted = Stock.Generate(t, td, seed, wealth, ClassOfEconomy[(int)a.Economy], a.Pirate);
            bool old = string.CompareOrdinal(dates[addrText], "2026-10-28T12") < 0;
            { Stock.DrawOverride = Stock.LastTradeDraws + (old ? -226 : 1); predicted = Stock.Generate(t, td, seed, wealth, ClassOfEconomy[(int)a.Economy], a.Pirate); Stock.DrawOverride = -1; }
            var p = predicted.ToHashSet();
            int hit = seen.Count(p.Contains);
            n++; totalHit += hit; totalSeen += seen.Count; if (hit == seen.Count) full++;
            foreach (var m in seen.Where(x => !p.Contains(x))) missCounts[m] = missCounts.GetValueOrDefault(m) + 1;
            if (Environment.GetEnvironmentVariable("QUIET") is null) Console.WriteLine($"{dates[addrText]} {addrText} {a.Economy}/{a.Wealth}{(a.Pirate ? " pirate" : "")} hit {hit}/{seen.Count} pred {predicted.Count}  missing: {string.Join(",", seen.Where(x => !p.Contains(x)))}");
        }
        Console.WriteLine($"== {n} stations, all captured items predicted in {full}; items {totalHit}/{totalSeen} ({100.0 * totalHit / totalSeen:F1}%)");
        Console.WriteLine("most missed: " + string.Join(", ", missCounts.OrderByDescending(k => k.Value).Take(15).Select(k => $"{k.Key}:{k.Value}")));
    }
}
