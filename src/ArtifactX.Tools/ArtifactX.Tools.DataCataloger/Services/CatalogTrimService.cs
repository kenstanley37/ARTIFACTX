using Microsoft.EntityFrameworkCore;
using ArtifactX.Tools.DataCataloger.Data;
using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Services;

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
                DescriptionEnglish = sourceItem.DescriptionEnglish,
                UsageCategory = sourceItem.UsageCategory,
                MaxStackSize = sourceItem.MaxStackSize
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

        // Creature descriptor trees are a small, fully-content table (a few
        // thousand rows total across every rig) - unlike Items, there's no
        // "unnamed noise" to filter out, so every row copies over. Self-
        // referencing, so a multi-pass copy is needed: each pass copies
        // whatever's left whose parent (if any) has already been copied,
        // until nothing more can be resolved - this doesn't assume source
        // row Ids already happen to be in parent-before-child order.
        var sourceDescriptors = source.CreatureDescriptorOptions.AsNoTracking().ToList();
        var descriptorMap = new Dictionary<int, CreatureDescriptorOption>();
        var remainingDescriptors = sourceDescriptors;

        while (remainingDescriptors.Count > 0)
        {
            var stillRemaining = new List<CreatureDescriptorOption>();

            foreach (var sourceOption in remainingDescriptors)
            {
                CreatureDescriptorOption? destParent = null;
                if (sourceOption.ParentOptionId is int parentId)
                {
                    if (!descriptorMap.TryGetValue(parentId, out destParent))
                    {
                        stillRemaining.Add(sourceOption);
                        continue;
                    }
                }

                var destOption = new CreatureDescriptorOption
                {
                    RigId = sourceOption.RigId,
                    Category = sourceOption.Category,
                    OptionId = sourceOption.OptionId,
                    Name = sourceOption.Name,
                    Chance = sourceOption.Chance,
                    ParentOption = destParent
                };

                descriptorMap[sourceOption.Id] = destOption;
                dest.CreatureDescriptorOptions.Add(destOption);
            }

            if (stillRemaining.Count == remainingDescriptors.Count)
            {
                LogService.Write($"CatalogTrim: WARNING - {stillRemaining.Count} creature descriptor nodes had unresolved parents (orphaned), skipping.");
                break;
            }

            remainingDescriptors = stillRemaining;
        }

        dest.SaveChanges();
        LogService.Write($"CatalogTrim: copied {descriptorMap.Count} of {sourceDescriptors.Count} creature descriptor tree nodes.");

        long sourceSize = new FileInfo(sourceDbPath).Length;
        long destSize = new FileInfo(destDbPath).Length;

        LogService.Write($"CatalogTrim: wrote {namedItems.Count} items, {blobMap.Count} unique icon blobs ({copiedIcons} icon references) to {destDbPath}.");
        LogService.Write($"CatalogTrim: {sourceSize / 1024.0 / 1024.0:F1} MB -> {destSize / 1024.0 / 1024.0:F1} MB.");
    }
}