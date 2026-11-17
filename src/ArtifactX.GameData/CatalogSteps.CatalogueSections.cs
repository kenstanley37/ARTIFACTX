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
        var fossils = new List<GcProductData>();
        foreach (string table in new[] { "NMS_REALITY_GCPRODUCTTABLE", "NMS_BASEPARTPRODUCTS", "NMS_MODULARCUSTOMISATIONPRODUCTS" })
        {
            if (!index.TryFind($"METADATA/REALITY/TABLES/{table}.MBIN", out var pak, out var entry, out var header)) continue;
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pak, entry, header)!));
            data.Load();
            if (data.GetData() is not GcProductTable products) continue;
            uncraftable.UnionWith(products.Table.Where(p => !p.IsCraftable).Select(p => p.ID.Value));
            fossils.AddRange(products.Table.Where(p => p.Type.ProductCategory == GcProductCategory.ProductCategoryEnum.ExhibitBone));
        }
        // Wonders' record slots are named in the UI globals, one config per slot in
        // the save's array order (GcPlayerStateData.Wonder*Records).
        var recordNames = new Dictionary<GcWikiTopicType.WikiTopicTypeEnum, string[]>();
        if (index.TryFind("GCUIGLOBALS.GLOBAL.MBIN", out var uiPak, out var uiEntry, out var uiHeader))
        {
            using var data = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(uiPak, uiEntry, uiHeader)!));
            data.Load();
            if (data.GetData() is libMBIN.NMS.Globals.GcUIGlobals ui)
            {
                string[] Ids(GcWonderCategoryConfig[] configs) => configs.Select(c => c.LocID.Value).ToArray();
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.TreasureWonders] = Ids(ui.WonderTreasureCategoryConfig);
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.WeirdBasePartWonders] = Ids(ui.WonderWeirdBasePartCategoryConfig);
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.PlanetWonders] = Ids(ui.WonderPlanetCategoryConfig);
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.CreatureWonders] = Ids(ui.WonderCreatureCategoryConfig);
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.FloraWonders] = Ids(ui.WonderFloraCategoryConfig);
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.MineralWonders] = Ids(ui.WonderMineralCategoryConfig);
                recordNames[GcWikiTopicType.WikiTopicTypeEnum.CustomWonders] = Ids(ui.WonderCustomCategoryConfig);
            }
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
        (string[] Items, Dictionary<string, string[]>? Recipes, Dictionary<string, string>? Names) Contents(GcWikiCategory category)
        {
            var type = category.Type.WikiTopicType;
            if (recordNames.TryGetValue(type, out var slots))
            {
                var ids = slots.Select((_, i) => $"WONDER:{type}:{i}").ToArray();
                return (ids, null, ids.Select((id, i) => (id, Text(slots[i]))).ToDictionary(p => p.id, p => p.Item2));
            }
            // 143 of the 144 fossil products: BONE_TEMP's name is a fill-in template
            // ("%NAME% EXHIBIT") and isn't on the game's Fossil Records screen.
            if (type == GcWikiTopicType.WikiTopicTypeEnum.ExhibitBones)
                return (fossils.Where(f => !Text(f.Name.Value).Contains('%')).Select(f => f.ID.Value).ToArray(), null, null);
            Func<GcRefinerRecipe, bool>? kind = category.Type.WikiTopicType switch
            {
                GcWikiTopicType.WikiTopicTypeEnum.RecipesRefiner1 => r => !r.Cooking && r.Ingredients.Count == 1,
                GcWikiTopicType.WikiTopicTypeEnum.RecipesRefiner2 => r => !r.Cooking && r.Ingredients.Count == 2,
                GcWikiTopicType.WikiTopicTypeEnum.RecipesRefiner3 => r => !r.Cooking && r.Ingredients.Count == 3,
                GcWikiTopicType.WikiTopicTypeEnum.RecipesCooker => r => r.Cooking,
                _ => null,
            };
            if (kind is null) return (category.Items.Select(i => i.Value).Where(i => i.Length > 0).ToArray(), null, null);
            var byResult = recipes.Where(kind).GroupBy(r => r.Result.Id.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Id.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
            return (byResult.Keys.ToArray(), byResult, null);
        }
        var keys = wikis.SelectMany(w => w.Wiki.Categories.Select(c => c.CategoryID.Value).Append(w.Key).Append(w.Key + "_SUB"))
            .Concat(recordNames.Values.SelectMany(n => n)).Concat(fossils.Select(f => f.Name.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var texts = EnglishTexts(index, extraction, keys);
        // Missing localization stays empty; raw localization ids must never reach players.
        string Text(string key) => texts.GetValueOrDefault(key, "");
        var result = wikis.Select(w => new CatalogueSection(w.Key, Text(w.Key), Text(w.Key + "_SUB"),
            w.Wiki.Categories.Select(c =>
            {
                var (items, itemRecipes, itemNames) = Contents(c);
                return new CatalogueCategory(c.CategoryID.Value, Text(c.CategoryID.Value), c.Type.WikiTopicType.ToString(), items, itemRecipes,
                    itemNames, Counted: c.Type.WikiTopicType != GcWikiTopicType.WikiTopicTypeEnum.CustomWonders);
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
        AddCatalogueIcons(db, wikis.Where(w => w.Key == "UI_PORTAL_CAT_COLLECTION").SelectMany(w => w.Wiki.Categories),
            new IconExtractionService(extraction, index, targetSize: 128), Text);
    }

    /// <summary>The Wonders categories' own icons (GcWikiCategory.IconOn, the pictures on the game's category list:
    /// planet, paw, leaf, rock...), stored as items <see cref="CatalogueCategory.IconIdFor"/> with their icon. Record
    /// tiles (Planet, Fauna, Flora, Mineral) have no item picture of their own and show these (2026-11-11). Replaced
    /// whole on every build; an icon image shared with another catalog row is kept.</summary>
    private static void AddCatalogueIcons(CatalogDbContext db, IEnumerable<GcWikiCategory> categories, IconExtractionService icons,
        Func<string, string> text)
    {
        var existing = db.Categories.FirstOrDefault(c => c.TemplateType == "CatalogueIcons");
        if (existing is not null)
        {
            var itemIds = db.Items.Where(i => i.CategoryId == existing.Id).Select(i => i.Id).ToList();
            var oldIcons = db.Set<IconAsset>().Where(a => itemIds.Contains(a.ItemId)).ToList();
            var oldBlobIds = oldIcons.Select(a => a.IconBlobId).Distinct().ToList();
            db.RemoveRange(oldIcons);
            db.Items.RemoveRange(db.Items.Where(i => i.CategoryId == existing.Id));
            db.SaveChanges();
            var stillUsed = db.Set<IconAsset>().Where(a => oldBlobIds.Contains(a.IconBlobId)).Select(a => a.IconBlobId).ToHashSet();
            db.RemoveRange(db.Set<IconBlob>().Where(b => oldBlobIds.Contains(b.Id) && !stillUsed.Contains(b.Id)));
            db.Categories.Remove(existing);
            db.SaveChanges();
        }

        var category = new CatalogCategory
        {
            TemplateType = "CatalogueIcons",
            RowType = "GcWikiCategory",
            SourceMbinPath = "metadata/reality/cataloguewonders.mbin (IconOn)",
        };
        // One IconBlob per texture path (unique in the DB): reuse one already there.
        var blobs = new Dictionary<string, IconBlob>(StringComparer.OrdinalIgnoreCase);
        foreach (var wikiCategory in categories)
        {
            string texture = wikiCategory.IconOn?.Filename?.Value ?? "";
            if (texture.Length == 0 || icons.ExtractAndConvert(texture) is not { } png) continue;
            if (!blobs.TryGetValue(texture, out var blob))
            {
                blob = db.Set<IconBlob>().FirstOrDefault(b => b.SourceDdsPath == texture) ?? new IconBlob { SourceDdsPath = texture, PngData = png };
                blobs[texture] = blob;
            }
            string key = wikiCategory.CategoryID.Value;
            var item = new CatalogItem { GameId = CatalogueCategory.IconIdFor(key), NameEnglish = text(key) is { Length: > 0 } name ? name : "Catalog category" };
            item.Icons.Add(new IconAsset { SourceField = "Icon", IconBlob = blob });
            category.Items.Add(item);
        }
        db.Categories.Add(category);
        db.SaveChanges();
        ConsoleStyle.Success($"Wonders category icons: {category.Items.Count}");
    }
}
