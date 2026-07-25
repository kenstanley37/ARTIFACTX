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

    /// <summary>Searches the catalog for items whose display name contains the
    /// query, restricted to the given MBIN table names (e.g. "GcTechnologyTable"
    /// for the Exosuit tech grid, "GcProductTable"/"GcSubstanceTable" for cargo).
    /// allowedUsageCategories further restricts to specific equipment slots (e.g.
    /// "Suit"/"All" for the Exosuit tech grid) - pass null to skip this filter
    /// entirely, which is required for Product/Substance searches since those rows
    /// have no UsageCategory at all. Returns bare GameId + elv-style CategoryLabel
    /// only - callers should follow up with WarmCacheAsync(results...GameId) and
    /// TryGet() for display name/icon, reusing the existing cache rather than
    /// duplicating that lookup here.</summary>
    public static async Task<List<CatalogSearchResult>> SearchAsync(string query, string[] allowedTemplateTypes, string[]? allowedUsageCategories = null, int maxResults = 30)
    {
        if (string.IsNullOrWhiteSpace(query) || allowedTemplateTypes.Length == 0)
            return new();

        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QuerySearch(dbPath, query.Trim(), allowedTemplateTypes, allowedUsageCategories, maxResults));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] SearchAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<CatalogSearchResult> QuerySearch(string dbPath, string query, string[] allowedTemplateTypes, string[]? allowedUsageCategories, int maxResults)
    {
        var results = new List<CatalogSearchResult>();
        var seen = new HashSet<string>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        string typePlaceholders = string.Join(",", allowedTemplateTypes.Select((_, i) => $"@t{i}"));

        // Only join on UsageCategory when a filter was actually requested - Product/
        // Substance rows have no UsageCategory at all, so forcing this clause for
        // every search would silently exclude every cargo item.
        string usageClause = "";
        if (allowedUsageCategories is { Length: > 0 })
        {
            string usagePlaceholders = string.Join(",", allowedUsageCategories.Select((_, i) => $"@u{i}"));
            usageClause = $"AND i.UsageCategory IN ({usagePlaceholders})";
        }

        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT i.GameId, c.TemplateType, i.MaxStackSize
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE i.NameEnglish IS NOT NULL
              AND c.TemplateType IN ({typePlaceholders})
              AND i.NameEnglish LIKE @q
              {usageClause}
            LIMIT @max";

        for (int i = 0; i < allowedTemplateTypes.Length; i++)
            command.Parameters.AddWithValue($"@t{i}", allowedTemplateTypes[i]);

        if (allowedUsageCategories is { Length: > 0 })
        {
            for (int i = 0; i < allowedUsageCategories.Length; i++)
                command.Parameters.AddWithValue($"@u{i}", allowedUsageCategories[i]);
        }

        command.Parameters.AddWithValue("@q", $"%{query}%");
        command.Parameters.AddWithValue("@max", maxResults);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string gameId = reader.GetString(0);
            if (!seen.Add(gameId)) continue;

            results.Add(new CatalogSearchResult
            {
                GameId = gameId,
                CategoryLabel = ElvLabelFor(reader.GetString(1)),
                MaxStackSize = reader.IsDBNull(2) ? null : reader.GetInt32(2)
            });
        }
        return results;
    }

    // Maps the MBIN source table to the "elv" string the save file itself uses
    // per-slot (Vn8.elv: "Technology"/"Product"/"Substance") - add new tables here
    // as they're classified, rather than guessing a default for unknown ones.
    private static string ElvLabelFor(string templateType) => templateType switch
    {
        "GcTechnologyTable" => "Technology",
        "GcSubstanceTable" => "Substance",
        _ => "Product"
    };

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

    /// <summary>Multi-tool "Type" (base model) options, sourced from the
    /// MultiToolTypes category CatalogBuildService discovers by filename rule
    /// (see that service for how) rather than a curated list anywhere in the
    /// app. GameId is the model scene path itself; NameEnglish is the derived
    /// display name. Rebuilding the catalog after a game update refreshes this
    /// automatically - nothing in the WinUI3 project needs to change.</summary>
    public static async Task<List<(string DisplayName, string ScenePath)>> GetMultiToolTypesAsync()
    {
        string? dbPath = ResolveDbPath();
        if (dbPath is null) return new();

        try
        {
            return await Task.Run(() => QueryMultiToolTypes(dbPath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CatalogService] GetMultiToolTypesAsync failed: {ex.Message}");
            return new();
        }
    }

    private static List<(string DisplayName, string ScenePath)> QueryMultiToolTypes(string dbPath)
    {
        var results = new List<(string, string)>();

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT i.NameEnglish, i.GameId
            FROM Items i
            JOIN Categories c ON c.Id = i.CategoryId
            WHERE c.TemplateType = 'MultiToolTypes'
            ORDER BY i.NameEnglish";

        using var reader = command.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetString(1)));

        return results;
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