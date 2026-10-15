using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using ArtifactX.GameData.Services.Interfaces;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// Which items the game counts as cooking ingredients (2026-10-15, for the
    /// Food Storage page's Ingredient Storage list): the product and substance
    /// tables' own CookingIngredient flag, plus EggModifierIngredient. Category
    /// "CookingIngredients": GameId "COOK:" + item id, NameEnglish = item id,
    /// UsageCategory = "Ingredient" or "EggModifier" (an egg modifier that
    /// isn't also an ingredient). Patch-in-place and idempotent, like the
    /// other steps.
    /// </summary>
    public static void AddCookingIngredients(string catalogPath, string pcbanksPath)
    {
        if (!File.Exists(catalogPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {catalogPath}");
            return;
        }

        IPakReaderService reader = new PakReaderService();
        var extraction = new ExtractionService();
        GcProductTable? products = null;
        GcSubstanceTable? substances = null;
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (products is not null && substances is not null) break;
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = reader.ReadHeader(pak.FullPath);
                entries = reader.Read(pak.FullPath);
            }
            catch { continue; }

            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && (e.FileName.EndsWith("nms_reality_gcproducttable.mbin", StringComparison.OrdinalIgnoreCase)
                             || e.FileName.EndsWith("nms_reality_gcsubstancetable.mbin", StringComparison.OrdinalIgnoreCase))))
            {
                try
                {
                    var bytes = extraction.ExtractEntryBytes(pak.FullPath, entry, header);
                    if (bytes is null || bytes.Length == 0) continue;
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    switch (mbin.GetData())
                    {
                        case GcProductTable p: products ??= p; break;
                        case GcSubstanceTable t: substances ??= t; break;
                    }
                }
                catch { }
            }
        }
        if (products?.Table is null || substances?.Table is null)
        {
            ConsoleStyle.Error("Could not find the product and substance tables.");
            return;
        }

        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? id, bool ingredient, bool egg)
        {
            if (string.IsNullOrEmpty(id) || (!ingredient && !egg)) return;
            found.TryAdd(id, ingredient ? "Ingredient" : "EggModifier");
        }
        foreach (var p in products.Table) Add(p.ID?.Value, p.CookingIngredient, p.EggModifierIngredient);
        foreach (var s in substances.Table) Add(s.ID?.Value, s.CookingIngredient, s.EggModifierIngredient);

        using var db = new CatalogDbContext(catalogPath);
        var old = db.Categories.FirstOrDefault(c => c.TemplateType == "CookingIngredients");
        if (old != null)
        {
            db.Items.RemoveRange(db.Items.Where(i => i.CategoryId == old.Id));
            db.Categories.Remove(old);
            db.SaveChanges();
        }
        var category = new CatalogCategory
        {
            TemplateType = "CookingIngredients",
            RowType = "GcProductData",
            SourceMbinPath = "metadata/reality/tables/nms_reality_gcproducttable.mbin",
        };
        db.Categories.Add(category);
        db.SaveChanges();
        foreach (var (id, kind) in found)
            db.Items.Add(new CatalogItem { CategoryId = category.Id, GameId = "COOK:" + id, NameEnglish = id, UsageCategory = kind });
        db.SaveChanges();
        ConsoleStyle.Success($"Wrote {found.Count} cooking ingredients to {catalogPath}.");
    }
}
