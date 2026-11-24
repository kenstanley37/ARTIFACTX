using System.Numerics;
using ArtifactX.Core.Universe;
public static class Sweep
{
    public static void Run(Vector3 local, int from, int to)
    {
        var t = Tables.Load();
        var stations = Check.Row<StationEquipmentTables>("STATIONEQUIPMENT");
        var cases = new List<(string addr, HashSet<string> seen, ulong seed, int wealth, int cls, int tradeDraws)>();
        foreach (var (addrText, seen) in Check.Truth("station"))
        {
            var (code, galaxy) = PortalPositions.Generator(Convert.ToUInt64(addrText, 16));
            var a = SystemGenerator.Attributes(code, galaxy);
            if (a.Abandoned || a.Uncharted || a.Pirate) continue;
            var pose = StationPoseGeneration.GenerateForSystem(code, galaxy, stations.SolarLayout);
            if (pose is null) continue;
            ulong seed = Stock.Mix64(MultiToolSiteGeneration.SeedForPosition(Check.World(pose, local)));
            Stock.DrawOverride = -1;
            Stock.Generate(t, t.Reality.TradeSettings.SpaceStation, seed, a.Wealth - 1, Check.ClassOfEconomy[(int)a.Economy], false);
            cases.Add((addrText, seen.Where(x => t.Substances.ContainsKey(x)).ToHashSet(), seed, a.Wealth - 1, Check.ClassOfEconomy[(int)a.Economy], Stock.LastTradeDraws));
        }
        Console.WriteLine("trade draws natural: " + string.Join(",", cases.Select(c => c.tradeDraws).Distinct().Take(10)));
        var best = new List<(int n, int hit, int total)>();
        for (int n = from; n <= to; n++)
        {
            Stock.DrawOverride = n; int hit = 0, total = 0;
            foreach (var c in cases)
            {
                var p = Stock.Generate(t, t.Reality.TradeSettings.SpaceStation, c.seed, c.wealth, c.cls, false).ToHashSet();
                hit += c.seen.Count(p.Contains); total += c.seen.Count;
            }
            best.Add((n, hit, total));
        }
        foreach (var b in best.OrderByDescending(b => b.hit).Take(8)) Console.WriteLine($"draws {b.n}: substances {b.hit}/{b.total}");
        if (Environment.GetEnvironmentVariable("PER") is null) return;
        var dates = Check.Dates();
        foreach (var c in cases.OrderBy(c => dates[c.addr]))
        {
            var fits = new List<int>();
            for (int n = from; n <= to; n++)
            {
                Stock.DrawOverride = n;
                var p = Stock.Generate(t, t.Reality.TradeSettings.SpaceStation, c.seed, c.wealth, c.cls, false).ToHashSet();
                if (c.seen.All(p.Contains)) fits.Add(n);
            }
            Stock.DrawOverride = -1; Stock.Generate(t, t.Reality.TradeSettings.SpaceStation, c.seed, c.wealth, c.cls, false);
            Console.WriteLine($"{dates[c.addr]} {c.addr} prodsBefore {Stock.ProductsBefore} natural {Stock.LastTradeDraws} subs {c.seen.Count} fits: {(fits.Count > 6 ? fits.Count + " values" : string.Join(",", fits))}");
        }
    }
}
