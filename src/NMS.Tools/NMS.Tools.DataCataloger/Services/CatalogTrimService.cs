using Microsoft.EntityFrameworkCore;
using NMS.Tools.DataCataloger.Data;
using NMS.Tools.DataCataloger.Models;

namespace NMS.Tools.DataCataloger.Services;

/// <summary>
/// Reads the full working catalog (every extracted row - ~1.25M of them,
/// most with no display name at all) and writes a small distribution copy
/// containing only what the shipped app ever queries: items with a real
/// name, and the icons/categories those items actually reference.
/// LocalizedTexts is dropped entirely - NameEnglish is already resolved to
/// plain text on each Item row, nothing downstream needs the raw loc-key
/// table. This is the file that ships in the installer, not the working
/// catalog itself.
/// </summary>
public static class CatalogTrimService
{
    public static void Trim(string sourceDbPath, string destDbPath)
    {
        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Source catalog not found - run the full build first.", sourceDbPath);

        if (File.Exists(destDbPath))
            File.Delete(destDbPath);

        using var source = new CatalogDbContext(sourceDbPath);
        using var dest = new CatalogDbContext(destDbPath);
        dest.Database.EnsureCreated();
        dest.ChangeTracker.AutoDetectChangesEnabled = false;

        var namedItems = source.Items
            .Where(i => i.NameEnglish != null)
            .Include(i => i.Category)
            .Include(i => i.Icons)
                .ThenInclude(a => a.IconBlob)
            .AsNoTracking()
            .ToList();

        LogService.Write($"CatalogTrim: {namedItems.Count} named items found in source catalog.");

        var categoryMap = new Dictionary<int, CatalogCategory>();
        var blobMap = new Dictionary<int, IconBlob>();
        int copiedIcons = 0;

        foreach (var sourceItem in namedItems)
        {
            if (sourceItem.Category is null) continue;

            if (!categoryMap.TryGetValue(sourceItem.Category.Id, out var destCategory))
            {
                destCategory = new CatalogCategory
                {
                    TemplateType = sourceItem.Category.TemplateType,
                    RowType = sourceItem.Category.RowType,
                    SourceMbinPath = sourceItem.Category.SourceMbinPath
                };
                categoryMap[sourceItem.Category.Id] = destCategory;
                dest.Categories.Add(destCategory);
            }

            var destItem = new CatalogItem
            {
                Category = destCategory,
                GameId = sourceItem.GameId,
                NameLocKey = sourceItem.NameLocKey,
                NameEnglish = sourceItem.NameEnglish,
                NameLowerLocKey = sourceItem.NameLowerLocKey,
                NameLowerEnglish = sourceItem.NameLowerEnglish,
                DescriptionLocKey = sourceItem.DescriptionLocKey,
                DescriptionEnglish = sourceItem.DescriptionEnglish
            };

            foreach (var sourceIcon in sourceItem.Icons)
            {
                if (sourceIcon.IconBlob is null) continue;

                if (!blobMap.TryGetValue(sourceIcon.IconBlob.Id, out var destBlob))
                {
                    destBlob = new IconBlob
                    {
                        SourceDdsPath = sourceIcon.IconBlob.SourceDdsPath,
                        PngData = sourceIcon.IconBlob.PngData
                    };
                    blobMap[sourceIcon.IconBlob.Id] = destBlob;
                }

                destItem.Icons.Add(new IconAsset
                {
                    SourceField = sourceIcon.SourceField,
                    IconBlob = destBlob
                });
                copiedIcons++;
            }

            dest.Items.Add(destItem);
        }

        dest.SaveChanges();

        long sourceSize = new FileInfo(sourceDbPath).Length;
        long destSize = new FileInfo(destDbPath).Length;

        LogService.Write($"CatalogTrim: wrote {namedItems.Count} items, {blobMap.Count} unique icon blobs ({copiedIcons} icon references) to {destDbPath}.");
        LogService.Write($"CatalogTrim: {sourceSize / 1024.0 / 1024.0:F1} MB -> {destSize / 1024.0 / 1024.0:F1} MB.");
    }
}