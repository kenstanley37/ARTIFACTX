using libMBIN;

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
