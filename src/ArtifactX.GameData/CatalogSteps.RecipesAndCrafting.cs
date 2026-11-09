using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>"DataCataloger add-recipes &lt;target-catalog-db-path&gt;" adds every refiner and cooking recipe from metadata/reality/tables/nms_reality_gcrecipetable.mbin (GcRecipeTable: Id, RecipeName/RecipeType loc keys, Result + Ingredients [{Id, Amount, Type}], TimeToMake, Cooking) as category "Recipes". Stored as ordinary catalog items so they survive `trim` with no schema change: GameId = recipe id, NameEnglish = resolved recipe name, UsageCategory = "Refiner"/"Cooking", DescriptionEnglish = compact JSON {"type","result":[id,amount],"ingredients":[[id,amount],...],"time"} - ids are item GameIds (products/substances), resolved to names/icons by the app through the normal catalog lookup. Added 2026-10-11 for the Recipes tool; same patch-in-place pattern as add-guide-topics; idempotent.</summary>
    public static void AddRecipes(string catalogPath, string pcbanksPath)
    {
        string recipeDbPath = catalogPath;
        if (!File.Exists(recipeDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {recipeDbPath}");
            return;
        }

        var recipeTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcRecipeTable>(
            pcbanksPath,
            f => f.Contains("nms_reality_gcrecipetable.mbin", StringComparison.OrdinalIgnoreCase));
        var recipeLookup = GetMergedEnglishLookup(pcbanksPath);

        if (recipeTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode nms_reality_gcrecipetable.mbin in any PAK.");
            return;
        }

        string Loc(string? id) => id is { Length: > 0 } && recipeLookup.TryGetValue(id, out var text) ? text : id ?? "";
        var recipes = new List<CatalogItem>();
        foreach (var r in recipeTable.Table)
        {
            string id = r.Id?.Value ?? "";
            string resultId = r.Result?.Id?.Value ?? "";
            var ingredients = (r.Ingredients ?? new()).Where(i => !string.IsNullOrEmpty(i.Id?.Value)).ToList();
            if (id.Length == 0 || resultId.Length == 0 || ingredients.Count == 0) continue;

            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = Loc(r.RecipeType?.Value),
                ["result"] = new System.Text.Json.Nodes.JsonArray(resultId, r.Result!.Amount),
                ["ingredients"] = new System.Text.Json.Nodes.JsonArray(ingredients
                    .Select(i => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonArray(i.Id.Value, i.Amount)).ToArray()),
                ["time"] = r.TimeToMake,
            };

            recipes.Add(new CatalogItem
            {
                GameId = id,
                NameEnglish = Loc(r.RecipeName?.Value),
                UsageCategory = r.Cooking ? "Cooking" : "Refiner",
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        ConsoleStyle.Header($"Found {recipes.Count} recipes ({recipes.Count(r => r.UsageCategory == "Cooking")} cooking, {recipes.Count(r => r.UsageCategory == "Refiner")} refiner).");
        foreach (var sample in recipes.Take(5))
            ConsoleStyle.Success($"  {sample.GameId} {sample.UsageCategory} '{sample.NameEnglish}' {sample.DescriptionEnglish}");

        using var recipeDb = new CatalogDbContext(recipeDbPath);
        Replace(recipeDb, "Recipes", "GcRefinerRecipe", "metadata/reality/tables/nms_reality_gcrecipetable.mbin", recipes);

        ConsoleStyle.Success($"Wrote {recipes.Count} recipes to {recipeDbPath}.");
    }

    /// <summary>"DataCataloger add-item-values &lt;target-catalog-db-path&gt;" adds the base Units value and trade data of every product and substance (GcProductTable / GcRealitySubstanceTable: BaseValue, TradeCategory, Legality, Rarity) as category "ItemValues". GameId = "VALUE:" + item id (prefixed so it never collides with the item's own catalog row in id lookups), NameEnglish = readable name, UsageCategory = TradeCategory (Mineral, Tech, Commodity, Component, Alloy, Exotic, Energy, None, SpecialShop), DescriptionEnglish = JSON {"id","value","legality","rarity","kind"}. Added 2026-10-11 for the Item Values tool; idempotent, same patch-in-place pattern as add-recipes.</summary>
    public static void AddItemValues(string catalogPath, string pcbanksPath)
    {
        string valueDbPath = catalogPath;
        if (!File.Exists(valueDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {valueDbPath}");
            return;
        }

        var productTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcProductTable>(
            pcbanksPath,
            f => f.EndsWith("nms_reality_gcproducttable.mbin", StringComparison.OrdinalIgnoreCase));
        var substanceTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcSubstanceTable>(
            pcbanksPath,
            f => f.EndsWith("nms_reality_gcsubstancetable.mbin", StringComparison.OrdinalIgnoreCase));
        var valueLookup = GetMergedEnglishLookup(pcbanksPath);

        if (productTable?.Table is null || substanceTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode the product and substance tables.");
            return;
        }

        string Name(string? lowerKey, string? key)
        {
            if (lowerKey is { Length: > 0 } && valueLookup.TryGetValue(lowerKey, out var lower)) return lower;
            return key is { Length: > 0 } && valueLookup.TryGetValue(key, out var upper) ? upper : "";
        }

        var valueItems = new List<CatalogItem>();
        void Add(string id, string name, int value, string trade, string legality, string rarity, string kind)
        {
            if (id.Length == 0 || name.Length == 0) return;
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = id, ["value"] = value, ["legality"] = legality, ["rarity"] = rarity, ["kind"] = kind,
            };
            valueItems.Add(new CatalogItem
            {
                GameId = "VALUE:" + id,
                NameEnglish = name,
                UsageCategory = trade,
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        foreach (var p in productTable.Table)
            Add(p.ID?.Value ?? "", Name(p.NameLower?.Value, p.Name?.Value), p.BaseValue,
                p.TradeCategory?.TradeCategory.ToString() ?? "None", p.Legality?.Legality.ToString() ?? "", p.Rarity?.Rarity.ToString() ?? "", "Product");
        foreach (var t in substanceTable.Table)
            Add(t.ID?.Value ?? "", Name(t.NameLower?.Value, t.Name?.Value), t.BaseValue,
                t.TradeCategory?.TradeCategory.ToString() ?? "None", t.Legality?.Legality.ToString() ?? "", t.Rarity?.Rarity.ToString() ?? "", "Substance");

        ConsoleStyle.Header($"Found {valueItems.Count} items with values.");
        foreach (var g in valueItems.GroupBy(i => i.UsageCategory))
            ConsoleStyle.Success($"  {g.Key}: {g.Count()}");

        using var valueDb = new CatalogDbContext(valueDbPath);
        Replace(valueDb, "ItemValues", "BaseValue", "metadata/reality/tables/nms_reality_gcproducttable.mbin + gcsubstancetable.mbin", valueItems);

        ConsoleStyle.Success($"Wrote {valueItems.Count} item values to {valueDbPath}.");
    }

    /// <summary>"DataCataloger add-crafting &lt;target-catalog-db-path&gt;" adds what every craftable thing is made from, for the Crafting calculator: GcProductData.Requirements (what a product costs to craft) and GcTechnology.Requirements (what a technology costs to build/install), each a list of {ID, Amount, Type}. Category "Crafting"; GameId = "CRAFT:" + id (prefixed so it never collides with the item's own catalog row), NameEnglish = readable name, UsageCategory = "Product"/"Technology", DescriptionEnglish = JSON {"id","kind","craftable", "req":[[id,amount],...]}. Only rows with at least one requirement are kept; ids in "req" are products/substances (raw when they have no row here). Added 2026-10-11; idempotent, same patch-in-place pattern as add-item-values.</summary>
    public static void AddCrafting(string catalogPath, string pcbanksPath)
    {
        string craftDbPath = catalogPath;
        if (!File.Exists(craftDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {craftDbPath}");
            return;
        }

        var craftProducts = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcProductTable>(
            pcbanksPath,
            f => f.EndsWith("nms_reality_gcproducttable.mbin", StringComparison.OrdinalIgnoreCase));
        var craftTech = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcTechnologyTable>(
            pcbanksPath,
            f => f.EndsWith("nms_reality_gctechnologytable.mbin", StringComparison.OrdinalIgnoreCase));
        var craftLookup = GetMergedEnglishLookup(pcbanksPath);

        if (craftProducts?.Table is null || craftTech?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode the product and technology tables.");
            return;
        }

        string CraftName(string? lowerKey, string? key)
        {
            if (lowerKey is { Length: > 0 } && craftLookup.TryGetValue(lowerKey, out var lower)) return lower;
            return key is { Length: > 0 } && craftLookup.TryGetValue(key, out var upper) ? upper : "";
        }

        var craftItems = new List<CatalogItem>();
        void AddCraft(string id, string name, string kind, bool craftable, List<libMBIN.NMS.GameComponents.GcTechnologyRequirement>? requirements)
        {
            var req = (requirements ?? new()).Where(r => !string.IsNullOrEmpty(r.ID?.Value) && r.Amount > 0).ToList();
            if (id.Length == 0 || req.Count == 0) return;
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = id,
                ["kind"] = kind,
                ["craftable"] = craftable,
                ["req"] = new System.Text.Json.Nodes.JsonArray(req
                    .Select(r => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonArray(r.ID.Value, r.Amount)).ToArray()),
            };
            craftItems.Add(new CatalogItem
            {
                GameId = "CRAFT:" + id,
                NameEnglish = name.Length > 0 ? name : id,
                UsageCategory = kind,
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        foreach (var p in craftProducts.Table)
            AddCraft(p.ID?.Value ?? "", CraftName(p.NameLower?.Value, p.Name?.Value), "Product", p.IsCraftable, p.Requirements);
        foreach (var t in craftTech.Table)
            AddCraft(t.ID?.Value ?? "", CraftName(t.NameLower?.Value, t.Name?.Value), "Technology", true, t.Requirements);

        ConsoleStyle.Header($"Found {craftItems.Count} craftable things ({craftItems.Count(i => i.UsageCategory == "Product")} products, {craftItems.Count(i => i.UsageCategory == "Technology")} technology).");
        foreach (var sample in craftItems.Where(i => i.GameId is "CRAFT:STASIS_DEVICE" or "CRAFT:HYPERDRIVE"))
            ConsoleStyle.Success($"  {sample.GameId} '{sample.NameEnglish}' {sample.DescriptionEnglish}");

        using var craftDb = new CatalogDbContext(craftDbPath);
        Replace(craftDb, "Crafting", "GcTechnologyRequirement", "metadata/reality/tables/nms_reality_gcproducttable.mbin + gctechnologytable.mbin", craftItems);

        ConsoleStyle.Success($"Wrote {craftItems.Count} crafting requirement rows to {craftDbPath}.");
    }

    /// <summary>"DataCataloger add-procedural-products &lt;target-catalog-db-path&gt;" adds the procedurally generated items (save ids like ^PROC_SEA#17140 - ocean salvage, fossils, plant samples, documents, freighter logs...) from metadata/reality/tables/proceduralproducttable.mbin (GcProceduralProductTable, one entry per GcProceduralProductCategory). The game builds each item's display name from random words picked by the #seed, which can't be reproduced here, so each row gets the product's own subtitle/category name and its icon instead. Category "ProceduralProducts"; GameId = the product id (the app already strips "#seed" when looking items up), NameEnglish = resolved name, icon from Product.Icon (else the first ProceduralData icon). Added 2026-10-11; idempotent.</summary>
    public static void AddProceduralProducts(string catalogPath, string pcbanksPath)
    {
        string procDbPath = catalogPath;
        if (!File.Exists(procDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {procDbPath}");
            return;
        }

        var procIndex = GetIndex(pcbanksPath);
        var procTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcProceduralProductTable>(
            pcbanksPath,
            f => f.EndsWith("proceduralproducttable.mbin", StringComparison.OrdinalIgnoreCase));
        var procLookup = GetMergedEnglishLookup(pcbanksPath);

        if (procTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode proceduralproducttable.mbin.");
            return;
        }

        string ProcLoc(string? key) => key is { Length: > 0 } && procLookup.TryGetValue(key, out var text) ? text : "";
        var categories = Enum.GetNames(typeof(libMBIN.NMS.GameComponents.GcProceduralProductCategory.ProceduralProductCategoryEnum));
        var iconService = new IconExtractionService(SharedExtraction, procIndex, targetSize: 128);
        var procRows = new List<(string Id, string Name, string Category, string TexturePath, byte[]? Png)>();

        for (int i = 0; i < procTable.Table.Length; i++)
        {
            var entry = procTable.Table[i];
            var product = entry?.Product;
            if (product is null) continue;
            string category = i < categories.Length ? categories[i] : $"#{i}";
            string id = product.ID?.Value ?? "";
            string name = ProcLoc(product.NameLower?.Value);
            if (name.Length == 0) name = ProcLoc(product.Name?.Value);
            string subtitle = ProcLoc(product.Subtitle?.Value);
            string texture = product.Icon?.Filename?.Value ?? "";
            if (texture.Length == 0)
                texture = entry!.ProceduralData?.Select(d => d?.Icon?.Filename?.Value ?? "").FirstOrDefault(f => f.Length > 0) ?? "";

            ConsoleStyle.Success($"  [{i}] {category,-20} id='{id}' name='{name}' nameKey='{product.Name?.Value}' lowerKey='{product.NameLower?.Value}' subtitle='{subtitle}' (key '{product.Subtitle?.Value}') base='{entry!.NameGeneratorBase?.Word?.Value}' icon='{texture}'");
            if (id.Length == 0 || (name.Length == 0 && subtitle.Length == 0)) continue;

            // Names with %WORD% placeholders are filled in per item by the game.
            string display = name.Length > 0 && !name.Contains('%') ? name : subtitle.Length > 0 ? subtitle : category;
            byte[]? png = texture.Length > 0 ? iconService.ExtractAndConvert(texture) : null;
            procRows.Add((id, display, category, texture, png));
        }

        ConsoleStyle.Header($"Found {procRows.Count} procedural products ({procRows.Count(r => r.Png is not null)} with icons).");

        using var procDb = new CatalogDbContext(procDbPath);
        var existingProc = procDb.Categories.FirstOrDefault(c => c.TemplateType == "ProceduralProducts");
        if (existingProc is not null)
        {
            var itemIds = procDb.Items.Where(i => i.CategoryId == existingProc.Id).Select(i => i.Id).ToList();
            var oldIcons = procDb.Set<IconAsset>().Where(a => itemIds.Contains(a.ItemId)).ToList();
            var oldBlobIds = oldIcons.Select(a => a.IconBlobId).Distinct().ToList();
            procDb.RemoveRange(oldIcons);
            procDb.Items.RemoveRange(procDb.Items.Where(i => i.CategoryId == existingProc.Id));
            procDb.SaveChanges();
            // Icon images can be shared with other catalog rows; only drop ones nothing uses now.
            var stillUsed = procDb.Set<IconAsset>().Where(a => oldBlobIds.Contains(a.IconBlobId)).Select(a => a.IconBlobId).ToHashSet();
            procDb.RemoveRange(procDb.Set<IconBlob>().Where(b => oldBlobIds.Contains(b.Id) && !stillUsed.Contains(b.Id)));
            procDb.Categories.Remove(existingProc);
            procDb.SaveChanges();
        }

        var procCategory = new CatalogCategory
        {
            TemplateType = "ProceduralProducts",
            RowType = "GcProceduralProductData",
            SourceMbinPath = "metadata/reality/tables/proceduralproducttable.mbin"
        };
        // One IconBlob per texture path (unique in the DB): reuse an existing one -
        // e.g. the fossil texture is shared by two categories and may already be in
        // the catalog from the product table.
        var blobs = new Dictionary<string, IconBlob>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in procRows)
        {
            var item = new CatalogItem { GameId = row.Id, NameEnglish = row.Name, UsageCategory = row.Category };
            if (row.Png is not null)
            {
                if (!blobs.TryGetValue(row.TexturePath, out var blob))
                {
                    blob = procDb.Set<IconBlob>().FirstOrDefault(b => b.SourceDdsPath == row.TexturePath)
                        ?? new IconBlob { SourceDdsPath = row.TexturePath, PngData = row.Png };
                    blobs[row.TexturePath] = blob;
                }
                item.Icons.Add(new IconAsset { SourceField = "Icon", IconBlob = blob });
            }
            procCategory.Items.Add(item);
        }
        procDb.Categories.Add(procCategory);
        procDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {procRows.Count} procedural products to {procDbPath}.");
    }
}
