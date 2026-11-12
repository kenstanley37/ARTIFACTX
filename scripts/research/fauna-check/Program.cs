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
    var creatureBySeed = new Dictionary<ulong, (PredictedCreature C, ulong PlanetSeed)>();
    var probe = new List<string>();
    var nameProbe = new List<(ulong Seed, ulong B2, ulong B3, ulong PlanetSeed, ulong Address)>();
    var allNames = new List<(ulong Address, ulong B2, ulong B3)>();
    var resourceHash = new Dictionary<(ulong, ulong), ulong>();
    var discoveryYear = new Dictionary<(ulong, ulong), int>();
    var recent = new List<(ulong Address, ulong Seed, ulong B2, ulong B3)>();
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
            if (v.Count >= 4 && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[2]) is ulong n2
                && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[3]) is ulong n3)
                allNames.Add((address, n2, n3));
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
                        foreach (var c in p.PredictedCreatures ?? []) { seeds.Add(c.Seed); creatureBySeed[c.Seed] = (c, bodySeeds[i]); }
                    }
                }
                catch { }
                planetCache[address] = seeds;
            }
            var tally = perPlanet.TryGetValue(address, out var t0) ? t0 : (0, 0);
            perPlanet[address] = seeds.Contains(seed) ? (tally.Item1 + 1, tally.Item2) : (tally.Item1, tally.Item2 + 1);
            long when = record.SelectToken("ksu.3I1")?.Value<long>() ?? 0;
            int year = when > 0 ? DateTimeOffset.FromUnixTimeSeconds(when).Year : 0;
            discoveryYear[(address, seed)] = year;
            var yt = byYear.TryGetValue(year, out var y0) ? y0 : (0, 0);
            byYear[year] = seeds.Contains(seed) ? (yt.Item1 + 1, yt.Item2) : (yt.Item1, yt.Item2 + 1);
            if (v.Count >= 2 && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[1]) is ulong n1)
                resourceHash[(address, seed)] = n1;
            if (Environment.GetEnvironmentVariable("SINCE") is string since && record.SelectToken("ksu.3I1")?.Value<long>() >= long.Parse(since)
                && v.Count >= 4 && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[2]) is ulong r2
                && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[3]) is ulong r3)
                recent.Add((address, seed, r2, r3));
            if (seeds.Contains(seed) && v.Count >= 4 && creatureBySeed.TryGetValue(seed, out var pn)
                && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[2]) is ulong b2
                && ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(v[3]) is ulong b3)
                nameProbe.Add((seed, b2, b3, pn.PlanetSeed, address));
            if (seeds.Contains(seed) && probe.Count < 14 && creatureBySeed.TryGetValue(seed, out var pc))
                probe.Add($"{address:X} {pc.C.Id,-14} role {pc.C.Role} planetSeed {pc.PlanetSeed:X16} bEr [{string.Join(", ", v.Select(x => ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ParsePackedAddress(x)?.ToString("X16")))}]");
            if (seeds.Contains(seed)) found++;
            else { missingSeed++; if (examples.Count < 6) examples.Add($"{address:X} seed {seed:X16} predicted [{string.Join(",", seeds.Take(4).Select(s => s.ToString("X16")))}...]"); }
        }
    }
    Console.WriteLine($"creature discoveries {found + missingSeed}: seed predicted {found}, not predicted {missingSeed}");
    var all = perPlanet.Where(k => k.Value.Item1 == 0 && k.Value.Item2 > 0).ToList();
    var part = perPlanet.Where(k => k.Value.Item1 > 0 && k.Value.Item2 > 0).ToList();
    Console.WriteLine($"planets {perPlanet.Count}: all found {perPlanet.Count(k => k.Value.Item2 == 0)}, none found {all.Count} ({all.Sum(k => k.Value.Item2)} creatures), some missing {part.Count} ({part.Sum(k => k.Value.Item2)} creatures)");
    foreach (var line in probe) Console.WriteLine("probe " + line);
    // bEr[2] (species) and bEr[3] (genus) predicted with SpeciesNames.FaunaSeeds (NMS.exe 184271 0x14123e740, called
    // by the Animal discovery key builder 0x14044e1e0). For misses: which creature id would give the species seed, and
    // which (type, diet) the genus seed.
    static ulong H(ulong lo, ulong hi)
    {
        const ulong k = 0x9DDFEA08EB382D69UL;
        ulong a = (lo ^ hi) * k; a ^= a >> 47;
        ulong b = (hi ^ a) * k; b ^= b >> 47; return b * k;
    }
    var allIds = profiles.Creatures!.Creatures.Select(c => c.Id).Distinct().ToArray();
    int tested = 0, speciesHit = 0, genusHit = 0, bothHit = 0;
    var speciesMiss = new Dictionary<string, int>();
    var genusMiss = new Dictionary<string, int>();
    var nameByYear = new SortedDictionary<int, (int, int, int, int)>();
    foreach (var (discSeed, b2, b3, planetSeed, address) in nameProbe)
    {
        tested++;
        var c = creatureBySeed[discSeed].C;
        var (species, genus) = ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(discSeed, c.SpeciesId, address, c.CreatureType, c.SizeClass);
        if (species == b2)
        {
            speciesHit++;
            if (speciesHit <= 2)
                for (int y = 0; y < 4; y++)
                    if (ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(discSeed, c.Id, address, c.CreatureType, y).Genus == b3)
                        Console.WriteLine($"  example: id {c.Id} seed 0x{discSeed:X16} address 0x{address:X16} type {c.CreatureType} size class {y} -> species 0x{b2:X16} genus 0x{b3:X16}");
        }
        else
        {
            string real = allIds.FirstOrDefault(id => ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(discSeed, id, address, 0, 0).Species == b2) ?? "?";
            string k = $"{c.Id} -> {real}";
            speciesMiss[k] = speciesMiss.GetValueOrDefault(k) + 1;
        }
        if (genus == b3) genusHit++;
        else
        {
            string real = "?";
            for (int x = 0; x < 64 && real == "?"; x++)
                for (int y = 0; y < 4; y++)
                    if (H((ulong)y, H((ulong)x, address)) == b3) { real = $"type {x} diet {y}"; break; }
            string k = $"{c.Id} role {c.Role} {c.Domain}: predicted type {c.CreatureType} diet {c.Diet}, real {real}";
            genusMiss[k] = genusMiss.GetValueOrDefault(k) + 1;
        }
        if (species == b2 && genus == b3) bothHit++;
        int dy = discoveryYear.GetValueOrDefault((address, discSeed));
        var yr = nameByYear.TryGetValue(dy, out var y1) ? y1 : (0, 0, 0, 0);
        nameByYear[dy] = (yr.Item1 + 1, yr.Item2 + (species == b2 ? 1 : 0), yr.Item3 + (genus == b3 ? 1 : 0), yr.Item4 + (species == b2 && genus == b3 ? 1 : 0));
    }
    // Is int@+0xac an index among the planet's creatures? Count earlier creatures in the predicted list that share
    // the type / id / role / domain+type.
    var planetLists = new Dictionary<ulong, PredictedCreature[]>();
    var indexHits = new Dictionary<string, int>();
    foreach (var (discSeed, b2, b3, planetSeed, address) in nameProbe)
    {
        if (!planetLists.TryGetValue(address, out var list))
        {
            int index = (int)((address >> 52) & 0xF);
            var (code, galaxy) = Gen(address & ~(0xFUL << 52));
            var types = PlanetGenerator.Generate(code, galaxy, biomes);
            var attributes = SystemGenerator.Attributes(code, galaxy);
            var bodySeeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
            list = PlanetProfileGenerator.Generate(bodySeeds[index - 1], index - 1, types[index - 1], (int)attributes.Star,
                PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned)
                .PredictedCreatures ?? [];
            planetLists[address] = list;
        }
        int at = Array.FindIndex(list, x => x.Seed == discSeed);
        var me = list[at];
        var before = list.Take(at).ToArray();
        var candidates = new Dictionary<string, int>
        {
            ["same type"] = before.Count(x => x.CreatureType == me.CreatureType),
            ["same id"] = before.Count(x => x.Id == me.Id),
            ["same role"] = before.Count(x => x.Role == me.Role),
            ["same domain"] = before.Count(x => x.Domain == me.Domain),
            ["same domain+type"] = before.Count(x => x.Domain == me.Domain && x.CreatureType == me.CreatureType),
            ["position"] = at,
        };
        foreach (var (name, value) in candidates)
            if (H((ulong)value, H((ulong)me.CreatureType, address)) == b3) indexHits[name] = indexHits.GetValueOrDefault(name) + 1;
    }
    Console.WriteLine("genus int@+0xac as an index: " + string.Join(", ", indexHits.Select(k => $"{k.Key} {k.Value}")));
    // The real size class (from the saved genus, type predicted) against the role's MinSize..MaxSize.
    var sizeTally = new Dictionary<string, int>();
    foreach (var (discSeed, b2, b3, planetSeed, address) in nameProbe)
    {
        var c = creatureBySeed[discSeed].C;
        int real = -1;
        for (int y = 0; y < 4; y++)
            if (ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(discSeed, c.Id, address, c.CreatureType, y).Genus == b3) { real = y; break; }
        if (real == 2 && c.SizeClass == 2 && c.MinSize != c.MaxSize && !sizeTally.ContainsKey("example"))
        {
            var e = profiles.Creatures!.Creatures.First(x => x.Id == c.Id);
            var z = profiles.Creatures.Sizes!;
            Console.WriteLine($"  size example: {c.Id} seed 0x{discSeed:X16} scale {e.MinScale}-{e.MaxScale} range {c.MinSize}-{c.MaxSize} " +
                $"sizes {z.MedMinSize}/{z.LargeMinSize}/{z.HugeMinSize}/{z.MaxCreatureSize} -> {c.Scale:R} class {c.SizeClass}");
            sizeTally["example"] = 0;
        }
        string k = real < 0 ? "type wrong" : real == c.SizeClass ? "class predicted" : $"class wrong: predicted {c.SizeClass} (scale {c.Scale:F2}) real {real}, {c.Id} range {c.MinSize}-{c.MaxSize}";
        sizeTally[k] = sizeTally.GetValueOrDefault(k) + 1;
    }
    foreach (var t in sizeTally.OrderByDescending(k => k.Value).Take(25)) Console.WriteLine($"  size {t.Key} x{t.Value}");
    // bEr[1] against djb2 of the creature filename table's scene files (FILENAMES env var: "ID<TAB>file<TAB>extra"
    // rows), and which creature ID makes the saved species seed.
    if (Environment.GetEnvironmentVariable("FILENAMES") is string fnPath && File.Exists(fnPath))
    {
        static ulong Djb2(string text) { ulong h = 5381; foreach (char ch in text) h = h * 33 + (ulong)(long)(sbyte)ch; return h; }
        var sceneByHash = new Dictionary<ulong, string>();
        foreach (var line in File.ReadAllLines(fnPath))
            foreach (var f in line.Split('	').Skip(1).Where(f => f.Length > 0))
                foreach (var form in new[] { f, f.ToLowerInvariant(), f.Replace(".MBIN", ""), f.Replace(".MBIN", "").ToLowerInvariant() })
                    sceneByHash.TryAdd(Djb2(form), form);
        var sceneTally = new Dictionary<string, int>();
        foreach (var (discSeed, b2, b3, planetSeed, address) in nameProbe)
        {
            var c = creatureBySeed[discSeed].C;
            string scene = resourceHash.TryGetValue((address, discSeed), out var h) && sceneByHash.TryGetValue(h, out var sc) ? sc : $"? {h:X16}";
            string real = allIds.FirstOrDefault(id => ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(discSeed, id, address, 0, 0).Species == b2) ?? "?";
            string k = $"{c.Id} (species {c.SpeciesId}) -> scene {scene} -> species id {real}";
            sceneTally[k] = sceneTally.GetValueOrDefault(k) + 1;
        }
        foreach (var t in sceneTally.OrderByDescending(k => k.Value).Take(45)) Console.WriteLine($"  scene x{t.Value}: {t.Key}");
    }
    Console.WriteLine($"name seeds tested {tested}: species {speciesHit}, genus {genusHit}, both {bothHit}");
    {
        List<string> words;
        using (var cc = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly"))
        {
            cc.Open();
            var cmd = cc.CreateCommand();
            cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType='MultiToolNameWords' AND i.NameEnglish='CRE'";
            words = JsonSerializer.Deserialize<List<string>>((string)cmd.ExecuteScalar()!)!;
        }
        foreach (var (address, seed, r2, r3) in recent)
        {
            string real = ArtifactX.Core.Procedural.SpeciesNames.Fauna(r2, r3, words) ?? "?";
            string predictedName = "not predicted";
            if (creatureBySeed.TryGetValue(seed, out var pc))
            {
                var c = pc.C;
                var (sp, ge) = ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(seed, c.SpeciesId, address, c.CreatureType, c.SizeClass);
                predictedName = $"{ArtifactX.Core.Procedural.SpeciesNames.Fauna(sp, ge, words)} ({c.Id}, species id {c.SpeciesId}, type {c.CreatureType}, size {c.SizeClass}, scale {c.Scale:F2})";
            }
            Console.WriteLine($"  recent {address:X}: game {real} | predicted {predictedName}");
        }
    }
    // PLAYER_PLANET=1: the predicted species names on the planet the newest save's player stands on.
    if (Environment.GetEnvironmentVariable("PLAYER_PLANET") == "1")
        foreach (var file in Directory.EnumerateFiles(args[2], "save*.hg.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).Take(1))
        {
            Newtonsoft.Json.Linq.JToken root;
            using (var reader = new Newtonsoft.Json.JsonTextReader(new StreamReader(file)) { SupportMultipleContent = true })
                root = Newtonsoft.Json.Linq.JToken.ReadFrom(reader);
            if (ArtifactX.Core.NmsModels.NmsSpaceStationPaths.ReadSystemAddress(root["vLc"]?["6f="]?["yhJ"]) is not ulong systemHere)
            {
                Console.WriteLine("  player address not found");
                continue;
            }
            int jsv = root["vLc"]?["6f="]?["yhJ"]?["oZw"]?["jsv"]?.Value<int>() ?? 0;
            foreach (int index in new[] { jsv - 1, jsv })
            {
            if (index < 0) continue;
            ulong here = systemHere;
            var (code, galaxy) = Gen(here & ~(0xFUL << 52));
            var types = PlanetGenerator.Generate(code, galaxy, biomes);
            var attributes = SystemGenerator.Attributes(code, galaxy);
            var bodySeeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
            var p = PlanetProfileGenerator.Generate(bodySeeds[index], index, types[index], (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy),
                attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
            List<string> words;
            using (var cc = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly"))
            {
                cc.Open();
                var cmd = cc.CreateCommand();
                cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType='MultiToolNameWords' AND i.NameEnglish='CRE'";
                words = JsonSerializer.Deserialize<List<string>>((string)cmd.ExecuteScalar()!)!;
            }
            Console.WriteLine($"  player at {here:X16}, planet index {index}");
            foreach (var c in p.PredictedCreatures ?? [])
            {
                ulong planetAddress = (here & ~(0xFUL << 52)) | ((ulong)(index + 1) << 52);
                var (sp, ge) = ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(c.Seed, c.SpeciesId, planetAddress, c.CreatureType, c.SizeClass);
                Console.WriteLine($"  predicted on this planet: {ArtifactX.Core.Procedural.SpeciesNames.Fauna(sp, ge, words)} ({c.Id}, {c.Habitat}, size {c.SizeClass})");
            }
            }
        }
    foreach (var (y, (n, sp, ge, bo)) in nameByYear) Console.WriteLine($"  names {y}: {n} discoveries, species {sp}, genus {ge}, both {bo}");
    foreach (var m in speciesMiss.OrderByDescending(k => k.Value).Take(15)) Console.WriteLine($"  species miss x{m.Value}: {m.Key}");
    foreach (var m in genusMiss.OrderByDescending(k => k.Value).Take(15)) Console.WriteLine($"  genus miss x{m.Value}: {m.Key}");
    // The genus's second int (@+0xac, 0-3) is not the role's diet. Name every discovery from its real seeds, find the
    // owner's captured creature by name, and list its traits against that int.
    List<string> creWords;
    using (var cc = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly"))
    {
        cc.Open();
        var cmd = cc.CreateCommand();
        cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType='MultiToolNameWords' AND i.NameEnglish='CRE'";
        creWords = JsonSerializer.Deserialize<List<string>>((string)cmd.ExecuteScalar()!)!;
    }
    var captured = new Dictionary<(ulong, string), string>();
    using (var u = new SqliteConnection($"Data Source={userDb};Mode=ReadOnly"))
    {
        u.Open();
        var cmd = u.CreateCommand();
        cmd.CommandText = "SELECT system_address, name, rarity, activity, gender, habitat, size FROM planet_creatures WHERE deleted = 0";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            captured[(ulong.Parse(r.GetString(0), NumberStyles.HexNumber) & ~(0xFUL << 52), r.GetString(1))] =
                $"{r.GetString(6),-18} rarity {r.GetString(2),-9} habitat {r.GetString(5)}";
    }
    var traitLines = new Dictionary<string, int>();
    int named = 0;
    foreach (var (address, n2, n3) in allNames)
    {
        string? name = ArtifactX.Core.Procedural.SpeciesNames.Fauna(n2, n3, creWords);
        if (name is null || !captured.TryGetValue((address & ~(0xFUL << 52), name), out var traits)) continue;
        named++;
        string ac = "?";
        for (int x = 0; x < 64 && ac == "?"; x++)
            for (int y = 0; y < 8; y++)
                if (H((ulong)y, H((ulong)x, address)) == n3) { ac = y.ToString(); break; }
        string line = $"ac {ac}: {traits}";
        traitLines[line] = traitLines.GetValueOrDefault(line) + 1;
    }
    Console.WriteLine($"discoveries matched to captured creatures by name: {named}");
    foreach (var l in traitLines.OrderBy(k => k.Key[..5]).ThenBy(k => double.TryParse(k.Key.Split('/').ElementAtOrDefault(1)?.Trim().Split('m')[0], out var h) ? h : 0)) Console.WriteLine($"  x{l.Value,-3} {l.Key}");
    foreach (var (year, (hit, miss)) in byYear) Console.WriteLine($"  discovered {year}: predicted {hit}, not {miss}");
    foreach (var k in part.Take(8)) Console.WriteLine($"  partial {k.Key:X}: found {k.Value.Item1}, missing {k.Value.Item2}");
    foreach (var k in all.Take(8)) Console.WriteLine($"  none {k.Key:X}: missing {k.Value.Item2}");
    foreach (var e in examples) Console.WriteLine("  " + e);
}

// TRAITS=1: predicted creature info (PlanetProfileGenerator.CreatureInfoFor) against the owner's captured creatures,
// matched by species name on the same planet. Read-only.
if (Environment.GetEnvironmentVariable("TRAITS") == "1")
{
    List<string> creWords;
    using (var cc = new SqliteConnection($"Data Source={catalog};Mode=ReadOnly"))
    {
        cc.Open();
        var cmd = cc.CreateCommand();
        cmd.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType='MultiToolNameWords' AND i.NameEnglish='CRE'";
        creWords = JsonSerializer.Deserialize<List<string>>((string)cmd.ExecuteScalar()!)!;
    }
    var genderWords = profiles.Creatures!.GenderWords ?? [];
    var planetNames = new Dictionary<(ulong, int), Dictionary<string, PredictedCreature>>();
    int matched = 0, unmatched = 0, sizeOk = 0, rarityOk = 0, genderOk = 0, genderAny = 0, heightOk = 0, variantOk = 0, noTraits = 0;
    var variantMiss = new List<string>();
    var rarityTable = new Dictionary<string, int>();
    var densityTable = new Dictionary<string, int>();
    var rarityPoints = new List<(float H1, int Size, float Scale, string Rarity, string Domain)>();
    var miss = new List<string>();
    using (var u = new SqliteConnection($"Data Source={userDb};Mode=ReadOnly"))
    {
        u.Open();
        var cmd = u.CreateCommand();
        cmd.CommandText = "SELECT system_address, planet_index, name, rarity, gender, size FROM planet_creatures WHERE deleted = 0 AND name <> ''";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            ulong system = ulong.Parse(r.GetString(0), NumberStyles.HexNumber) & ~(0xFUL << 52);
            int index = r.GetInt32(1);
            string name = r.GetString(2), rarity = r.GetString(3), gender = r.GetString(4), size = r.GetString(5);
            if (!planetNames.TryGetValue((system, index), out var byName))
            {
                byName = new Dictionary<string, PredictedCreature>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var (code, galaxy) = Gen(system);
                    var types = PlanetGenerator.Generate(code, galaxy, biomes);
                    var attributes = SystemGenerator.Attributes(code, galaxy);
                    var bodySeeds = SystemGenerator.Bodies(code, galaxy).PlanetSeeds;
                    int i = index - 1;
                    if (i >= 0 && i < types.Length && i < bodySeeds.Length)
                    {
                        var p = PlanetProfileGenerator.Generate(bodySeeds[i], i, types[i], (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy),
                            attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
                        ulong planetAddress = system | ((ulong)index << 52);
                        foreach (var c in p.PredictedCreatures ?? [])
                        {
                            var (sp, ge) = ArtifactX.Core.Procedural.SpeciesNames.FaunaSeeds(c.Seed, c.SpeciesId, planetAddress, c.CreatureType, c.SizeClass);
                            if (ArtifactX.Core.Procedural.SpeciesNames.Fauna(sp, ge, creWords) is string n) byName.TryAdd(n, c);
                        }
                    }
                }
                catch { }
                planetNames[(system, index)] = byName;
            }
            if (!byName.TryGetValue(name, out var pc) || pc.Info is not { } info) { unmatched++; continue; }
            matched++;
            string g1 = pc.Info.Gender1 > 0 && pc.Info.Gender1 <= genderWords.Length ? genderWords[pc.Info.Gender1 - 1] : "?";
            string g2 = pc.Info.Gender2 > 0 && pc.Info.Gender2 <= genderWords.Length ? genderWords[pc.Info.Gender2 - 1] : "?";
            bool sOk = string.Equals(size.Trim(), info.Size, StringComparison.OrdinalIgnoreCase);
            string size2 = info.Size2;
            if (size.Trim().Length == 0 && gender.Length == 0) noTraits++;
            else if ((sOk && SameWord(gender, g1)) || (string.Equals(size.Trim(), size2, StringComparison.OrdinalIgnoreCase) && SameWord(gender, g2)))
                variantOk++;
            else if (variantMiss.Count < 15)
                variantMiss.Add($"{name,-20} {pc.Id,-14} captured [{gender,-11} {size,-17}] predicted [{g1} {info.Size} | {g2} {size2}]");
            if (rarity.Length > 0) rarityPoints.Add((info.Height1, pc.SizeClass, pc.Scale, rarity, pc.Domain));
            if (rarity.Length > 0) { string dk = $"density {pc.Density} -> {rarity}"; densityTable[dk] = densityTable.GetValueOrDefault(dk) + 1; }
            float shownHeight = sOk ? info.Height1 : info.Height2;
            string band = shownHeight > 2.0f ? ">2.0" : shownHeight > 1.1f ? "1.1-2.0" : "<=1.1";
            string rk = $"{pc.Domain,-6} role {pc.Role} {band,-8} captured {rarity}";
            rarityTable[rk] = rarityTable.GetValueOrDefault(rk) + 1;
            if (sOk) sizeOk++;
            if (size.Contains($"{info.Height1.ToString("0.0", CultureInfo.InvariantCulture)}m")) heightOk++;
            if (string.Equals(rarity, info.RarityName, StringComparison.OrdinalIgnoreCase)) rarityOk++;
            if (string.Equals(gender, g1, StringComparison.OrdinalIgnoreCase)) genderOk++;
            if (string.Equals(gender, g1, StringComparison.OrdinalIgnoreCase) || string.Equals(gender, g2, StringComparison.OrdinalIgnoreCase)) genderAny++;
            if ((!sOk || !string.Equals(rarity, info.RarityName, StringComparison.OrdinalIgnoreCase)) && miss.Count < 25)
                miss.Add($"{name,-20} {pc.Id,-14} captured [{rarity,-8} {gender,-11} {size,-17}] predicted [{info.RarityName,-8} {g1}/{g2} {info.Size} | {info.Weight2:0.0}kg / {info.Height2:0.0}m]");
        }
    }
    Console.WriteLine($"traits: captured creatures matched by name {matched} (not matched {unmatched}); size exact {sizeOk}, height {heightOk}, rarity {rarityOk}, gender = first {genderOk}, gender either {genderAny}");
    static bool SameWord(string a, string b) => string.Equals(a.Trim().TrimStart('•', ' ').Replace("ized", "ised"), b.Replace("ized", "ised"), StringComparison.OrdinalIgnoreCase);
    Console.WriteLine($"traits: one whole variant (gender + size) matches {variantOk} of {matched - noTraits} with traits recorded");
    foreach (var m in variantMiss) Console.WriteLine("  variant miss " + m);
    foreach (var t in densityTable.OrderBy(k => k.Key)) Console.WriteLine($"  {t.Key} x{t.Value}");
    foreach (var g in rarityPoints.GroupBy(p => p.Size).OrderBy(g => g.Key))
        Console.WriteLine($"  size class {g.Key}: " + string.Join(", ", g.GroupBy(p => p.Rarity).Select(r => $"{r.Key} x{r.Count()}")));
    File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "rarity_points.tsv"),
        rarityPoints.OrderBy(p => p.H1).Select(p => $"{p.H1:0.000}	{p.Scale:0.000}	{p.Size}	{p.Domain}	{p.Rarity}"));
}
