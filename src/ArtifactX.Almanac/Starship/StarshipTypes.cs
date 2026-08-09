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
/// **Reward ships - CONFIRMED directly from real owned-ship save data**
/// (2026-08-09, user claimed all 6 in-game then shared their save's own
/// vLc/6f=/@Cs array - each owned ship carries both its real NKm display
/// Name AND its real NTx.93M scene path together, the most direct evidence
/// possible, no inference needed):
///  - Starborn Runner = fighters/wracer.scene.mbin (note: NOT the "SE"
///    file - see Starborn Phoenix below for why that distinction matters).
///  - Boundary Herald = fighters/spookship.scene.mbin - genuinely
///    surprising (its loc DESC mentions a "Cursed Expedition", not
///    Halloween) but this is straight from the save, not a guess.
///  - The Wraith = s-class/bioparts/biofighter.scene.mbin, seed 0x0 -
///    matches NmsInventoryContainer's own pre-existing doc comment on
///    ModelSeedPath ("One sampled Living Ship ('The Wraith') had seed
///    '0x0'") exactly, an independent confirmation from a completely
///    different investigation. The Wraith is a Living Ship, not a
///    standard archetype variant - matches its organic/tentacled look.
///
/// **Reward ships - inferred, MEDIUM confidence, not directly
/// save-confirmed**:
///  - Golden Vector = fighters/fighterclassicgold.scene.mbin - traced via
///    gold-painted cockpit/engine/wing/nose PART files found ONLY
///    alongside it (matching its loc DESC's "golden edition").
///  - Horizon Vector NX = fighters/fighterspecialswitch.scene.mbin -
///    traced via paired "switchcockpita" parts ("switch"/NX both
///    referencing the Nintendo Switch release).
///  - Starborn Phoenix = fighters/wracerse.scene.mbin (the "SE"/Special
///    Edition sibling of Starborn Runner's plain wracer.scene.mbin,
///    confirmed above) - inferred from the naming pattern once Runner's
///    real file ruled out the original "both share one file" theory this
///    class held before 2026-08-09. Also the exact file a Squadron pilot
///    in this same save was independently flying (see
///    GcSpaceshipGlobals.HoverShipDataNamesSpecial), consistent with but
///    not equivalent to a real owned-ship confirmation for Phoenix
///    specifically.
/// Internal codenames not matching the polished display name (Golden
/// Vector/Horizon Vector NX especially) is an already-confirmed HG pattern
/// (see NpcRaceOptions' "NPCFOURTH" = Autophage).
///
/// DELIBERATELY EXCLUDED - do not add without further confirmation:
///  - Hauler: no unambiguous personal-starship file found. The best
///    candidate, models/common/spacecraft/industrial/freighter_proc.scene.mbin,
///    sits in a folder now CONFIRMED to be the FREIGHTER (capital ship)
///    model system (its full file listing is entirely hangars/cargo
///    containers/gantries/turrets - capital-ship furniture, not a personal
///    starship), not the personal Hauler starship type. Assigning it here
///    could hand a squadron pilot a capital-ship-scale model.
///  - Utopia Speeder, Iron Vulture: real, confirmed loc-table display
///    names, not present in the one real save checked (only 7 of that
///    save's 12 ship slots were populated, none of these two). Revisit if
///    the user claims/registers these in a save and shares it the same way
///    the other 3 were confirmed.
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
        new StarshipTypeInfo("Starborn Runner (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/WRACER.SCENE.MBIN"),
        new StarshipTypeInfo("Starborn Phoenix (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/WRACERSE.SCENE.MBIN"),
        new StarshipTypeInfo("Boundary Herald (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/SPOOKSHIP.SCENE.MBIN"),
        new StarshipTypeInfo("The Wraith (Reward, Living Ship)", "MODELS/COMMON/SPACECRAFT/S-CLASS/BIOPARTS/BIOFIGHTER.SCENE.MBIN"),
    };
}
