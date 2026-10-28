using ArtifactX.Core.Universe;
using System.Text.Json;

/// <summary>Compares ported star positions with emulator dumps (regionpositions.py, POSITIONS_OUT jsonl).</summary>
static class Positions
{
    public static void Check(string jsonl, GalaxyGenerationSettings? settings = null, GalaxyPlacementSettings? placement = null)
    {
        if (settings is not null && placement is not null) { CheckOrdinary(jsonl, settings, placement); return; }
        int regions = 0, exactRegions = 0;
        foreach (string line in File.ReadLines(jsonl))
        {
            if (line.Length == 0) continue;
            var row = JsonDocument.Parse(line).RootElement;
            int g = row.GetProperty("galaxy").GetInt32(), x = row.GetProperty("x").GetInt32(), y = row.GetProperty("y").GetInt32(), z = row.GetProperty("z").GetInt32();
            var expected = row.GetProperty("stars").EnumerateArray()
                .Select(s => new RegionStar(s[0].GetInt32(), s[1].GetSingle(), s[2].GetSingle(), s[3].GetSingle())).ToArray();
            var port = RegionStarPositions.FixedStarsOf(g, x, y, z);
            int same = 0, firstDiff = -1;
            for (int i = 0; i < port.Count && i < expected.Length; i++)
            {
                if (port[i] == expected[i]) same++;
                else if (firstDiff < 0) firstDiff = i;
            }
            regions++;
            if (same == port.Count) exactRegions++;
            Console.WriteLine($"galaxy {g} {x:X3}/{y:X2}/{z:X3}: fixed stars {same}/{port.Count} bit-exact"
                + (firstDiff >= 0 ? $"; first diff #{firstDiff + 1}: port {port[firstDiff]} vs game {expected[firstDiff]}" : ""));
        }
        Console.WriteLine($"{exactRegions}/{regions} regions with every fixed star exact");
    }

    static void CheckOrdinary(string jsonl, GalaxyGenerationSettings settings, GalaxyPlacementSettings placement)
    {
        foreach (string line in File.ReadLines(jsonl))
        {
            if (line.Length == 0) continue;
            var row = JsonDocument.Parse(line).RootElement;
            int g = row.GetProperty("galaxy").GetInt32(), x = row.GetProperty("x").GetInt32(), y = row.GetProperty("y").GetInt32(), z = row.GetProperty("z").GetInt32();
            var expected = row.GetProperty("stars").EnumerateArray()
                .Select(s => new RegionStar(s[0].GetInt32(), s[1].GetSingle(), s[2].GetSingle(), s[3].GetSingle())).ToArray();
            var port = RegionStarPositions.Stars(g, x, y, z, settings, placement);
            if (port is null) { Console.WriteLine($"galaxy {g} {x:X3}/{y:X2}/{z:X3}: not covered (spiral or gap)"); continue; }
            int same = 0, firstDiff = -1;
            for (int i = 0; i < Math.Min(port.Count, expected.Length); i++)
            {
                if (port[i] == expected[i]) same++;
                else if (firstDiff < 0) firstDiff = i;
            }
            Console.WriteLine($"galaxy {g} {x:X3}/{y:X2}/{z:X3}: {same}/{expected.Length} ordinary stars bit-exact (port emitted {port.Count})"
                + (firstDiff >= 0 ? $"; first diff #{firstDiff + 1}: port {port[firstDiff]} vs game {expected[firstDiff]}" : ""));
        }
    }
}
