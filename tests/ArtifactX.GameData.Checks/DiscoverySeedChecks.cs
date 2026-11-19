using System.Globalization;
using System.Text.Json;
using ArtifactX.Core;
using ArtifactX.Core.Universe;
using ArtifactX.GameData.Services;

/// <summary>
/// SystemDiscoveries.ForSystem against a real save's discovery records (fDu/ETO/OsQ/?fB 8P3: &lt;Dn kind, 5L6 planet
/// address, bEr): each Planet, Animal, Flora and Mineral record should be one the generators give its system, key for
/// key (an Animal record's optional fifth part, Mix64 of its seed, is only there for some creatures: 273 of 492 in
/// Slot 8, with no trait of the creature that predicts it, so a four-part record matches the first four). On the owner's Slot 8 (2026-11-19) 1043 of 1056 Flora and Mineral records were; the check fails when plants
/// and minerals fall below 95%. Tables from the phone's rules file (MobileRulesExportService), so it covers that data too.
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

        var bySystem = new Dictionary<ulong, HashSet<string>>();
        var totals = new SortedDictionary<string, (int Matched, int All)>(StringComparer.Ordinal);
        foreach (var record in records.EnumerateArray())
        {
            var key = record.GetProperty("8P3");
            string kind = key.GetProperty("<Dn").GetString() ?? "";
            if (kind is not ("Planet" or "Animal" or "Flora" or "Mineral")) continue;
            ulong planet = Number(key.GetProperty("5L6"));
            var ber = key.GetProperty("bEr").EnumerateArray().Select(Number).ToArray();
            ulong system = planet & ~(0xFUL << 52);
            if (!bySystem.TryGetValue(system, out var known))
                bySystem[system] = known = SystemDiscoveries.ForSystem(system, rules.Biomes!, rules.Profiles!)
                    .SelectMany(r => r.Kind == "Animal" ? [Text(r.Kind, r.Address, r.Ber), Text(r.Kind, r.Address, r.Ber[..4])]
                        : new[] { Text(r.Kind, r.Address, r.Ber) }).ToHashSet(StringComparer.Ordinal);
            bool ok = known.Contains(Text(kind, planet, ber));
            var t = totals.GetValueOrDefault(kind);
            totals[kind] = (t.Matched + (ok ? 1 : 0), t.All + 1);
            if (!ok && kind is "Flora" or "Mineral") Console.WriteLine($"  miss {planet:X}: {kind} {string.Join(" ", ber.Select(b => b.ToString("X16")))}");
        }
        foreach (var (kind, (matched, all)) in totals)
            Console.WriteLine($"{kind}: {matched} of {all} records match ({matched * 100.0 / Math.Max(1, all):F1}%).");
        int plants = totals.GetValueOrDefault("Flora").Matched + totals.GetValueOrDefault("Mineral").Matched;
        int plantsAll = totals.GetValueOrDefault("Flora").All + totals.GetValueOrDefault("Mineral").All;
        double share = plantsAll == 0 ? 0 : plants * 100.0 / plantsAll;
        Console.WriteLine($"{bySystem.Count} systems. " + (share >= 95 ? "PASS" : "FAIL: plants and minerals below 95%."));
        if (share < 95) Environment.ExitCode = 1;
    }

    private static string Text(string kind, ulong planet, ulong[] ber) => $"{kind}|{planet:X}|{string.Join(",", ber.Select(b => b.ToString("X")))}";

    /// <summary>A save number: a JSON number, or a "0x..." string for values past a double's range.</summary>
    private static ulong Number(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? ulong.Parse((e.GetString() ?? "0").Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber)
        : e.TryGetUInt64(out ulong u) ? u : unchecked((ulong)e.GetInt64());
}
