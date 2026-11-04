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
    /// <summary>Extracts the in-game Catalog screen's sections and subcategories, in
    /// the game's order, from metadata/reality/catalogue*.mbin (GcWiki). Build 184271,
    /// inspected 2026-11-03 (docs/research/2026-11-03-CATALOG-CATEGORIES.md). Which
    /// file is which section is chosen in NMS.exe, not data, so the file names and
    /// their UI_PORTAL_CAT_* title keys are listed here; every name shown comes from
    /// the game's text. Collected Knowledge has no such file and is left out.
    /// Products marked not craftable are recorded as always known (see CatalogueSection).</summary>
    public static void AddCatalogueSections(string catalogPath, string pcbanksPath)
    {
        (string File, string Key)[] sections =
        [
            ("CATALOGUEMATERIALS", "UI_PORTAL_CAT_MATERIALS"),
            ("CATALOGUECRAFTING", "UI_PORTAL_CAT_CRAFTING"),
            ("CATALOGUEBUILDING", "UI_PORTAL_CAT_BUILDING"),
            ("CATALOGUERECIPES", "UI_PORTAL_CAT_RECIPES"),
            ("CATALOGUEWONDERS", "UI_PORTAL_CAT_COLLECTION"),
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
        var wikis = new List<(string Key, GcWiki Wiki)>();
        foreach (var (file, key) in sections)
        {
            string name = $"METADATA/REALITY/{file}.MBIN";
            if (!index.TryFind(name, out var pak, out var entry, out var header))
                throw new InvalidDataException($"Catalog section not found: {name}");
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, entry, header)!));
            data.Load();
            if (data.GetData() is not GcWiki wiki)
                throw new InvalidDataException($"Catalog section has an unexpected structure: {name}");
            wikis.Add((key, wiki));
        }
        var uncraftable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string table in new[] { "NMS_REALITY_GCPRODUCTTABLE", "NMS_BASEPARTPRODUCTS", "NMS_MODULARCUSTOMISATIONPRODUCTS" })
        {
            if (!index.TryFind($"METADATA/REALITY/TABLES/{table}.MBIN", out var pak, out var entry, out var header)) continue;
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, entry, header)!));
            data.Load();
            if (data.GetData() is GcProductTable products)
                uncraftable.UnionWith(products.Table.Where(p => !p.IsCraftable).Select(p => p.ID.Value));
        }
        // The Recipes categories list no items: the game fills them from the recipe
        // table - refiner recipes by ingredient count (Refiner1/2/3), cooking recipes
        // (Cooking) - one tile per distinct result, in table order. 31 + 45 + 23 + 333
        // = 432, the game's own Recipes total (build 184271, 2026-11-03).
        var recipes = new List<GcRefinerRecipe>();
        if (index.TryFind("METADATA/REALITY/TABLES/NMS_REALITY_GCRECIPETABLE.MBIN", out var recipePak, out var recipeEntry, out var recipeHeader))
        {
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(recipePak, recipeEntry, recipeHeader)!));
            data.Load();
            if (data.GetData() is GcRecipeTable table) recipes.AddRange(table.Table);
        }
        (string[] Items, Dictionary<string, string[]>? Recipes) Contents(GcWikiCategory category)
        {
            Func<GcRefinerRecipe, bool>? kind = category.Type.WikiTopicType switch
            {
                GcWikiTopicType.WikiTopicTypeEnum.RecipesRefiner1 => r => !r.Cooking && r.Ingredients.Count == 1,
                GcWikiTopicType.WikiTopicTypeEnum.RecipesRefiner2 => r => !r.Cooking && r.Ingredients.Count == 2,
                GcWikiTopicType.WikiTopicTypeEnum.RecipesRefiner3 => r => !r.Cooking && r.Ingredients.Count == 3,
                GcWikiTopicType.WikiTopicTypeEnum.RecipesCooker => r => r.Cooking,
                _ => null,
            };
            if (kind is null) return (category.Items.Select(i => i.Value).Where(i => i.Length > 0).ToArray(), null);
            var byResult = recipes.Where(kind).GroupBy(r => r.Result.Id.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Id.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
            return (byResult.Keys.ToArray(), byResult);
        }
        var keys = wikis.SelectMany(w => w.Wiki.Categories.Select(c => c.CategoryID.Value).Append(w.Key).Append(w.Key + "_SUB"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var texts = EnglishTexts(index, extraction, keys);
        // Missing localization stays empty; raw localization ids must never reach players.
        string Text(string key) => texts.GetValueOrDefault(key, "");
        var result = wikis.Select(w => new CatalogueSection(w.Key, Text(w.Key), Text(w.Key + "_SUB"),
            w.Wiki.Categories.Select(c =>
            {
                var (items, itemRecipes) = Contents(c);
                return new CatalogueCategory(c.CategoryID.Value, Text(c.CategoryID.Value), c.Type.WikiTopicType.ToString(), items, itemRecipes);
            }).ToArray(),
            w.Wiki.Categories.SelectMany(c => c.Items.Select(i => i.Value)).Where(uncraftable.Contains).Distinct().ToArray())).ToArray();
        foreach (var section in result)
            ConsoleStyle.Success($"{section.Title} / {section.KnownLabel}: {section.Categories.Length} categories, {section.Categories.Sum(c => c.Items.Length)} items, {section.AlwaysKnown.Length} always known");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "CatalogueSections", "GcWiki", "metadata/reality/catalogue*.mbin", new List<CatalogItem>
        {
            new CatalogItem { GameId = "CATALOGUESECTIONS", NameEnglish = "Catalog sections",
                DescriptionEnglish = JsonSerializer.Serialize(result) }
        });
    }
}
