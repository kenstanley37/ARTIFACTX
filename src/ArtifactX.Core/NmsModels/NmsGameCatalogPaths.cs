using System.Collections.Generic;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// The per-save lists behind the in-game Catalog screen's "known" counts. Matched
/// 2026-11-03 against the owner's Slot 1 and Slot 8 Catalog screens, exact on both
/// (docs/research/2026-11-03-CATALOG-CATEGORIES.md):
///   Technology known = KnownTechPath (4kj) + the section's 187 products the game
///     marks not craftable (CatalogueSection.AlwaysKnown - the very ids account B1h
///     holds): Slot 1 170 + 187 = 357, Slot 8 147 + 187 = 334.
///   Building Parts known = KnownProductsPath (eZ&lt;) + KnownSpecialsPath (24&lt;) +
///     the section's 10 not-craftable firework packs: Slot 1 1,011, Slot 8 964.
/// eZ&lt; is GcPlayerStateData.KnownProducts (a researched-blueprint diff confirmed it in
/// 2026-08, see NmsFreighterUpgradePaths). CONFIRMED writable 2026-11-03: the owner
/// learned greyed Titanic Skull (STA_09) on the Catalog page on Slot 8, saved, and it
/// showed coloured in the game's Catalog (Building Parts 964 -> 965).
/// </summary>
public static class NmsGameCatalogPaths
{
    public static readonly string[] KnownProductsPath = NmsFreighterUpgradePaths.BlueprintArrayPath;
    public static readonly string[] KnownSpecialsPath = { "vLc", "6f=", "24<" };
    public static readonly string[] KnownTechPath = NmsCataloguePaths.KnownTechnologyArrayPath;

    /// <summary>GcPlayerStateData.KnownRefinerRecipes: recipe ids (^REFINERECIPE_41,
    /// ^RECIPE_1161). A Recipes tile is a result, known when any recipe making it is
    /// here: Slot 1 20 + 9 + 0 + 54 = 83, Slot 8 11 + 4 + 0 + 6 = 21, both exact, and
    /// Slot 8's 11 simple-refiner results match the owner's screenshot by name
    /// (2026-11-03).</summary>
    public static readonly string[] KnownRecipesPath = { "vLc", "6f=", "Ddk" };

    /// <summary>The Wonders record arrays (GcPlayerStateData.Wonder*Records, one
    /// GcWonderRecord per slot: UqY GenerationID, 1OQ value, y87 seen), keyed by the
    /// Catalog category type. Found by shape and size on Slot 1 and Slot 8; their save
    /// order matches libMBIN's field order, which is how same-sized Flora (fp9) and
    /// Mineral (bYs) were told apart. A slot is recorded when its GenerationID isn't 0:
    /// with fish caught and all 143 fossils this gives Slot 1 61 + 219 + 143 = 423 and
    /// Slot 8 45 + 60 + 143 = 248, the game's own counts (2026-11-03).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> WonderRecordPaths = new Dictionary<string, string[]>
    {
        ["PlanetWonders"] = new[] { "vLc", "6f=", "dgD" },
        ["CreatureWonders"] = new[] { "vLc", "6f=", "MZ1" },
        ["FloraWonders"] = new[] { "vLc", "6f=", "fp9" },
        ["MineralWonders"] = new[] { "vLc", "6f=", "bYs" },
        ["TreasureWonders"] = new[] { "vLc", "6f=", "e?Y" },
        ["WeirdBasePartWonders"] = new[] { "vLc", "6f=", "<8Q" },
        ["CustomWonders"] = new[] { "vLc", "6f=", "Svy" },
    };

    /// <summary>GcPlayerStateData.WonderCustomRecordsExtraData: one entry per Personal
    /// Record slot, fH8 = the name the player gave it, 7bp/93I = what it records
    /// ("Creature", "Planet"; "Custom" when unused). Found by shape 2026-11-03: Slot 8's
    /// first is "Dude" (Creature), Slot 1's "Water World" (Planet).</summary>
    public static readonly string[] PersonalRecordNamesPath = { "vLc", "6f=", "YvA" };
}
