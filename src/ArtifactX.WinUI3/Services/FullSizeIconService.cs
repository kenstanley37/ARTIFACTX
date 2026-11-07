using ArtifactX.GameData.Services;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// An item's picture at the game's own size, for a closer look (#10). The catalog keeps
/// icons at 128 px (IconBlobs.PngData) but also the texture each came from
/// (IconBlobs.SourceDdsPath), so this reads that texture again from the player's own game
/// files - the same IconExtractionService the catalog build uses, without its resize
/// (up to 1024 px; NMS item icons are 256 px). The PAK index is built once, on first use,
/// off the UI thread. Null when the game or the texture isn't there.
/// </summary>
public static class FullSizeIconService
{
    private static readonly ConcurrentDictionary<string, byte[]?> _pngs = new(StringComparer.OrdinalIgnoreCase);
    private static Task<IconExtractionService?>? _extractor;
    private static readonly object _gate = new();

    public static Task<byte[]?> LoadPngAsync(string gameId)
    {
        string id = CatalogService.NormalizeId(gameId);
        if (_pngs.TryGetValue(id, out var cached)) return Task.FromResult(cached);
        return Task.Run(async () =>
        {
            byte[]? png = null;
            if (TexturePath(id) is string texture && await Extractor() is { } extractor)
                png = extractor.ExtractAndConvert(texture);
            _pngs[id] = png;
            return png;
        });
    }

    private static Task<IconExtractionService?> Extractor()
    {
        lock (_gate)
        {
            return _extractor ??= Task.Run(() =>
            {
                if (GameDataService.Game?.PcbanksPath is not string pcbanks) return null;
                var reader = new PakReaderService();
                var index = new GlobalFileIndexService();
                foreach (var pak in new PakDiscoveryService().Discover(pcbanks))
                {
                    if (pak.FullPath is not string path) continue;
                    try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
                    catch (System.IO.IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
                }
                return (IconExtractionService?)new IconExtractionService(new ExtractionService(), index, targetSize: 1024);
            });
        }
    }

    /// <summary>The texture the item's catalog icon was made from; the plain Icon before a HeroIcon.</summary>
    private static string? TexturePath(string gameId)
    {
        if (CatalogService.ResolveDbPath() is not string dbPath) return null;
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT b.SourceDdsPath FROM Items i
            JOIN Icons ic ON ic.ItemId = i.Id
            JOIN IconBlobs b ON b.Id = ic.IconBlobId
            WHERE i.GameId = $id
            ORDER BY ic.SourceField = 'Icon' DESC
            LIMIT 1";
        command.Parameters.AddWithValue("$id", gameId);
        return command.ExecuteScalar() as string;
    }
}
