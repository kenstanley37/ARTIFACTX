using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Media.Imaging;
using NMS.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace NMS.WinUI3.Services;

public static class CatalogService
{
    private static readonly Dictionary<string, CatalogEntry?> Cache = new();
    private static string? _dbPath;
    private static bool _pathChecked;

    public static bool IsAvailable => ResolveDbPath() is not null;

    public static CatalogEntry? TryGet(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return null;
        string key = gameId.TrimStart('^');
        return Cache.TryGetValue(key, out var entry) ? entry : null;
    }

    public static async Task WarmCacheAsync(IEnumerable<string?> gameIds)
    {
        var toFetch = gameIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!.TrimStart('^'))
            .Distinct()
            .Where(id => !Cache.ContainsKey(id))
            .ToList();

        Debug.WriteLine($"[CatalogService] WarmCacheAsync: {toFetch.Count} new ids to look up (of the ids passed in).");

        if (toFetch.Count == 0) return;

        string? dbPath = ResolveDbPath();
        if (dbPath is null)
        {
            Debug.WriteLine("[CatalogService] No catalog file found - all ids will fall back to raw display.");
            foreach (var id in toFetch) Cache[id] = null;
            return;
        }

        Dictionary<string, (string DisplayName, byte[]? IconPng)> rows;
        try
        {
            rows = await Task.Run(() => QueryRows(dbPath, toFetch));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] Query failed against {dbPath}: {ex.Message}");
            foreach (var id in toFetch) Cache[id] = null;
            return;
        }

        Debug.WriteLine($"[CatalogService] Query returned {rows.Count} matches out of {toFetch.Count} requested ids.");

        foreach (var id in toFetch)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                Cache[id] = null;
                continue;
            }

            BitmapImage? icon = null;
            if (row.IconPng is { Length: > 0 })
            {
                try
                {
                    icon = new BitmapImage();
                    using var stream = new InMemoryRandomAccessStream();
                    await stream.WriteAsync(row.IconPng.AsBuffer());
                    stream.Seek(0);
                    await icon.SetSourceAsync(stream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CatalogService] Icon decode failed for '{id}': {ex.Message}");
                    icon = null;
                }
            }

            Cache[id] = new CatalogEntry { DisplayName = row.DisplayName, Icon = icon };
        }
    }

    private static Dictionary<string, (string DisplayName, byte[]? IconPng)> QueryRows(string dbPath, List<string> gameIds)
    {
        var result = new Dictionary<string, (string, byte[]?)>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        string placeholders = string.Join(",", gameIds.Select((_, i) => $"@p{i}"));
        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT i.GameId, i.NameEnglish, b.PngData
            FROM Items i
            LEFT JOIN Icons ic ON ic.ItemId = i.Id
            LEFT JOIN IconBlobs b ON b.Id = ic.IconBlobId
            WHERE i.NameEnglish IS NOT NULL AND i.GameId IN ({placeholders})";

        for (int i = 0; i < gameIds.Count; i++)
            command.Parameters.AddWithValue($"@p{i}", gameIds[i]);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string gameId = reader.GetString(0);
            if (result.ContainsKey(gameId)) continue;

            string name = reader.GetString(1);
            byte[]? png = reader.IsDBNull(2) ? null : (byte[])reader["PngData"];
            result[gameId] = (name, png);
        }
        return result;
    }

    private static string? ResolveDbPath()
    {
        if (_pathChecked) return _dbPath;
        _pathChecked = true;

        string candidate = Path.Combine(AppContext.BaseDirectory, "Data", "nms_catalog.sqlite");
        _dbPath = File.Exists(candidate) ? candidate : null;

        Debug.WriteLine(_dbPath is not null
            ? $"[CatalogService] Catalog found at: {_dbPath}"
            : $"[CatalogService] Catalog NOT found. Checked: {candidate}");

        return _dbPath;
    }
}