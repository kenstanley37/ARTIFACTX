namespace ArtifactX.Almanac.Starship;

/// <summary>
/// Starship "Type" (base hull model) options, discovered the same way as
/// MultiToolTypes - listing real .SCENE.MBIN files under
/// models/common/spacecraft/ (see `DataCataloger ships`), not copied from
/// any external source. Unlike Multi-Tool's flat ~11-file list, spacecraft/
/// turned out to hold ~1,565 files (cockpits, connectors, decorations,
/// engine parts, etc. - the game builds ships from procedural building
/// blocks, not one file per type), so this list is built from the small
/// number of top-level folders that each resolve to exactly ONE clean
/// "_proc.scene.mbin" (or single named file) representing a whole distinct
/// archetype, cross-checked against a reference tool's own Type dropdown
/// (2026-08-09) for which display names to expect - the file paths
/// themselves are independently discovered, never copied.
///
/// Archetype -> folder mapping confidence: HIGH for the 7 "normal" types
/// below - each folder contains exactly one shallow (non-nested) scene file
/// with no sibling candidates, and the folder name plainly matches its
/// real-world theme (Explorer <-> "scientific", Exotic <-> "s-class" -
/// Exotic ships are always S-class rarity by design, matching real save
/// data: a Squadron pilot's Gek-flown ship was
/// MODELS/COMMON/SPACECRAFT/S-CLASS/S-CLASS_PROC.SCENE.MBIN - Solar <->
/// "sailship", Sentinel Interceptor <-> "sentinelship").
///
/// The reward ships are MEDIUM confidence: their loc-table display names
/// (found via `DataCataloger locidall "_SHIP"`, scattered across
/// loc6/7/8/9 under ids like UI_EXPD_SHIP_01_NAME_L) don't literally match
/// any filename, but each was traced via a different piece of real evidence:
///  - "fighterclassicgold.scene.mbin" (matching gold-painted cockpit/engine/
///    wing/nose PART files found ONLY alongside it) = "Golden Vector".
///  - "fighterspecialswitch.scene.mbin" (paired with "switchcockpita"
///    parts, "switch"/NX both referencing the Nintendo Switch release) =
///    "Horizon Vector NX".
///  - "fighters/wracerse.scene.mbin" = GcSpaceshipGlobals'
///    HoverShipDataNamesSpecial field (`DataCataloger dumpfile
///    gcspaceshipglobals.global.mbin`) - a real data-table reference, not
///    just filename correlation. Matches both Starborn Runner's AND
///    Starborn Phoenix's loc DESC text ("a localised vector field allows
///    the craft to hover above solid planes" - near-identical wording for
///    both) - almost certainly one shared hover-chassis file reused for
///    both reward skins via a seed/texture choice this list can't resolve,
///    so both names point at the same path here rather than guessing which
///    is which. This is also the exact file this save's own Slot 1 pilot 3
///    was already flying, independent confirmation it's a real in-use ship.
/// Internal codenames not matching the polished display name is an
/// already-confirmed HG pattern (see NpcRaceOptions' "NPCFOURTH" =
/// Autophage).
///
/// DELIBERATELY EXCLUDED - do not add without further confirmation:
///  - Hauler: no unambiguous personal-starship file found. The best
///    candidate, models/common/spacecraft/industrial/freighter_proc.scene.mbin,
///    sits in the same folder as capitalfreighter_proc/freightersmall_proc/
///    freightertiny_proc - naming that suggests this whole folder may
///    actually be the FREIGHTER (capital ship) model system, not the
///    personal Hauler starship type. Assigning the wrong one here could
///    hand a squadron pilot a capital-ship-scale model.
///  - Utopia Speeder, Boundary Herald, The Wraith - real, confirmed
///    loc-table display names (loc8/loc9), but GENUINELY EXHAUSTED as of
///    2026-08-09: checked every "*ShipDataNames"-shaped field in
///    GcSpaceshipGlobals (only Hover/HoverSpecial/Spook exist), and
///    confirmed via `DataCataloger grep "Boundary Herald"` (and similarly
///    for the others) that NO non-loc MBIN file references their display
///    text anywhere - their model path is most likely a literal string
///    constant in the game's own compiled reward-granting code, not present
///    in any decodable data table. Don't re-attempt this exact search
///    strategy expecting a different result; would need a different
///    approach entirely (e.g. testing a real claim of one in-game and
///    diffing the save).
/// </summary>
public static class StarshipTypes
{
    public static readonly IReadOnlyList<StarshipTypeInfo> All = new[]
    {
        new StarshipTypeInfo("Fighter", "MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTER_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Explorer", "MODELS/COMMON/SPACECRAFT/SCIENTIFIC/SCIENTIFIC_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Exotic", "MODELS/COMMON/SPACECRAFT/S-CLASS/S-CLASS_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Shuttle", "MODELS/COMMON/SPACECRAFT/SHUTTLE/SHUTTLE_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Solar", "MODELS/COMMON/SPACECRAFT/SAILSHIP/SAILSHIP_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Sentinel Interceptor", "MODELS/COMMON/SPACECRAFT/SENTINELSHIP/SENTINELSHIP_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Corvette", "MODELS/COMMON/SPACECRAFT/CORVETTE/CORVETTE.SCENE.MBIN"),
        new StarshipTypeInfo("Golden Vector (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTERCLASSICGOLD.SCENE.MBIN"),
        new StarshipTypeInfo("Horizon Vector NX (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTERSPECIALSWITCH.SCENE.MBIN"),
        new StarshipTypeInfo("Starborn Runner / Phoenix (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/WRACERSE.SCENE.MBIN"),
    };
}
