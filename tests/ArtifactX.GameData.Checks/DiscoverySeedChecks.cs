using System.Globalization;
using System.Text.Json;
using ArtifactX.Core;
using ArtifactX.Core.Universe;
using ArtifactX.GameData.Services;

/// <summary>
/// PlanetProfileGenerator.FloraAndMinerals against a real save's Flora and Mineral discovery records
/// (fDu/ETO/OsQ/?fB 8P3: &lt;Dn kind, 5L6 planet address, bEr = [seed, type hash]): each record's seed and type hash
/// should be one the generator gives that planet. On the owner's Slot 8 (2026-11-19) 1043 of 1056 were; the check
/// fails below 95%. Tables from the phone's rules file (MobileRulesExportService), so it covers that data too.
/// </summary>
static class DiscoverySeedChecks
{
    public static void Run(string catalogPath, string savePath)
    {
        var rules = MobileRulesExportService.Read(catalogPath);
        string json = savePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? File.ReadAllText(savePath)
            : SaveStreamProcessor.ExtractRawJsonAsync(savePath).GetAwaiter().GetResult();
        json = json[..(json.LastIndexOf('}') + 1)];
        using var doc = JsonDocument.Parse(json);
        var records = doc.RootElement.GetProperty("fDu").GetProperty("ETO").GetProperty("OsQ").GetProperty("?fB");

        var byPlanet = new Dictionary<ulong, HashSet<(string, ulong, ulong)>?>();
        int total = 0, matched = 0, unknownPlanet = 0;
        foreach (var record in records.EnumerateArray())
        {
            var key = record.GetProperty("8P3");
            string kind = key.GetProperty("<Dn").GetString() ?? "";
            if (kind is not ("Flora" or "Mineral")) continue;
            ulong planet = Number(key.GetProperty("5L6"));
            var ber = key.GetProperty("bEr").EnumerateArray().Select(Number).ToArray();
            if (ber.Length < 2) continue;
            total++;
            if (!byPlanet.TryGetValue(planet, out var known)) byPlanet[planet] = known = Generate(planet, rules);
            if (known is null) { unknownPlanet++; continue; }
            if (known.Contains((kind, ber[0], ber[1]))) matched++;
            else Console.WriteLine($"  miss {planet:X}: {kind} {ber[0]:X16} {ber[1]:X16}");
        }
        double share = total == 0 ? 0 : matched * 100.0 / total;
        Console.WriteLine($"{matched} of {total} Flora and Mineral records match ({share:F1}%) on {byPlanet.Count} planets"
            + (unknownPlanet > 0 ? $"; {unknownPlanet} on planets the generator can't build" : "") + ".");
        Console.WriteLine(share >= 95 ? "PASS" : "FAIL: below 95%.");
        if (share < 95) Environment.ExitCode = 1;
    }

    private static HashSet<(string, ulong, ulong)>? Generate(ulong planet, GameRules rules)
    {
        int index = (int)((planet >> 52) & 0xF);
        var (portal, galaxy) = SystemProfiles.PortalOf(planet);
        try
        {
            var types = PlanetGenerator.Generate(portal, galaxy, rules.Biomes!);
            var seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
            var attributes = SystemGenerator.Attributes(portal, galaxy);
            if (index < 1 || index > types.Length || index > seeds.Length) return null;
            return PlanetProfileGenerator.FloraAndMinerals(seeds[index - 1], index - 1, types[index - 1], (int)attributes.Star,
                    PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, rules.Profiles!)
                .Select(d => (d.Kind, d.Seed, d.TypeHash)).ToHashSet();
        }
        catch { return null; }
    }

    /// <summary>A save number: a JSON number, or a "0x..." string for values past a double's range.</summary>
    private static ulong Number(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? ulong.Parse((e.GetString() ?? "0").Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber)
        : e.TryGetUInt64(out ulong u) ? u : unchecked((ulong)e.GetInt64());
}
