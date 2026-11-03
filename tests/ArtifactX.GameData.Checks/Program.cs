using libMBIN;
using ArtifactX.Core.Recipes;
using ArtifactX.GameData;
using ArtifactX.GameData.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ArtifactX.Core.Unlocks;

if (args.Length == 3 && args[0] == "--industrial-cleanup-catalog-check")
{
    string output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("Use a new scratch catalog.");
    using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
    CatalogSteps.AddIndustrialCleanup(output, args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
    connection.Open();
    using var query = connection.CreateCommand();
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='INDUSTRIALCLEANUP'";
    var guide = JsonSerializer.Deserialize<ArtifactX.Core.IndustrialCleanup.IndustrialCleanupDefinition>((string)query.ExecuteScalar()!)!;
    if (guide.Title != "Industrial Cleanup" || guide.Instructions.Length < 4 || guide.Counters.Length != 5
        || !guide.Counters.Select(c => c.StatId).ToHashSet().SetEquals(new[] { "CARGO_VALUE", "CARGO_SCRAP", "CARGO_TOX", "CARGO_RAD", "CARGO_EXP" })
        || !guide.FindingText.Contains("Exocraft Radar") || !guide.Instructions.Any(i => i.Text.Contains("maximum resource recovery")))
        throw new InvalidOperationException("Cleanup definitions differ from inspected build 184271.");
    Console.WriteLine("PASS: normal cleanup tutorial, discovery instructions and five mission-defined lifetime counters.");
    return;
}

if (args.Length == 3 && args[0] == "--title-unlocks-catalog-check")
{
    string output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("The check requires a new scratch catalog.");
    using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
    CatalogSteps.AddTitleUnlocks(output, args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
    connection.Open();
    using var query = connection.CreateCommand();
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='TITLEUNLOCKS'";
    var rules = JsonSerializer.Deserialize<TitleUnlockRule[]>((string)query.ExecuteScalar()!)!;
    // Installed build 184271 references, inspected 2026-11-02. These assert
    // declared requirements, not that meeting them grants a cosmetic in game.
    foreach (var (special, stat, amount) in new[]
    {
        ("TRUCK_CAB02", "CARGO_DESTROYED", 100f),
        ("TRUCK_BODY03", "CARGO_DESTROYED", 200f),
        ("TRUCK_WHEELS02", "CARGO_SCRAP", 75f),
        ("TRUCK_WHEELS03", "CARGO_EXP", 50f),
        ("TRUCK_WHEELS06", "CARGO_TOX", 50f),
        ("TRUCK_WHEELS07", "CARGO_RAD", 50f)
    })
    {
        var rule = rules.Single(r => r.SpecialIds.Contains(special));
        if (rule.StatId != stat || rule.StatValue != amount || string.IsNullOrWhiteSpace(rule.Title)
            || string.IsNullOrWhiteSpace(rule.Requirement))
            throw new InvalidOperationException($"Title reward mapping differs for {special}.");
    }
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='VEHICLECUSTOMISATION'";
    var options = JsonSerializer.Deserialize<VehicleCustomisationOption[]>((string)query.ExecuteScalar()!)!;
    var cabin = options.Single(o => o.SpecialId == "TRUCK_CAB02");
    if (cabin.Vehicle != 2 || cabin.Group != "CABIN" || cabin.Name != "Widescreen Cabin"
        || !options.Any(o => o.Vehicle == 2 && o.Group == "PAINT" && o.SpecialId == "TRUCK_PAINT02")
        || options.GroupBy(o => (o.Vehicle, o.Group, o.OptionId)).Any(g => g.Count() != 1)
        || options.Any(o => string.IsNullOrWhiteSpace(o.Name) || string.IsNullOrWhiteSpace(o.Group)))
        throw new InvalidOperationException("Vehicle customization associations or localization differ from references.");
    Console.WriteLine($"PASS: {rules.Length} title reward rules and {options.Length} vehicle choices; six Colossus references and localized instructions match.");
    return;
}

if (args.Length is 3 or 4 && args[0] == "--extraction-site-check")
{
    ExtractionCatalogChecks.Sites(args[1], args[2], args.Length == 4 ? args[3] : null);
    return;
}

if (args.Length == 3 && args[0] == "--extraction-world-check")
{
    ExtractionCatalogChecks.World(args[1], args[2]);
    return;
}

if (args.Length == 3 && args[0] is "--food-catalog-check" or "--creature-source-catalog-check" or "--extraction-catalog-check")
{
    string output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("The check requires a new scratch catalog.");
    using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
    CatalogSteps.AddPlanetGeneration(output, args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
    connection.Open(); using var query = connection.CreateCommand();
    if (args[0] == "--extraction-catalog-check")
    {
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='EXTRACTIONTABLES'";
        var extractionTables = JsonSerializer.Deserialize<ExtractionTables>((string)query.ExecuteScalar()!)!;
        if (extractionTables.Hotspots.Length != 6 || extractionTables.BiomeGases.Length != 17 || extractionTables.Substances.Length == 0 || extractionTables.Networks.Length == 0)
            throw new InvalidOperationException("Extraction tables are incomplete.");
        var substance = extractionTables.Substances.Single(s => s.Id == "LUSH1");
        if (ExtractionStock.DisplayAmount(10033886, substance) != 6750)
            throw new InvalidOperationException("Extracted Paraffinium yield differs from the observed stock.");
        if (extractionTables.Networks.Single(n => n.PartId == "U_EXTRACTOR_S").HotspotKind != "Mineral"
            || extractionTables.Networks.Single(n => n.PartId == "U_GASEXTRACTOR").HotspotKind != "Gas"
            || extractionTables.Networks.Single(n => n.PartId == "U_SILO_S").Storage != 1440000)
            throw new InvalidOperationException("Installed extraction equipment definitions differ from references.");
        Console.WriteLine($"PASS: extracted {extractionTables.Hotspots.Length} hotspot categories, {extractionTables.BiomeGases.Length} biome gases, {extractionTables.Substances.Length} yields and {extractionTables.Networks.Length} resource-link definitions.");
        return;
    }
    if (args[0] == "--creature-source-catalog-check")
    {
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='CREATUREPRODUCTS'";
        var sources = JsonSerializer.Deserialize<CreatureProductSource[]>((string)query.ExecuteScalar()!)!;
        var meat = sources.Where(s => s.ItemId == "FOOD_M_DIGGER").ToArray();
        if (meat.Length != 2 || meat.Any(s => s.Method != "Hunt" || s.Habitat != "Ground")
            || !meat.Select(s => s.CreatureKind).Order().SequenceEqual(new[] { "Plough", "ProtoDigger" }))
            throw new InvalidOperationException("Dirty Meat creature source mapping differs from the installed reference.");
        if (!sources.Any(s => s.Method == "Harvest") || sources.Any(s => string.IsNullOrWhiteSpace(s.ItemId)))
            throw new InvalidOperationException("Creature product extraction is incomplete.");
        Console.WriteLine($"PASS: extracted {sources.Length} creature product mappings; Dirty Meat hunting and habitats match installed tables.");
        return;
    }
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='FOODINGESTOR'";
    var tables = JsonSerializer.Deserialize<FoodIngestorTables>((string)query.ExecuteScalar()!)!;
    if (tables.Foods.Length == 0 || tables.BaseDuration <= 0 || tables.Foods.Any(f => !float.IsFinite(FoodIngestorBonus.Calculate(f))))
        throw new InvalidOperationException("Invalid food bonus extraction.");
    Console.WriteLine($"PASS: catalog build extracted {tables.Foods.Length} food bonus definitions and duration from the installed game.");
    return;
}

// Synthetic binary records contain no extracted game content.
const uint unknownHash = 0xFFFFFFFF;
if (NMSTemplate.GetTemplateType(unknownHash) is not null)
    throw new InvalidOperationException("Test hash is no longer unknown");
using var emptyReader = new BinaryReader(new MemoryStream());
ExpectTemplateError(() => NMSTemplate.DeserializeBinaryTemplate(emptyReader, (string)null!), "missing name");
using (var mbin = new MBINFile(new MemoryStream(new byte[32])))
{
    mbin.Header = new MBINHeader { NameHash = unknownHash };
    ExpectTemplateError(() => mbin.GetData(), "FFFFFFFF");
}
if (NMSTemplate.TemplateFromNameHash(unknownHash) is not null
    || NMSTemplate.DeserializeBinaryTemplate(emptyReader, unknownHash) is not null)
    throw new InvalidOperationException("Unknown hash unexpectedly resolved");

foreach (bool empty in new[] { false, true })
{
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
    {
        writer.Write(16L); writer.Write(1); writer.Write(1u); // Generic list header.
        writer.Write(empty ? 0L : 16L); writer.Write(unknownHash); writer.Write(0xEEEEEE01u);
    }
    stream.Position = 0;
    using var reader = new BinaryReader(stream);
    if (!empty) ExpectTemplateError(() => NMSTemplate.DeserializeGenericList(reader, new NMSTemplate()), "FFFFFFFF");
    else if (NMSTemplate.DeserializeGenericList(reader, new NMSTemplate()) is not { Count: 1 } list
        || list[0].GetType().Name != "EmptyNode")
        throw new InvalidOperationException("Empty component placeholder was not preserved");
}
Console.WriteLine("PASS: missing name, unknown hash lookup, unknown component diagnostic and empty component placeholder.");

static void ExpectTemplateError(Action action, string diagnostic)
{
    try { action(); }
    catch (DeserializeTemplateException ex) when (ex.Message.Contains(diagnostic, StringComparison.Ordinal)) { return; }
    throw new InvalidOperationException($"Expected template error containing {diagnostic}");
}


// Keep both a WAL reader and a source writer open across publication. These are
// synthetic catalogs; no installed catalog or player database is changed.
string folder = Path.Combine(Path.GetTempPath(), "ArtifactX-catalog-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    string target = Path.Combine(folder, "catalog.sqlite");
    string source = Path.Combine(folder, "next.sqlite");
    using var reader = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={target};Pooling=False");
    using var writer = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={source};Pooling=False");
    reader.Open(); writer.Open();
    void Execute(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
    }
    Execute(reader, "PRAGMA journal_mode=WAL; CREATE TABLE Version(Value INTEGER); INSERT INTO Version VALUES(51)");
    Execute(writer, "PRAGMA journal_mode=WAL; CREATE TABLE Version(Value INTEGER); INSERT INTO Version VALUES(52)");
    ArtifactX.GameData.Services.CatalogPublication.Publish(source, target);
    using var query = reader.CreateCommand(); query.CommandText = "SELECT Value FROM Version";
    if (Convert.ToInt32(query.ExecuteScalar()) != 52)
        throw new InvalidOperationException("Open catalog connection retained stale WAL data after publication");
    using var fresh = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={target};Mode=ReadOnly;Pooling=False");
    fresh.Open(); using var freshQuery = fresh.CreateCommand(); freshQuery.CommandText = "SELECT Value FROM Version";
    if (Convert.ToInt32(freshQuery.ExecuteScalar()) != 52)
        throw new InvalidOperationException("Fresh catalog connection retained stale WAL data after publication");
    Execute(writer, "UPDATE Version SET Value=53");
    using (var snapshot = reader.BeginTransaction(deferred: true))
    {
        query.Transaction = snapshot;
        if (Convert.ToInt32(query.ExecuteScalar()) != 52)
            throw new InvalidOperationException("Reader snapshot did not start on the published catalog");
        ArtifactX.GameData.Services.CatalogPublication.Publish(source, target);
        if (Convert.ToInt32(query.ExecuteScalar()) != 52)
            throw new InvalidOperationException("Publication disrupted an active reader snapshot");
        snapshot.Commit();
    }
    query.Transaction = null;
    if (Convert.ToInt32(query.ExecuteScalar()) != 53 || Convert.ToInt32(freshQuery.ExecuteScalar()) != 53)
        throw new InvalidOperationException("Readers did not see the next catalog after ending the snapshot");
    Console.WriteLine("PASS: catalog publication updates open and fresh readers, includes source WAL changes and preserves active reader snapshots.");
}
finally { Directory.Delete(folder, recursive: true); }
