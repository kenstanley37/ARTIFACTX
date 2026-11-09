using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;

namespace ArtifactX.GameData;

/// <summary>The catalog's extra build steps (run after CatalogBuildService's main
/// build, each adding one kind of data). Moved from DataCataloger's command
/// line (2026-10-14) so the app can run them on the player's PC.
///
/// Split by domain into CatalogSteps.*.cs partial files; this file holds the
/// shared PAK index, MBIN decoding, English localisation cache, and DB helpers
/// so a full catalog build indexes the PAK files and English strings once
/// instead of once per step.</summary>
public static partial class CatalogSteps
{
    private static readonly object CacheLock = new();
    private static string? _cachedPcbanksPath;
    private static List<(string PakPath, PakHeader Header, IReadOnlyList<PakEntry> Entries)>? _cachedPaks;
    private static GlobalFileIndexService? _cachedIndex;
    private static Dictionary<string, string>? _cachedMergedEnglish;
    private static readonly ExtractionService SharedExtraction = new();

    /// <summary>Releases cached PAK headers, file index, and localisation strings after a build completes.</summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            _cachedPcbanksPath = null;
            _cachedPaks = null;
            _cachedIndex = null;
            _cachedMergedEnglish = null;
        }
    }

    private static IReadOnlyList<(string PakPath, PakHeader Header, IReadOnlyList<PakEntry> Entries)> GetPakEntries(string pcbanksPath)
    {
        lock (CacheLock)
        {
            if (_cachedPaks is not null && string.Equals(_cachedPcbanksPath, pcbanksPath, StringComparison.OrdinalIgnoreCase))
                return _cachedPaks;

            var reader = new PakReaderService();
            var paks = new List<(string PakPath, PakHeader Header, IReadOnlyList<PakEntry> Entries)>();
            var index = new GlobalFileIndexService();
            foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
            {
                if (string.IsNullOrEmpty(pak.FullPath)) continue;
                try
                {
                    var header = reader.ReadHeader(pak.FullPath);
                    var entries = reader.Read(pak.FullPath);
                    paks.Add((pak.FullPath, header, entries));
                    index.Add(pak.FullPath, header, entries);
                }
                catch
                {
                    // Match the catalog's treatment of unreadable PAKs.
                }
            }

            _cachedPcbanksPath = pcbanksPath;
            _cachedPaks = paks;
            _cachedIndex = index;
            _cachedMergedEnglish = null;
            return paks;
        }
    }

    private static GlobalFileIndexService GetIndex(string pcbanksPath)
    {
        lock (CacheLock)
        {
            _ = GetPakEntries(pcbanksPath);
            return _cachedIndex!;
        }
    }

    private static byte[]? ExtractBytes(string pcbanksPath, string virtualPath) =>
        GetIndex(pcbanksPath).TryFind(virtualPath, out var pakPath, out var entry, out var header)
            ? SharedExtraction.ExtractEntryBytes(pakPath, entry, header)
            : null;

    private static NMSTemplate? DecodeMbin(string pcbanksPath, string virtualPath)
    {
        try
        {
            if (ExtractBytes(pcbanksPath, virtualPath) is not { Length: > 0 } bytes) return null;
            using var mbin = new MBINFile(new MemoryStream(bytes));
            mbin.Load();
            return mbin.GetData();
        }
        catch
        {
            return null;
        }
    }

    private static T? DecodeFirstMatch<T>(string pcbanksPath, Func<string, bool> fileNamePredicate) where T : class
    {
        foreach (var (pakPath, header, entries) in GetPakEntries(pcbanksPath))
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName) || !fileNamePredicate(entry.FileName)) continue;
                try
                {
                    byte[]? bytes = SharedExtraction.ExtractEntryBytes(pakPath, entry, header);
                    if (bytes is null || bytes.Length == 0) continue;
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    if (mbin.GetData() is T typed) return typed;
                }
                catch
                {
                    // Skip unreadable entry and continue searching.
                }
            }
        }
        return null;
    }

    /// <summary>Merged English localisation lookup across all <c>language/nms_*_english.mbin</c> files
    /// (excluding <c>usenglish</c>), cached for the current <paramref name="pcbanksPath"/>.</summary>
    private static IReadOnlyDictionary<string, string> GetMergedEnglishLookup(string pcbanksPath)
    {
        lock (CacheLock)
        {
            if (_cachedMergedEnglish is not null && string.Equals(_cachedPcbanksPath, pcbanksPath, StringComparison.OrdinalIgnoreCase))
                return _cachedMergedEnglish;
        }

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pakPath, header, entries) in GetPakEntries(pcbanksPath))
        {
            var locEntries = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase));

            foreach (var entry in locEntries)
            {
                try
                {
                    byte[]? bytes = SharedExtraction.ExtractEntryBytes(pakPath, entry, header);
                    if (bytes is null || bytes.Length == 0) continue;
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    if (mbin.GetData() is { } locTemplate)
                    {
                        foreach (var kv in LocalisationService.BuildEnglishLookup(locTemplate))
                            lookup.TryAdd(kv.Key, kv.Value);
                    }
                }
                catch
                {
                    // Skip unreadable loc entry.
                }
            }
        }

        lock (CacheLock)
        {
            if (string.Equals(_cachedPcbanksPath, pcbanksPath, StringComparison.OrdinalIgnoreCase))
                _cachedMergedEnglish = lookup;
        }
        return lookup;
    }

    /// <summary>Replaces a category's rows (idempotent re-runs).</summary>
    private static void Replace(CatalogDbContext db, string templateType, string rowType, string source, List<CatalogItem> rows)
    {
        var existing = db.Categories.FirstOrDefault(c => c.TemplateType == templateType);
        if (existing != null)
        {
            db.Items.RemoveRange(db.Items.Where(i => i.CategoryId == existing.Id));
            db.Categories.Remove(existing);
            db.SaveChanges();
        }
        if (rows.Count == 0) return;
        var category = new CatalogCategory { TemplateType = templateType, RowType = rowType, SourceMbinPath = source };
        db.Categories.Add(category);
        db.SaveChanges();
        foreach (var row in rows) row.CategoryId = category.Id;
        db.Items.AddRange(rows);
        db.SaveChanges();
    }
}
