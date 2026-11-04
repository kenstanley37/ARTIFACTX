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
/// 2026-08, see NmsFreighterUpgradePaths). Writing a greyed building part into eZ&lt; is
/// supported by these counts only; an in-game write test is still pending.
/// </summary>
public static class NmsGameCatalogPaths
{
    public static readonly string[] KnownProductsPath = NmsFreighterUpgradePaths.BlueprintArrayPath;
    public static readonly string[] KnownSpecialsPath = { "vLc", "6f=", "24<" };
    public static readonly string[] KnownTechPath = NmsCataloguePaths.KnownTechnologyArrayPath;
}
