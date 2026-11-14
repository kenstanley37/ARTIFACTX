// Research harness: generated planet descriptors vs the owner's captured ones (planet_details.descriptor,
// "Tropical Planet"). Read-only on both databases. Usage: dotnet run -- <catalog.sqlite> <user.db> [misses]
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtifactX.Core.Universe;
using Microsoft.Data.Sqlite;

string catalog = args[0], userDb = args[1];
bool listMisses = args.Length > 2 && args[2] == "misses";
// "weather <biome>": generated weather type against the captured weather word, for one biome.
int? weatherBiome = args.Length > 3 && args[2] == "weather" ? int.Parse(args[3], CultureInfo.InvariantCulture) : null;
var weatherTab = new SortedDictionary<string, int>();
// "families": every planet's generated weather type against the family of its captured word (catalog tier).
bool families = args.Length > 2 && args[2] == "families";
// A word can be on two lists ("Clear": DEAD and BARREN_CLEAR), so each word keeps every family it is in.
var family = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
using (var cat = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly"))
{
    cat.Open();
    var fc = cat.CreateCommand();
    fc.CommandText = "SELECT i.NameEnglish, i.DescriptionEnglish FROM Items i JOIN Categories k ON k.Id = i.CategoryId WHERE k.TemplateType = 'PlanetDescriptors' AND i.UsageCategory = 'Weather'";
    using var fr = fc.ExecuteReader();
    while (fr.Read())
    {
        if (!family.TryGetValue(fr.GetString(0), out var list)) family[fr.GetString(0)] = list = new();
        list.Add(fr.IsDBNull(1) ? "" : fr.GetString(1));
    }
}
if (families) weatherBiome = -1;
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

using var db = new SqliteConnection($"Data Source={userDb};Mode=ReadOnly");
db.Open();
var q = db.CreateCommand();
q.CommandText = weatherBiome is null
    ? "SELECT system_address, planet_index, descriptor, updated_at, weather FROM planet_details WHERE deleted = 0 AND descriptor <> '' ORDER BY updated_at"
    : "SELECT system_address, planet_index, descriptor, updated_at, weather FROM planet_details WHERE deleted = 0 AND weather <> '' ORDER BY updated_at";
using var r = q.ExecuteReader();
int total = 0, hit = 0;
var missKinds = new Dictionary<string, int>();
while (r.Read())
{
    ulong address = ulong.Parse(r.GetString(0), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    int index = r.GetInt32(1);
    string recorded = r.GetString(2);
    var basics = SystemProfiles.Basics(address, biomes, profiles);
    if (index < 1 || index > basics.Length || basics[index - 1] is not { } b) continue;
    var (portal, galaxy) = SystemProfiles.PortalOf(address);
    var type = PlanetGenerator.Generate(portal, galaxy, biomes)[index - 1];
    total++;
    if (weatherBiome is int wb)
    {
        if (wb == -1)
        {
            static string Base(string f) => f.Replace("_CLEAR", "").Replace("_EXTREME", "").Replace("EXTREME", "");
            var fams = family.TryGetValue(r.GetString(4), out var f) ? f : new List<string> { "?" };
            string fam = fams[0];
            string k = Environment.GetEnvironmentVariable("BYBIOME") == "1"
                ? $"biome {type.Biome,2} sub {type.SubType,2} type {b.WeatherType,2} -> {Base(fam)}"
                : $"type {b.WeatherType,2} corrupt {(b.Sentinels.Length > 2 && b.Sentinels[2] == 3 ? "Y" : "n")} -> {fam}";
            weatherTab[k] = weatherTab.GetValueOrDefault(k) + 1;
            if (fam != "?")
            {
                string gen = PlanetProfileGenerator.WeatherWordFamily(type.Biome, type.SubType, b.WeatherType);
                string fk = fams.Any(x => Base(x) == gen) ? "FAMILY ok" : $"FAMILY miss biome {type.Biome} gen {gen} seen {Base(fam)}";
                weatherTab[fk] = weatherTab.GetValueOrDefault(fk) + 1;
            }
        }
        if (type.Biome == wb) { string k = $"type {b.WeatherType,2} extreme {b.Extreme,-5} '{r.GetString(4)}'"; weatherTab[k] = weatherTab.GetValueOrDefault(k) + 1; }
        if (type.Biome == wb && args.Length > 4 && args[4].Split(',').Contains(b.WeatherType.ToString(CultureInfo.InvariantCulture)))
            Console.WriteLine($"{address:X13} p{index} sub {type.SubType} class {type.Class} weather {b.WeatherType} storms {b.Storms} sent [{string.Join(",", b.Sentinels)}] " +
                $"desc rec '{r.GetString(2)}' gen '{b.Descriptor}' weather rec '{r.GetString(4)}'");
        continue;
    }
    // The game adds " Planet" / " Moon" to most words ("Tropical Planet"); compare the word itself.
    string word = Regex.Replace(recorded, @"\s+(Planet|Moon)$", "");
    if (string.Equals(word, b.Descriptor, StringComparison.OrdinalIgnoreCase)) { hit++; continue; }
    string kind = $"biome {type.Biome} sub {type.SubType}";
    missKinds[kind] = missKinds.GetValueOrDefault(kind) + 1;
    if (listMisses)
        Console.WriteLine($"{address:X13} p{index} class {type.Class} {kind} weather {b.WeatherType} extreme {b.Extreme} storms {b.Storms} " +
            $"sent [{string.Join(",", b.Sentinels)}]: recorded '{recorded}' gen '{b.Descriptor}' {r.GetString(3)[..10]}" +
            // Another body's word here means the reading was filed on the wrong planet.
            (basics.Select((o, i) => (o, i)).FirstOrDefault(x => x.i != index - 1 && string.Equals(x.o?.Descriptor, word, StringComparison.OrdinalIgnoreCase)) is { o: not null } other
                ? $"  <- body {other.i + 1}'s word" : ""));
}
foreach (var (k, n) in weatherTab) Console.WriteLine($"{n,4}  {k}");
if (weatherBiome is not null) return;
Console.WriteLine($"descriptors {hit}/{total}; misses by type: {string.Join(", ", missKinds.OrderByDescending(k => k.Value).Select(k => $"{k.Key}: {k.Value}"))}");
