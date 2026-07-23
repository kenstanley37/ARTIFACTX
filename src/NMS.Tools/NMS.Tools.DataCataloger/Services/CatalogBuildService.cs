using System.Reflection;
using libMBIN;
using Microsoft.EntityFrameworkCore;
using NMS.Tools.DataCataloger.Data;
using NMS.Tools.DataCataloger.Models;
using NMS.Tools.DataCataloger.Services.Interfaces;

namespace NMS.Tools.DataCataloger.Services;

public class CatalogBuildService
{
    private readonly IPakDiscoveryService _discovery;
    private readonly IPakReaderService _pakReader;
    private readonly ExtractionService _extraction;

    // NMS has shipped several generations of loc tables across its many expansions
    // (loc1, loc4, loc5, ... but also "update3" - Hello Games doesn't keep a single
    // consistent prefix), each adding keys for newer content. Some groups use
    // "_english", newer ones use "_usenglish" instead. We need ALL of them merged, or
    // names for anything added after whichever generation happened to be first simply
    // won't resolve. Matching any alphabetic prefix (not hardcoding "loc") so a future
    // third naming scheme doesn't silently get skipped the same way "update3" was.
    private static readonly System.Text.RegularExpressions.Regex LocFilePattern =
        new(@"language/nms_[a-z]+\d+_(us)?english\.mbin$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public CatalogBuildService(IPakDiscoveryService discovery, IPakReaderService pakReader, ExtractionService extraction)
    {
        _discovery = discovery;
        _pakReader = pakReader;
        _extraction = extraction;
    }

    public void Run(string pcbanksPath, string dbOutputPath, int iconTargetSize = 128)
    {
        var paks = _discovery.Discover(pcbanksPath);
        LogService.Write($"CatalogBuild: found {paks.Count} PAK files.");

        // -------------------------------------------------------------
        // Phase 0: index every file in every PAK (needed later for icons,
        // which usually live in a different PAK than the table that
        // references them), while also caching each PAK's (header, entries)
        // so we don't re-parse the index twice.
        // -------------------------------------------------------------
        var fileIndex = new GlobalFileIndexService();
        var pakData = new List<(string PakPath, PakHeader Header, IReadOnlyList<PakEntry> Entries)>();

        foreach (var pak in paks)
        {
            try
            {
                var header = _pakReader.ReadHeader(pak.FullPath);
                var entries = _pakReader.Read(pak.FullPath);

                fileIndex.Add(pak.FullPath, header, entries);
                pakData.Add((pak.FullPath, header, entries));
            }
            catch (Exception ex)
            {
                LogService.Write($"CatalogBuild: failed to index {pak.FileName}: {ex.Message}");
            }
        }

        LogService.Write($"CatalogBuild: indexed {fileIndex.Count} files across all PAKs.");

        // -------------------------------------------------------------
        // Phase 1: resolve localisation FIRST. Row classification needs the
        // full loc-key set to decide which string fields are "name-shaped",
        // so this has to happen before the main sweep, not during it.
        // Merge every loc-generation's English file, not just one.
        // -------------------------------------------------------------
        var englishLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var locEntryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pakPath, header, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName) || !LocFilePattern.IsMatch(entry.FileName))
                    continue;

                locEntryPaths.Add(entry.FileName);

                var locTemplate = DecodeMbin(pakPath, entry, header);
                if (locTemplate == null) continue;

                var thisLookup = LocalisationService.BuildEnglishLookup(locTemplate);
                foreach (var kvp in thisLookup)
                    englishLookup[kvp.Key] = kvp.Value; // later files can safely override earlier ones

                LogService.Write($"CatalogBuild: merged {thisLookup.Count} entries from {entry.FileName} (total now {englishLookup.Count}).");
            }
        }

        if (englishLookup.Count == 0)
            LogService.Write("CatalogBuild: WARNING - no localisation table found. Names will be stored as loc keys only.");

        // -------------------------------------------------------------
        // Phase 2: classify every MBIN across every PAK. Skip the loc table
        // itself - it technically matches our own "has a List<T> with an Id
        // field" heuristic, but it isn't a gameplay catalog.
        // -------------------------------------------------------------
        var iconService = new IconExtractionService(_extraction, fileIndex, iconTargetSize);
        var iconBlobCache = new Dictionary<string, IconBlob>(StringComparer.OrdinalIgnoreCase);
        var categories = new List<CatalogCategory>();
        int totalRows = 0, totalIcons = 0;

        foreach (var (pakPath, header, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName) ||
                    !entry.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (locEntryPaths.Contains(entry.FileName))
                    continue;

                NMSTemplate? template;
                try
                {
                    template = DecodeMbin(pakPath, entry, header);
                }
                catch
                {
                    continue; // already logged inside DecodeMbin
                }

                if (template == null) continue;

                bool isClassified;
                FieldInfo listField = null!;
                Type rowType = null!;
                try
                {
                    isClassified = CatalogClassifier.TryClassify(template, out listField, out rowType);
                }
                catch (Exception ex)
                {
                    LogService.Write($"CatalogBuild: TryClassify threw for {entry.FileName}: {ex}");
                    continue;
                }

                if (!isClassified)
                    continue;

                var category = new CatalogCategory
                {
                    TemplateType = template.GetType().Name,
                    RowType = rowType.Name,
                    SourceMbinPath = entry.FileName
                };

                List<ClassifiedRow> rows;
                try
                {
                    rows = CatalogClassifier.ExtractRows(template, listField);
                }
                catch (Exception ex)
                {
                    LogService.Write($"CatalogBuild: ExtractRows threw for {entry.FileName}: {ex}");
                    continue;
                }

                foreach (var row in rows)
                {
                    if (string.IsNullOrEmpty(row.GameId)) continue;

                    var item = new CatalogItem
                    {
                        GameId = row.GameId,
                        NameLocKey = row.NameLocKey,
                        NameEnglish = row.NameLocKey != null && englishLookup.TryGetValue(row.NameLocKey, out var n) ? n : null,
                        NameLowerLocKey = row.NameLowerLocKey,
                        NameLowerEnglish = row.NameLowerLocKey != null && englishLookup.TryGetValue(row.NameLowerLocKey, out var nl) ? nl : null,
                        DescriptionLocKey = row.DescriptionLocKey,
                        DescriptionEnglish = row.DescriptionLocKey != null && englishLookup.TryGetValue(row.DescriptionLocKey, out var d) ? d : null,
                    };

                    foreach (var (sourceField, texturePath) in row.Icons)
                    {
                        if (!iconBlobCache.TryGetValue(texturePath, out var blob))
                        {
                            byte[]? pngBytes = iconService.ExtractAndConvert(texturePath);
                            if (pngBytes == null) continue; // extraction/decode failed - skip, don't create an empty blob

                            blob = new IconBlob { SourceDdsPath = texturePath, PngData = pngBytes };
                            iconBlobCache[texturePath] = blob;
                        }

                        item.Icons.Add(new IconAsset
                        {
                            SourceField = sourceField,
                            IconBlob = blob
                        });

                        totalIcons++;
                    }

                    category.Items.Add(item);
                    totalRows++;
                }

                if (category.Items.Count > 0)
                    categories.Add(category);
            }
        }

        LogService.Write($"CatalogBuild: classified {categories.Count} tables, {totalRows} rows, {totalIcons} icon references.");

        // -------------------------------------------------------------
        // Phase 3: write everything to a fresh SQLite database.
        // -------------------------------------------------------------
        if (File.Exists(dbOutputPath))
            File.Delete(dbOutputPath);

        using var db = new CatalogDbContext(dbOutputPath);
        db.Database.EnsureCreated();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var kvp in englishLookup)
            db.LocalizedTexts.Add(new LocalizedText { LocKey = kvp.Key, Language = "en", Text = kvp.Value });

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            LogService.Write($"CatalogBuild: failed to save localisation texts: {ex.Message}");
        }

        int savedCategories = 0, savedItems = 0;

        foreach (var category in categories)
        {
            db.Categories.Add(category);

            try
            {
                db.SaveChanges();
                savedCategories++;
                savedItems += category.Items.Count;
            }
            catch (Exception ex)
            {
                LogService.Write($"CatalogBuild: failed to save category {category.SourceMbinPath}: {ex.Message}");
                DetachCategoryGraph(db, category);
            }
        }

        LogService.Write($"CatalogBuild: wrote {savedCategories}/{categories.Count} categories ({savedItems} items) to {dbOutputPath}.");
    }

    /// <summary>
    /// Detaches just the failed category's own entities (category/items/icon-assets) so the
    /// next SaveChanges isn't poisoned by it - but deliberately leaves each IconAsset's
    /// IconBlob tracked, since that blob may be shared with other categories that already
    /// saved successfully. A full ChangeTracker.Clear() here would silently un-track those
    /// too, causing duplicate-insert failures the next time a later category reuses them.
    /// </summary>
    private static void DetachCategoryGraph(CatalogDbContext db, CatalogCategory category)
    {
        db.Entry(category).State = EntityState.Detached;

        foreach (var item in category.Items)
        {
            db.Entry(item).State = EntityState.Detached;

            foreach (var icon in item.Icons)
                db.Entry(icon).State = EntityState.Detached;
        }
    }

    private NMSTemplate? DecodeMbin(string pakPath, PakEntry entry, PakHeader header)
    {
        byte[]? bytes = _extraction.ExtractEntryBytes(pakPath, entry, header);
        if (bytes == null || bytes.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            return mbin.GetData();
        }
        catch (Exception ex)
        {
            LogService.Write($"CatalogBuild: failed to decode {entry.FileName}: {ex.Message}");
            return null;
        }
    }
}