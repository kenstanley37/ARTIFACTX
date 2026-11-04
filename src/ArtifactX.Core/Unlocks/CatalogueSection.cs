namespace ArtifactX.Core.Unlocks;

/// <summary>One section of the in-game Catalog screen (Materials &amp; Items, Technology,
/// Building Parts, Recipes, Wonders), from its own GcWiki file
/// (metadata/reality/catalogue*.mbin). Title and KnownLabel are the game's words
/// ("Building Parts", "Blueprints Known"). See docs/research/2026-11-03-CATALOG-CATEGORIES.md:
/// the files' item totals (653 / 358 / 1,024) match the game's own screen.
/// AlwaysKnown lists the section's products the game data marks not craftable
/// (IsCraftable false): in Building Parts exactly the 10 firework packs, which no
/// save list holds yet the game counts as known on every slot checked.</summary>
public sealed record CatalogueSection(string Key, string Title, string KnownLabel, CatalogueCategory[] Categories, string[] AlwaysKnown);

/// <summary>A Catalog subcategory in the game's order ("Space Station Exterior").
/// Items are listed ids. The game fills record-type categories at run time
/// (GcWikiTopicType); the Recipes ones are rebuilt the same way from the recipe table:
/// each item is a result, ItemRecipes the recipe ids that make it (see
/// CatalogSteps.AddCatalogueSections). Wonders have no items yet.</summary>
public sealed record CatalogueCategory(string Key, string Name, string Type, string[] Items,
    Dictionary<string, string[]>? ItemRecipes = null);
