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

        // The shared, cached index (CatalogSteps.GetIndex) instead of a scan of every PAK.
        var products = DecodeMbin(pcbanksPath, "METADATA/REALITY/TABLES/NMS_REALITY_GCPRODUCTTABLE.MBIN") as GcProductTable;
        var substances = DecodeMbin(pcbanksPath, "METADATA/REALITY/TABLES/NMS_REALITY_GCSUBSTANCETABLE.MBIN") as GcSubstanceTable;
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
