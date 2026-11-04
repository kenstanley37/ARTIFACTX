using System.Text.Json;
using ArtifactX.Core.Unlocks;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>Extracts every product's game type from the three product tables
    /// the catalog reads. Build 184271, inspected 2026-11-03: the 1,215 products
    /// without a Catalogue group split into BuildingPart 285, modular ship parts 280,
    /// Consumable 228, CustomisationPart 196, Curiosity 181, CreatureEgg 18, Tradeable
    /// 15, Component 7, Fish 5 - enough for the Catalog page to group them.</summary>
    public static void AddProductTypes(string catalogPath, string pcbanksPath)
    {
        const string modular = "METADATA/REALITY/TABLES/NMS_MODULARCUSTOMISATIONPRODUCTS.MBIN";
        string[] files =
        [
            "METADATA/REALITY/TABLES/NMS_REALITY_GCPRODUCTTABLE.MBIN",
            "METADATA/REALITY/TABLES/NMS_BASEPARTPRODUCTS.MBIN",
            modular,
        ];
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string path) continue;
            try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
            catch (IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
        }
        var extraction = new ExtractionService();
        var entries = new List<ProductTypeEntry>();
        foreach (string file in files)
        {
            if (!index.TryFind(file, out var pak, out var item, out var header))
                throw new InvalidDataException($"Product table not found: {file}");
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, item, header)!));
            data.Load();
            if (data.GetData() is not GcProductTable table)
                throw new InvalidDataException($"Product table has an unexpected structure: {file}");
            entries.AddRange(table.Table.Where(p => p.ID.Value.Length > 0)
                .Select(p => new ProductTypeEntry(p.ID.Value, p.Type.ProductCategory.ToString(), file == modular)));
        }
        var rows = entries.DistinctBy(e => e.ItemId, StringComparer.OrdinalIgnoreCase).ToArray();
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ProductTypes", "GcProductCategory", string.Join(" + ", files).ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "PRODUCTTYPES", NameEnglish = "Product types",
                DescriptionEnglish = JsonSerializer.Serialize(rows) }
        });
        ConsoleStyle.Success($"Wrote {rows.Length} product types to {catalogPath}.");
    }
}
