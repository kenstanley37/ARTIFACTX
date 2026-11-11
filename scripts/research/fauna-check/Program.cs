// Scratch measurement: the existing fauna generator's totals vs the owner's captured "x / y fauna" totals.
// Read-only on both databases. Mirrors PlanetTypesService.Profile.
using System.Globalization;
using System.Text.Json;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

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
