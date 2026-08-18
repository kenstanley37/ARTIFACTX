namespace ArtifactX.Core.NmsModels;

/// <summary>
/// "Capital Ship Research" blueprint unlocks (Frigate Upgrade consumables,
/// Freighter Construction Modules, Freighter Recolouring options) - a
/// completely separate mechanism from CataloguePage's known-technology array
/// (vLc/6f=/4kj, unchanged by any of these). Discovered 2026-08-11/12 via a
/// real controlled in-game test: user researched 4 specific blueprints (Fuel
/// Oxidiser, Mind Control Device, Freighter Glass Corridor, Red recolour)
/// and a before/after save diff found this array grew from 778 to 782
/// entries, gaining exactly ^FRIG_BOOST_SPD/^FRIG_BOOST_TRA/
/// ^FRE_CORR_A_GLAS/^FREIGHT_RED - a perfect match. Same flat "^ITEM_ID"
/// array shape as 4kj.
///
/// NOT Freighter-exclusive - the array's very first sampled entry
/// ("BUILD_REFINER1") is an unrelated base-building unlock, so this is
/// NMS's general blueprint-unlock tracking array shared across several
/// systems. See ArtifactX.Almanac.Freighter.FreighterUpgrades for the
/// curated Freighter/Frigate-relevant subset this app actually exposes.
///
/// The "Salvaged Frigate Module" currency shown on the same research screen
/// needs no path here at all - it's just a stack of a regular Product item
/// (^FRIG_TOKEN) sitting in the Freighter's own Cargo container
/// (NmsInventoryContainer.FreighterCargoPath), already fully editable there.
/// </summary>
public static class NmsFreighterUpgradePaths
{
    public static readonly string[] BlueprintArrayPath = { "vLc", "6f=", "eZ<" };
}
