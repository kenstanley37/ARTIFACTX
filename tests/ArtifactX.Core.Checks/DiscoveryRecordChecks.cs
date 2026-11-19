using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

internal static class DiscoveryRecordChecks
{
    /// <summary>NmsDiscoveryRecords against the records of the owner's 2026-11-18 Slot 8 diff (Egha planet 1): the
    /// game's number format, which keys count as already discovered, and the stats a discovery raises.</summary>
    public static void Check()
    {
        const ulong egha = 0x10CBDAFBFFB004;
        var planet = new DiscoveryRecordKey("Planet", egha, [0x7679B68AF3AA6790, 5]);
        var plant = new DiscoveryRecordKey("Flora", egha, [0xEE343D2B73D4C31F, 0x15374F0EB05FE8F3]);
        var bird = new DiscoveryRecordKey("Animal", egha, [0x5ADF4B1FAA81423D, 0xE7CAD69E2F26FF55, 0x2D542BC74FE289DF, 0x9EC476E85A6D512A, 0xBEE2EB3F3B61B456]);

        var record = NmsDiscoveryRecords.CreateRecord(planet, 1795060928, "76561198090992118", "Neophyte");
        string expected = """{"8P3":{"5L6":4727741018517508,"<Dn":"Planet","bEr":["0x7679B68AF3AA6790",5]},"ksu":{"3I1":1795060928,"D6b":"","K7E":"76561198090992118","V?:":"Neophyte","f5Q":"76561198090992118"},"q9a":{}}""";
        if (record.ToString(Newtonsoft.Json.Formatting.None) != expected)
            throw new InvalidOperationException($"Discovery record format: {record.ToString(Newtonsoft.Json.Formatting.None)}");

        // A four-part record of the same creature counts as discovered; the plant doesn't yet.
        var saved = new JArray(record, new JObject { ["8P3"] = new JObject
        {
            ["5L6"] = "0x10CBDAFBFFB004", ["<Dn"] = "Animal",
            ["bEr"] = new JArray("0x5ADF4B1FAA81423D", "0xE7CAD69E2F26FF55", "0x2D542BC74FE289DF", "0x9EC476E85A6D512A"),
        } });
        var missing = NmsDiscoveryRecords.Missing(saved, [planet, bird, plant, plant]);
        if (missing.Count != 1 || missing[0] != plant)
            throw new InvalidOperationException($"Missing discoveries: {string.Join(", ", missing.Select(m => m.Kind))}, expected one Flora.");

        var groups = new JArray(new JObject
        {
            [":rc"] = "^GLOBAL_STATS", ["2Ak"] = 0,
            ["gUR"] = new JArray(new JObject { ["b2n"] = "^DISC_FLORA", [">MX"] = new JObject { [">vs"] = 457 } }),
        });
        var counted = NmsDiscoveryRecords.CountInStats(groups, [plant, planet]);
        var global = (JObject)counted[0];
        var planetGroup = counted.OfType<JObject>().Single(g => g[":rc"]?.Value<string>() == "^PLANET_STATS");
        if (NmsSpaceStationPaths.ReadStat(global, "^DISC_FLORA") != 458 || NmsSpaceStationPaths.ReadStat(global, "^DISC_PLANETS") != 1
            || NmsSpaceStationPaths.ReadStat(planetGroup, "^DISC_FLORA") != 1 || planetGroup["2Ak"]?.Value<long>() != (long)egha
            || ((JArray)planetGroup["gUR"]!).Count != 22 || NmsSpaceStationPaths.ReadStat((JObject)groups[0], "^DISC_FLORA") != 457)
            throw new InvalidOperationException("Discovery stats counted wrongly (or the input was changed).");
        Console.WriteLine("PASS: discovery records (game format, already-discovered keys, global and planet stats).");
    }
}
