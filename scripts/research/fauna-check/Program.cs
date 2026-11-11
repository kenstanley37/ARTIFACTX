// Scratch measurement: the existing fauna generator's totals vs the owner's captured "x / y fauna" totals.
// Read-only on both databases. Mirrors PlanetTypesService.Profile.
using System.Globalization;
using System.Text.Json;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

string catalog = args[0], userDb = args[1];
T Row<T>(string id)
{
    using var c = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
    c.Open();
    var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories k ON k.Id = i.CategoryId WHERE k.TemplateType = 'PlanetGeneration' AND i.GameId = $id";
    cmd.Parameters.AddWithValue("$id", id);
    return JsonSerializer.Deserialize<T>((string)cmd.ExecuteScalar()!)!;
}
var biomes = Row<BiomeTables>("BIOMETABLES");
var profiles = Row<PlanetProfileTables>("PLANETPROFILE");
Console.WriteLine($"creature tables loaded: {profiles.Creatures is not null}");

var truth = new List<(ulong System, int Index, int Total)>();
using (var u = new SqliteConnection($"Data Source={userDb};Mode=ReadOnly"))
{
    u.Open();
    var cmd = u.CreateCommand();
    cmd.CommandText = "SELECT system_address, planet_index, fauna_total FROM planet_details WHERE fauna_total IS NOT NULL AND deleted = 0";
    using var r = cmd.ExecuteReader();
    while (r.Read()) truth.Add((ulong.Parse(r.GetString(0), NumberStyles.HexNumber), r.GetInt32(1), r.GetInt32(2)));
}

static (ulong Code, int Galaxy) Gen(ulong address) =>
    ((((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF), (int)((address >> 32) & 0xFF));

int exact = 0, off1 = 0, none = 0, total = 0;
var diffs = new Dictionary<int, int>();
var zeroWrong = 0; var zeroRight = 0;
var lines = new List<string>();
foreach (var (system, index, want) in truth)
{
    total++;
    var (code, galaxy) = Gen(system & ~(0xFUL << 52));
    try
    {
        var types = PlanetGenerator.Generate(code, galaxy, biomes);
        var attributes = SystemGenerator.Attributes(code, galaxy);
        var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
        int i = index - 1;
        if (i < 0 || i >= types.Length || i >= seeds.Length) { none++; continue; }
        var p = PlanetProfileGenerator.Generate(seeds[i], i, types[i], (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy),
            attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
        if (p.FaunaCount is not int got) { none++; continue; }
        int d = got - want;
        diffs[d] = diffs.GetValueOrDefault(d) + 1;
        if (d == 0) exact++; else if (Math.Abs(d) == 1) off1++;
        if (want == 0) { if (got == 0) zeroRight++; else zeroWrong++; }
        lines.Add($"{system:X}\t{index}\t{types[i].Biome}\t{types[i].SubType}\t{p.Fauna}\twant {want}\tgot {got}");
        if (types[i].Biome == 14)
            Console.WriteLine($"waterworld {system:X} {index}: want {want} got {got} star {attributes.Star} uncharted {attributes.Uncharted} " +
                $"abandoned {attributes.Abandoned} pirate {attributes.Pirate} class {types[i].Class} habitats " +
                string.Join(",", (p.PredictedCreatures ?? []).GroupBy(c => c.Habitat).Select(g => g.Key + "x" + g.Count())));
    }
    catch (Exception ex) { none++; Console.WriteLine($"{system:X} {index}: {ex.GetType().Name} {ex.Message}"); }
}
Console.WriteLine($"planets {total}: exact {exact} ({100.0 * exact / total:F1}%), off by 1 {off1}, no prediction {none}");
Console.WriteLine($"recorded 0 fauna: predicted 0 {zeroRight}, predicted >0 {zeroWrong}");
Console.WriteLine("got - want: " + string.Join(", ", diffs.OrderBy(k => k.Key).Select(k => $"{k.Key:+0;-0;0}:{k.Value}")));
File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "fauna_compare.tsv"), lines);

// Habitats: every recorded creature must fit in the predicted list's habitats (recorded = discovered subset).
var recorded = new Dictionary<(ulong, int), List<string>>();
using (var u = new SqliteConnection($"Data Source={userDb};Mode=ReadOnly"))
{
    u.Open();
    var cmd = u.CreateCommand();
    cmd.CommandText = "SELECT system_address, planet_index, habitat FROM planet_creatures WHERE deleted = 0 AND habitat IS NOT NULL";
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        var key = (ulong.Parse(r.GetString(0), NumberStyles.HexNumber), r.GetInt32(1));
        if (!recorded.TryGetValue(key, out var l)) recorded[key] = l = new();
        l.Add(r.GetString(2));
    }
}
int fit = 0, misfit = 0, planets = 0;
var misfitLines = new List<string>();
foreach (var ((system, index), habitats) in recorded)
{
    var (code, galaxy) = Gen(system & ~(0xFUL << 52));
    var types = PlanetGenerator.Generate(code, galaxy, biomes);
    var attributes = SystemGenerator.Attributes(code, galaxy);
    var seeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
    int i = index - 1;
    if (i < 0 || i >= types.Length) continue;
    var p = PlanetProfileGenerator.Generate(seeds[i], i, types[i], (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy),
        attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
    var predicted = p.PredictedCreatures?.GroupBy(c => c.Habitat).ToDictionary(g => g.Key, g => g.Count()) ?? new();
    planets++;
    bool ok = habitats.GroupBy(h => h).All(g => predicted.GetValueOrDefault(g.Key) >= g.Count());
    if (ok) fit++; else { misfit++; misfitLines.Add($"{system:X} {index} biome {types[i].Biome}: recorded {string.Join(",", habitats.GroupBy(h => h).Select(g => g.Key + "x" + g.Count()))} | predicted {string.Join(",", predicted.Select(k => k.Key + "x" + k.Value))}"); }
}
Console.WriteLine($"planets with recorded creatures {planets}: habitats fit {fit}, don't fit {misfit}");
foreach (var l in misfitLines.Take(12)) Console.WriteLine("  " + l);

// Seeds: each creature discovery in the decrypted saves (read-only) stores bEr[0]; does the planet's predicted list
// contain a creature with that seed? Usage: third argument = %AppData%\ArtifactX\Working.
if (args.Length > 2)
{
    int found = 0, missingSeed = 0;
    var seen = new HashSet<(ulong, ulong)>();
    var planetCache = new Dictionary<ulong, HashSet<ulong>>();
    var examples = new List<string>();
    var perPlanet = new Dictionary<ulong, (int, int)>();
    var byYear = new SortedDictionary<int, (int, int)>();
    foreach (var file in Directory.EnumerateFiles(args[2], "save*.hg.json", SearchOption.AllDirectories))
    {
        Newtonsoft.Json.Linq.JToken? root;
        try
        {
            using var reader = new Newtonsoft.Json.JsonTextReader(new StreamReader(file)) { SupportMultipleContent = true };
            root = Newtonsoft.Json.Linq.JToken.ReadFrom(reader);
        }
        catch { continue; }
        if (root?.SelectToken("fDu.ETO.OsQ.?fB") is not Newtonsoft.Json.Linq.JArray discoveries) continue;
        foreach (var record in discoveries.OfType<Newtonsoft.Json.Linq.JObject>())
        {
            if (record["8P3"] is not Newtonsoft.Json.Linq.JObject key || key["<Dn"]?.Value<string>() != "Animal") continue;
            if (ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(key["5L6"]) is not ulong address) continue;
            if (key["bEr"] is not Newtonsoft.Json.Linq.JArray v || v.Count < 1
                || ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[0]) is not ulong seed) continue;
            if (!seen.Add((address, seed))) continue;
            if (!planetCache.TryGetValue(address, out var seeds))
            {
                seeds = new HashSet<ulong>();
                int index = (int)((address >> 52) & 0xF);
                var (code, galaxy) = Gen(address & ~(0xFUL << 52));
                try
                {
                    var types = PlanetGenerator.Generate(code, galaxy, biomes);
                    var attributes = SystemGenerator.Attributes(code, galaxy);
                    var bodySeeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
                    int i = index - 1;
                    if (i >= 0 && i < types.Length && i < bodySeeds.Length)
                    {
                        var p = PlanetProfileGenerator.Generate(bodySeeds[i], i, types[i], (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy),
                            attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
                        foreach (var c in p.PredictedCreatures ?? []) seeds.Add(c.Seed);
                    }
                }
                catch { }
                planetCache[address] = seeds;
            }
            var tally = perPlanet.TryGetValue(address, out var t0) ? t0 : (0, 0);
            perPlanet[address] = seeds.Contains(seed) ? (tally.Item1 + 1, tally.Item2) : (tally.Item1, tally.Item2 + 1);
            long when = record.SelectToken("ksu.3I1")?.Value<long>() ?? 0;
            int year = when > 0 ? DateTimeOffset.FromUnixTimeSeconds(when).Year : 0;
            var yt = byYear.TryGetValue(year, out var y0) ? y0 : (0, 0);
            byYear[year] = seeds.Contains(seed) ? (yt.Item1 + 1, yt.Item2) : (yt.Item1, yt.Item2 + 1);
            if (seeds.Contains(seed)) found++;
            else { missingSeed++; if (examples.Count < 6) examples.Add($"{address:X} seed {seed:X16} predicted [{string.Join(",", seeds.Take(4).Select(s => s.ToString("X16")))}...]"); }
        }
    }
    Console.WriteLine($"creature discoveries {found + missingSeed}: seed predicted {found}, not predicted {missingSeed}");
    var all = perPlanet.Where(k => k.Value.Item1 == 0 && k.Value.Item2 > 0).ToList();
    var part = perPlanet.Where(k => k.Value.Item1 > 0 && k.Value.Item2 > 0).ToList();
    Console.WriteLine($"planets {perPlanet.Count}: all found {perPlanet.Count(k => k.Value.Item2 == 0)}, none found {all.Count} ({all.Sum(k => k.Value.Item2)} creatures), some missing {part.Count} ({part.Sum(k => k.Value.Item2)} creatures)");
    foreach (var (year, (hit, miss)) in byYear) Console.WriteLine($"  discovered {year}: predicted {hit}, not {miss}");
    foreach (var k in part.Take(8)) Console.WriteLine($"  partial {k.Key:X}: found {k.Value.Item1}, missing {k.Value.Item2}");
    foreach (var k in all.Take(8)) Console.WriteLine($"  none {k.Key:X}: missing {k.Value.Item2}");
    foreach (var e in examples) Console.WriteLine("  " + e);
}
