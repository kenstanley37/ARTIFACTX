using Microsoft.Data.Sqlite;

namespace ArtifactX.GameData.Services;

/// <summary>Publishes a completed catalog through SQLite's transactional backup.
/// Replacing an open WAL database with File.Move leaves the destination's older
/// WAL attached to the new file. Confirmed 2026-10-31: the main file held format
/// 52 while normal readers saw format 51. Backup includes the source WAL and
/// updates the destination through SQLite, retaining active reader safety.</summary>
public static class CatalogPublication
{
    public static void Publish(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false,
        }.ToString());
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath, Pooling = false,
        }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }
}
