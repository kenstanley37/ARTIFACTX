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
/// Archetype -> folder mapping confidence: HIGH for the 7 folder-derived
/// types below - each folder contains exactly one shallow (non-nested)
/// scene file with no sibling candidates, and the folder name plainly
/// matches its real-world theme (Explorer <-> "scientific", Exotic <->
/// "s-class" - Exotic ships are always S-class rarity by design, matching
/// real save data: a Squadron pilot's Gek-flown ship was
/// MODELS/COMMON/SPACECRAFT/S-CLASS/S-CLASS_PROC.SCENE.MBIN - Solar <->
/// "sailship", Sentinel Interceptor <-> "sentinelship"). Hauler is the 8th
/// archetype and is instead CONFIRMED (see below), not folder-derived -
/// its real folder name ("dropships") gives no thematic hint at all.
///
/// **CONFIRMED directly from real owned-ship save data** (2026-08-09 -
/// user claimed all 8 reward ships in-game AND separately bought a random
/// Hauler at a space station, then shared the resulting saves; each owned
/// ship in vLc/6f=/@Cs carries both its real NKm display Name and its real
/// NTx.93M scene path together - the single most direct evidence tier used
/// anywhere in this investigation, no inference needed for any of these):
///  - Hauler = dropships/dropship_proc.scene.mbin - the user's own
///    player-named "TestHauler" purchase. "Dropships" as the internal
///    folder name for the Hauler archetype gives zero thematic hint - a
///    codename-vs-marketing-name mismatch even more extreme than the
///    others below, but this is real player-purchased save data, no
///    ambiguity.
///  - Iron Vulture = dropships/dropship_proc.scene.mbin - the SAME file as
///    Hauler, seed 0x0 (fixed/quest-crafted, unlike TestHauler's randomly
///    rolled seed) - a reward SKIN of the plain Hauler chassis, same
///    "reused base file + fixed seed" pattern as every other reward ship
///    here.
///  - Utopia Speeder = fighters/vrspeeder.scene.mbin - this file was
///    dismissed in an earlier pass as "VR-exclusive, unrelated" based on
///    its name alone; real save data proved that assumption wrong.
///  - Starborn Runner = fighters/wracer.scene.mbin (NOT the "SE" file -
///    see Starborn Phoenix below for why that distinction matters).
///  - Boundary Herald = fighters/spookship.scene.mbin - genuinely
///    surprising (its loc DESC mentions a "Cursed Expedition", not
///    Halloween) but this is straight from the save, not a guess.
///  - Golden Vector = fighters/fighterclassicgold.scene.mbin - matches
///    what an earlier PART-FILE-based inference (see below) had already
///    landed on, now save-confirmed too.
///  - The Wraith = s-class/bioparts/biofighter.scene.mbin, seed 0x0 -
///    matches NmsInventoryContainer's own pre-existing doc comment on
///    ModelSeedPath ("One sampled Living Ship ('The Wraith') had seed
///    '0x0'") exactly, an independent confirmation from a completely
///    different investigation. The Wraith is a Living Ship, not a
///    standard archetype variant - matches its organic/tentacled look.
///
/// **Reward ships - inferred, MEDIUM confidence, NOT directly
/// save-confirmed** (not present in either save checked so far):
///  - Horizon Vector NX = fighters/fighterspecialswitch.scene.mbin -
///    traced via paired "switchcockpita" parts ("switch"/NX both
///    referencing the Nintendo Switch release).
///  - Starborn Phoenix = fighters/wracerse.scene.mbin (the "SE"/Special
///    Edition sibling of Starborn Runner's now-confirmed plain
///    wracer.scene.mbin) - inferred from that naming pattern once Runner's
///    real file ruled out this class's original "Runner and Phoenix share
///    one file" theory. Also the exact file a Squadron pilot in the same
///    save was independently flying (see
///    GcSpaceshipGlobals.HoverShipDataNamesSpecial) - consistent with, but
///    not equivalent to, a real owned-ship confirmation for Phoenix
///    specifically.
/// Internal codenames not matching the polished display name (Hauler/
/// Golden Vector/Horizon Vector NX especially) is a pattern confirmed
/// repeatedly across this whole investigation (see also NpcRaceOptions'
/// "NPCFOURTH" = Autophage).
///
/// Nothing is deliberately excluded any more as of 2026-08-09 - every
/// named reward ship a reference tool's Type dropdown showed now has an
/// entry here.
/// </summary>
public static class StarshipTypes
{
    public static readonly IReadOnlyList<StarshipTypeInfo> All = new[]
    {
        new StarshipTypeInfo("Fighter", "MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTER_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Hauler", "MODELS/COMMON/SPACECRAFT/DROPSHIPS/DROPSHIP_PROC.SCENE.MBIN"),
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
        new StarshipTypeInfo("Iron Vulture (Reward)", "MODELS/COMMON/SPACECRAFT/DROPSHIPS/DROPSHIP_PROC.SCENE.MBIN"),
        new StarshipTypeInfo("Utopia Speeder (Reward)", "MODELS/COMMON/SPACECRAFT/FIGHTERS/VRSPEEDER.SCENE.MBIN"),
    };
}
