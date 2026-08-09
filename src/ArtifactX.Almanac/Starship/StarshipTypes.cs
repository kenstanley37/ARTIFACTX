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
/// The 2 reward ships are MEDIUM confidence: their loc-table display names
/// (found via `DataCataloger locidall "_SHIP"`, scattered across
/// loc6/7/8/9 under ids like UI_EXPD_SHIP_01_NAME_L) don't literally match
/// any filename, but "fighterclassicgold.scene.mbin" (with matching
/// gold-painted cockpit/engine/wing/nose PART files found ONLY alongside
/// it) lines up with "Golden Vector", and "fighterspecialswitch.scene.mbin"
/// (paired with "switchcockpita" parts, "switch"/NX both referencing the
/// Nintendo Switch release) lines up with "Horizon Vector NX" - internal
/// codenames not matching the polished display name is an already-confirmed
/// HG pattern (see NpcRaceOptions' "NPCFOURTH" = Autophage).
///
/// DELIBERATELY EXCLUDED - do not add without further confirmation:
///  - Hauler: no unambiguous personal-starship file found. The best
///    candidate, models/common/spacecraft/industrial/freighter_proc.scene.mbin,
///    sits in the same folder as capitalfreighter_proc/freightersmall_proc/
///    freightertiny_proc - naming that suggests this whole folder may
///    actually be the FREIGHTER (capital ship) model system, not the
///    personal Hauler starship type. Assigning the wrong one here could
///    hand a squadron pilot a capital-ship-scale model.
///  - The other 5 reward ships seen in a reference tool's list (Utopia
///    Speeder, Starborn Runner, Starborn Phoenix, Boundary Herald, The
///    Wraith) - real, confirmed loc-table display names (loc8/loc9), but no
///    matching filename was found despite targeted keyword searches. Their
///    internal codenames are apparently unrelated to both the display name
///    AND the expedition-reward theme words tried.
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
    };
}
