using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for the Animal Companions ("Pets") array. Unlike the
/// standard NmsInventoryContainer shape, a pet entry is NOT a :No/hl?
/// inventory grid - it's a flat 30-slot array of per-pet stat objects, one
/// slot per owned/tamed pet (empty slots have fH8 == "").
///
/// A slot's occupancy is XID (species), not fH8 (custom name) - fH8 is
/// empty until the player manually renames a pet, so a freshly tamed,
/// never-renamed pet still occupies its slot with fH8 == "" (real bug hit
/// 2026-07-28: the Pets page originally filtered occupied slots by fH8,
/// silently dropping every unnamed pet). The fancy auto-generated name
/// shown in-game (e.g. "Riverpito") is computed client-side for display -
/// the literal text itself is never written to the save (confirmed by
/// decrypting a real save with 7 such pets and full-text-searching the raw
/// JSON for their names, which found nothing anywhere in the file) - but
/// the seed that computation is driven from (m9o, see RollSeedPath below)
/// IS stored and IS confirmed editable, indirectly changing which name gets
/// picked.
///
/// Confirmed via real save data (4 owned test pets, cross-checked against
/// in-game screenshots) AND cross-referenced against NMSCD/Creature-Builder
/// (github.com/NMSCD/Creature-Builder, the open-source tool behind
/// creature.nmscd.com) - its src/contracts/creatureSave.ts independently
/// reverse-engineers the base "CreatureSave" structure every creature
/// reference shares, and its 27 fields (Scale..Moods) map cleanly onto our
/// first 27 keys in field-declaration order, confirming almost the entire
/// object at once:
///  - fH8 = CustomName, xDJ = Trust, unY = Scale, XID = CreatureID: already
///    independently confirmed via exact value matches against screenshots.
///  - osl (body part list) = Descriptors (Array&lt;string&gt;) - the
///    construction "recipe", separate from the seeds below. CONFIRMED
///    2026-07-28 what this actually is: a flat array of node Ids sampled
///    from a per-species RECURSIVE option tree, not a flat value list. Each
///    creature body "rig" (trex, cat, rodent, spider, grunt, ...) has its
///    own tree in models/planets/creatures/&lt;rig&gt;/&lt;name&gt;.descriptor.mbin
///    (TkModelDescriptorList -&gt; TkResourceDescriptorList.Descriptors -&gt;
///    TkResourceDescriptorData, whose own Children re-enter a nested
///    TkModelDescriptorList for the next branch down) - confirmed by
///    decoding a real pet's rig file and matching every one of its osl
///    entries to an exact node in that tree: a real TREX pet's osl
///    ["_TREX_4", "_HEAD_ALIEN", "_BLOB_2B", "_EYES_1", "_ANTENNAS_1",
///    "_BODY_BIRDREX", "_BBRACC_5N", "_TAIL_RAT", "4262532434"] matched
///    _TREX_4 (a top-level body archetype, sibling to _TREX_3XRARE) -&gt;
///    HEAD=_HEAD_ALIEN (one of 8 HEAD choices) -&gt; that head's own BLOB
///    slot=_BLOB_2B -&gt; that blob's own EYES=_EYES_1 and ANTENNAS=
///    _ANTENNAS_1 sub-choices, plus _TREX_4's sibling BODY=_BODY_BIRDREX
///    (one of 4) -&gt; its own BBRACC=_BBRACC_5N accessory slot, and sibling
///    TAIL=_TAIL_RAT (one of 5, a leaf with no further sub-choices) - every
///    named entry matched exactly, with only the trailing numeric entry
///    ("4262532434") unaccounted for anywhere in the tree, most likely a
///    per-instance detail/variation seed rather than a selectable node.
///    Now extracted into its own database table (CreatureDescriptorOption,
///    self-referencing via ParentOptionId - see CatalogBuildService's Phase
///    1.7 and ArtifactX.WinUI3's CatalogService.GetCreatureDescriptorTreeAsync)
///    rather than hardcoded, since it's real per-rig PAK content discovered
///    by a filename rule (every *.descriptor.mbin under models/planets/
///    creatures/, excluding ANIM/ANIMS/ANIMATION subfolders which hold
///    unrelated per-animation-clip files using the same MBIN type) covering
///    roughly 30-40 rigs. A pet's rig is looked up by XID lowercased -
///    exact for most species but a confirmed minority don't match
///    (SWIMCOW/cowswim, TWOLEGANTELOPE/antelopetwolegs, ROBOTANTELOPE/
///    anteloperobot - word order flips), so callers should treat an empty
///    tree result as "no data for this species," not force a guess.
///    CONFIRMED EDITABLE (2026-07-29): a per-slot dropdown editor (see
///    DescriptorsPath below) shipped and was real-tested on a live save -
///    swapping Head/Ears/Acc/Body/Back/Tail together in one edit, saving,
///    and reloading in-game rendered every new part correctly (a
///    lizard-shaped test pet became turtle/sloth/monkey-shaped), with no
///    change to Species name, Rarity/Affinity, Trust, Traits, Mutation
///    Progress, Battle Abilities, or Holo-Arena Victories. Most rows swap a
///    slot's value among its own real sibling options (same Category, same
///    parent) - including root-level ones on rigs like rodent that have
///    MULTIPLE independent root categories (Head/Body/Tail as separate
///    peers, not alternatives of one choice - swapping Body there doesn't
///    touch Head or Tail). The exception: on a rig whose tree has exactly
///    ONE distinct root category (e.g. TREX's single "_TREX_" category,
///    whose 2 alternatives lead to entirely different child slot sets -
///    TAILB/TOPB vs HEAD/BODY/TAIL), that one root row IS a genuine
///    archetype choice, and picking a different alternative REBUILDS the
///    whole array from scratch instead (PetsPage.BuildDefaultDescriptorArray:
///    walks the new archetype's tree depth-first, picking a default per
///    slot in the game's own original category order, preserving any
///    trailing non-tree entries like the detail seed unchanged) since a
///    different archetype has entirely different child slots. Checked
///    every cataloged rig: 27 of 71 have the multi-root shape, so this
///    can't be assumed to be "always the first row" - an earlier version
///    of this feature got that wrong and would have silently discarded a
///    rodent-shaped pet's other selections the first time someone touched
///    a Head/Body/Tail row, caught before it shipped to real testing.
///    UNTESTED IN-GAME as of 2026-07-29 - unlike the confirmed same-slot
///    swap above, nobody has yet confirmed the game accepts a full
///    archetype swap built this way.
///  - WTp/1p=/uAX/6fX ([bool, hex] pairs) = CreatureSeed/
///    CreatureSecondarySeed/ColourBaseSeed/BoneScaleSeed. Only WTp
///    (CreatureSeed) was exposed for editing before this was found - real
///    reroll-and-reload testing showed it drives color variance only. Every
///    sampled pet had 1p=/uAX permanently [false,"0x0"] (never independently
///    rolled) while 6fX always exactly MIRRORED WTp's value whenever both
///    were active - so Generate rerolls WTp+6fX together as one new value
///    (preserving that observed mirror) and deliberately leaves 1p=/uAX
///    alone rather than guessing at a state no real save has ever shown.
///  - 8jm.8jm = Biome (matches our established "Native Climate" reading).
///  - HbY.HbY = CreatureType (locomotion/role archetype, e.g. "Crab" -
///    distinct from Biome; not exposed, no clear editable use).
///  - HhX = CustomSpeciesName (always "^"/empty - none of our test pets had
///    a custom species rename).
///  - XwC/tDR/KY5/@Hb = BirthTime/LastEggTime/LastTrustIncreaseTime/
///    LastTrustDecreaseTime (4 timestamps, matches the contract's 4).
///  - ttq (4 empty strings + a 0 number) = SenderData (LID/UID/USN/PTK/TS) -
///    multiplayer gifting metadata, empty since these pets were never
///    received from another player.
///  - JAy (3 signed floats) = Traits, NOT a position vector as first
///    guessed - confirmed via exact match: PetTest1's JAy
///    [-0.224, -0.770, 0.178] -> abs()*100 = 22.4/77.0/17.8, matching its
///    real "Playfulness 22%/Gentleness 77%/Independence 18%" screenshot
///    exactly. Sign's meaning (which descriptor-word "pole" it picks) is
///    unconfirmed, so TraitsPath edits preserve each element's existing
///    sign and only change magnitude. Which of the 3 named traits (the
///    displayed set differs per pet/species - Playfulness/Gentleness/
///    Independence for one, Helpfulness/Aggression/Devotion for another)
///    corresponds to which array index isn't determinable from the save
///    alone, so these are exposed generically (Trait 1/2/3), not by name.
///  - IEo (2 floats) = Moods - not exposed, no clear single-value mapping
///    to the one-word "Current Mood" display (e.g. "Lively").
///  - a2U (Genes Improved) is NOT a simple stored display number - real
///    play testing showed the game recalculates/deducts from it on its own
///    (0 -&gt; 30 our edit -&gt; 26 -&gt; 28 across two real sessions, tracking
///    actual gameplay) - stages/saves fine mechanically, just don't expect
///    it to hold an arbitrary value long-term the way Trust does.
///  - KAx (Mutation Progress): 0-1 float, confirmed exact match (0.857... -&gt;
///    "86%") - a repeating meter, not a one-time completion value (observed
///    wrapping past 100% back toward 0 across real sessions).
///  - u75 (Holo-Arena Victories): exact integer match.
///  - ujr[3] (per-stat mutation points, max 10 each) and E&lt;S[3].1o6 (same
///    "1o6" Class-letter convention as Ships/Freighter/Multi-Tool) are both
///    pet-specific fields beyond the base CreatureSave contract (Creature-
///    Builder doesn't cover the companion leveling metagame). ujr is
///    confirmed via exact "X/10" matches. E&lt;S's relationship to the "Battle
///    Abilities" badges took two rounds to fully understand - first-round
///    testing (forcing one pet's E&lt;S to S/S/S across reloads with no badge
///    change, a second pet's untouched badges/E&lt;S not matching, grinding
///    mutation points not moving badges, a WTp/6fX reroll not moving them)
///    concluded E&lt;S was unrelated. That conclusion was INCOMPLETE, not
///    wrong: every one of those test pets had isp (see UnknownBoolFPath,
///    now ClassLetterOverrideActivePath below) false, which is the default
///    on every wild-tamed pet sampled - and isp=false is exactly the state
///    where E&lt;S is dormant/ignored (always observed as S/S/S, a placeholder
///    default) while the badges instead come from the m9o/JrL roll below.
///    Setting isp=true (2026-07-28) flips the game into reading E&lt;S
///    directly for the badges instead - confirmed via real testing: with
///    isp=true and E&lt;S left at its default S/S/S, all three badges/bars
///    jumped to max (S/S/S); with isp still true and E&lt;S manually set to
///    distinct letters (Agility=A/Health=B/Combat=C typed), the in-game
///    badges displayed exactly those three letters. That same test also
///    caught a real index-order bug: E&lt;S does NOT share ujr's Agility/
///    Health/Combat order - statIndex 0 is Health and statIndex 1 is
///    Agility (swapped relative to ujr), while statIndex 2 (Combat) does
///    match. See ClassLetterPath below for the corrected mapping.
///  - m9o AND JrL (hex strings, RollSeedPrimaryPath/RollSeedSecondaryPath
///    below) are BOTH CONFIRMED co-inputs driving the Battle Abilities
///    badges (Combat Effectiveness/Agility/Health grades) AND the in-game
///    fancy auto-generated species name (e.g. "M. Swanuciluoe") - found
///    2026-07-28 dumping a real pet's full raw structure while building an
///    "Advanced Fields" experimentation panel. m9o was confirmed first via a
///    reroll-and-revert round trip (regenerating it changed name+badges
///    together, restoring its original value reverted both exactly). JrL
///    was then confirmed independently: regenerated with m9o held fixed,
///    and the resulting name+badge change was verified directly against the
///    decrypted save (m9o genuinely unchanged, JrL matched the newly
///    generated value exactly). Since either field alone shifts the result,
///    they're most likely combined into one hash/roll rather than either
///    being "the" seed on its own - the same pattern NMS uses combining
///    multiple seed fields for full creature generation elsewhere. This
///    settles the "badges computed from data outside what save-editing can
///    reach" theory from earlier testing - they're reachable after all, just
///    via different fields (m9o/JrL) than the one originally guessed (E&lt;S).
///    No PAK/MBIN table search ever needed to be right about this - it was
///    in the save the whole time. 5L6 (also a raw hex string, see
///    UnknownHexCPath below, the remaining member of the same
///    originally-grouped trio) was tested the same way and is CONFIRMED NOT
///    a co-input - regenerated in isolation with m9o/JrL both held fixed,
///    name and all three badges stayed exactly the same. Its own purpose
///    remains unknown.
///  - fjE (5 slots, each an id + level + magnitude) is very likely the
///    Genetic Profile's 5 named special-ability mutation nodes (e.g.
///    "Frostburn"/"Voidfrost"/"Shrieking Gale"/"Glacial Energy"/"Refresh"
///    seen in a real screenshot) - distinct from ujr's plain Agility/
///    Health/Combat point counters. Stayed empty on every sampled pet
///    regardless of leveling, meaning none of our test pets ever unlocked
///    one - not exposed, unconfirmed and no known-good value to stage.
///  - jtr (5 known battle move ids, e.g. "^ATTACK_AFF") is the actual
///    combat move-set used in Holo-Arena fights - observed changing on its
///    own between two real saves with no edit from this app, so it rotates
///    somehow independent of anything above. Not exposed.
/// </summary>
public static class NmsPetPaths
{
    /// <summary>Fixed per-stat mutation point cap (matches "X/10" for every
    /// one of the 3 mutation stats) and the derived sum cap "Genes Improved"
    /// shows (3 x 10 = 30, matches "0/30" exactly).</summary>
    public const int MutationPointsMax = 10;
    public const int GenesImprovedMax = MutationPointsMax * 3;

    public static readonly string[] PetArrayPath = { "vLc", "6f=", "Mcl" };

    public static string[] PetPath(int petIndex) =>
        new[] { "vLc", "6f=", "Mcl", petIndex.ToString() };

    public static string[] NamePath(int petIndex) => PetPath(petIndex).Append("fH8").ToArray();
    public static string[] TrustPath(int petIndex) => PetPath(petIndex).Append("xDJ").ToArray();
    public static string[] NativeClimatePath(int petIndex) => PetPath(petIndex).Append("8jm").Append("8jm").ToArray();
    public static string[] SpeciesArchetypePath(int petIndex) => PetPath(petIndex).Append("XID").ToArray();
    public static string[] GenesImprovedPath(int petIndex) => PetPath(petIndex).Append("a2U").ToArray();
    public static string[] MutationProgressPath(int petIndex) => PetPath(petIndex).Append("KAx").ToArray();
    public static string[] HoloArenaVictoriesPath(int petIndex) => PetPath(petIndex).Append("u75").ToArray();

    /// <summary>The whole 3-entry mutation-points array (Agility/Health/
    /// Combat, display order) - staged as a whole, same reasoning as Ship/
    /// Freighter/Multi-Tool's @bB stat-bonus arrays (a deeper leaf-only
    /// stage isn't seen by SaveSessionManager's staged-edit lookup, which
    /// only matches at the exact path queried).</summary>
    public static string[] MutationPointsPath(int petIndex) => PetPath(petIndex).Append("ujr").ToArray();

    /// <summary>The whole 3-entry Traits (personality) array - see this
    /// class's doc comment for the JAy -&gt; Traits confirmation. Staged as a
    /// whole, same reasoning as MutationPointsPath above.</summary>
    public static string[] TraitsPath(int petIndex) => PetPath(petIndex).Append("JAy").ToArray();

    /// <summary>CreatureSeed - the pet's primary procedural-look seed.
    /// Confirmed via real reroll-and-reload testing to drive the pet's
    /// pattern/texture (markings, scale/fur style) AND its color WHILE
    /// ColourBaseSeedActivePath is off. Whether this field still controls
    /// pattern once the override IS active is untested - every override
    /// test so far held this field fixed and only varied the override hex
    /// (see ColourBaseSeedActivePath below), so it's unconfirmed whether
    /// rerolling this while the override is active would change pattern,
    /// leave it alone, or something else. BoneScaleSeedPath below is kept
    /// in sync with this one on every reroll since every sampled pet showed
    /// them mirrored.</summary>
    public static string[] SeedPath(int petIndex) => PetPath(petIndex).Append("WTp").Append("1").ToArray();

    /// <summary>BoneScaleSeed - always mirrored WTp's value in every sample
    /// gathered so far. Not exposed as its own editable field; only ever
    /// written by GenerateSeedBtn_Click alongside SeedPath, to preserve that
    /// observed mirror rather than letting the two diverge into an untested
    /// state.</summary>
    public static string[] BoneScaleSeedPath(int petIndex) => PetPath(petIndex).Append("6fX").Append("1").ToArray();

    /// <summary>CONFIRMED (2026-07-28, real reroll-and-revert round trip) to
    /// drive the in-game Battle Abilities badges (Combat Effectiveness/
    /// Agility/Health grades) AND the fancy auto-generated species name
    /// (e.g. "M. Swanuciluoe") - see this class's doc comment for the full
    /// test. Distinct from SeedPath (WTp) above, which only drives color.
    /// Unlike the color seed, m9o has no known "mirror" field to keep in
    /// sync - it's a single hex string, not a [bool, hex] pair. NOT the sole
    /// input to the badge/name roll - see RollSeedSecondaryPath below,
    /// confirmed to independently shift the same outputs while this field
    /// stays untouched. Most likely both feed one combined hash, the same
    /// way NMS combines multiple seed fields elsewhere in full creature
    /// generation.</summary>
    public static string[] RollSeedPrimaryPath(int petIndex) => PetPath(petIndex).Append("m9o").ToArray();

    /// <summary>CONFIRMED (2026-07-28, real isolated single-field test with
    /// RollSeedPrimaryPath/m9o held fixed and verified unchanged directly
    /// against the decrypted save afterward) to ALSO independently drive the
    /// same Battle Abilities badges and fancy species name as
    /// RollSeedPrimaryPath. 5L6 (still UnknownHexCPath below, untested in
    /// isolation) is the remaining member of the same originally-grouped
    /// "unknown hex" trio and a strong candidate to be a third co-input to
    /// the same roll.</summary>
    public static string[] RollSeedSecondaryPath(int petIndex) => PetPath(petIndex).Append("JrL").ToArray();

    /// <summary>CreatureSecondarySeed's active flag - every sampled pet had
    /// this permanently false (never independently rolled by the game)
    /// until real testing (2026-07-28) activated it with a fresh generated
    /// hex. CONFIRMED no effect: no change to species name, Traits, Weight/
    /// Height, or Holo-Arena Victories, and a follow-up test with
    /// ClassLetterOverrideActivePath off (so the m9o/JrL roll was actually
    /// driving the badges again, not masked by Class Letters) showed the
    /// same badges as before this field was ever touched. Purpose otherwise
    /// unknown.</summary>
    public static string[] SecondarySeedActivePath(int petIndex) => PetPath(petIndex).Append("1p=").Append("0").ToArray();
    public static string[] SecondarySeedPath(int petIndex) => PetPath(petIndex).Append("1p=").Append("1").ToArray();

    /// <summary>ColourBaseSeed's active flag - permanently false, never
    /// independently rolled by the game, on every sampled pet until real
    /// testing (2026-07-28). CONFIRMED an OVERRIDE that swaps ONLY color,
    /// leaving pattern/texture untouched - settled via two rounds of
    /// testing. First round (regenerating the override hex while also
    /// changing SeedPath/WTp partway through) briefly looked like it might
    /// be swapping pattern too, since different Seed values happened to
    /// pair with different-looking override results. Second round isolated
    /// the variables properly: with SeedPath held completely fixed and
    /// Override Active checked, three separate freshly generated override
    /// hexes in a row (green/brown camo, red, teal) all rendered on the
    /// EXACT SAME scale/texture pattern - only the color changed each time.
    /// A paired control (regenerating the override hex while Active stayed
    /// UNCHECKED) produced zero visible change, confirming the hex is
    /// simply not read at all while inactive. Species name, badges, Traits,
    /// Weight/Height, and Holo-Arena Victories were unaffected throughout
    /// either round.</summary>
    public static string[] ColourBaseSeedActivePath(int petIndex) => PetPath(petIndex).Append("uAX").Append("0").ToArray();
    public static string[] ColourBaseSeedPath(int petIndex) => PetPath(petIndex).Append("uAX").Append("1").ToArray();

    /// <summary>CreatureType (locomotion/role archetype, e.g. "Crab" or
    /// "Passive" - shares its enum with the species-shape archetypes seen in
    /// the CreatureSpecies catalog, see CatalogBuildService). Distinct from
    /// Biome/XID. Stored as the plain enum member name (no "^" prefix,
    /// unlike XID/HhX) - confirmed on a real pet ("Passive"). The complete,
    /// authoritative value set is libMBIN's own GcCreatureTypes.
    /// CreatureTypeEnum - ArtifactX.Core deliberately doesn't reference
    /// libMBIN (that's a WinUI3-side concern), so the UI pulls the enum's
    /// values directly via Enum.GetNames rather than a hand-copied
    /// duplicate living here. CONFIRMED no visible effect (real test,
    /// 2026-07-28): set to "Jellyfish" on a Rodent-shaped pet (about as
    /// different a value as the enum allows) with no change to the model,
    /// species name, Battle Abilities, or stats. Purpose otherwise
    /// unknown.</summary>
    public static string[] CreatureTypePath(int petIndex) => PetPath(petIndex).Append("HbY").Append("HbY").ToArray();

    /// <summary>CustomSpeciesName - always "^" (empty) on every sampled pet
    /// until real testing (2026-07-28) confirmed it: overrides the fancy
    /// in-game Latin species name (the auto-generated one, e.g.
    /// "Z. Spellgigarnima") with whatever plain text is set, uppercased in
    /// display. CONFIRMED purely cosmetic - no effect on stats, Battle
    /// Abilities, or anything else tested. Uses the same "^" prefix
    /// convention as XID - the game visibly broke when tested with plain
    /// text and no leading caret, so ArtifactX.WinUI3's UI (see
    /// PetsPage.AddCaretPrefixedStringFieldRow) always adds the prefix back
    /// automatically before staging, regardless of what the user
    /// types.</summary>
    public static string[] CustomSpeciesNamePath(int petIndex) => PetPath(petIndex).Append("HhX").ToArray();

    /// <summary>The whole osl array (Descriptors, body-part composition
    /// "recipe") - see this class's doc comment (the "osl" bullet) for the
    /// confirmed recursive-tree finding. Staged as a whole array, same
    /// reasoning as TraitsPath/MutationPointsPath above - ArtifactX.WinUI3's
    /// editor swaps individual entries' values in place (preserving array
    /// length/order) rather than rebuilding the array shape, backed by the
    /// CreatureDescriptorOption catalog data (CatalogService.
    /// GetCreatureDescriptorTreeAsync). CONFIRMED WORKING (2026-07-29, real
    /// save+reload test): swapped Head/Ears/Acc/Body/Back/Tail all together
    /// in one edit (e.g. Head Taz->Turtle, Body Rodent->Sloth, Tail
    /// Rodent->Monkey) and the pet rendered with every new part correctly
    /// applied in-game - visibly a different body shape (a lizard-posed
    /// creature became a stocky turtle/sloth-shaped one) - with Species
    /// name, Rarity/Affinity, Trust, Traits, Mutation Progress, Battle
    /// Abilities, and Holo-Arena Victories all unchanged. The editor now
    /// ALSO supports rebuilding the whole array for a different archetype
    /// on rigs that have exactly one - see PetsPage.BuildDescriptorsPanel's
    /// rigHasSingleRootArchetype for why that's rig-shape-dependent, not
    /// always "osl[0]" - but that specific path is UNTESTED IN-GAME as of
    /// 2026-07-29, and long-term save stability beyond one reload cycle is
    /// untested for either kind of edit.</summary>
    public static string[] DescriptorsPath(int petIndex) => PetPath(petIndex).Append("osl").ToArray();

    /// <summary>E&lt;S[statIndex].1o6 - the same "1o6" Class-letter (S/A/B/C)
    /// convention Ships/Freighter/Multi-Tool use. CONFIRMED (2026-07-28) to
    /// directly set the in-game "Battle Abilities" badges (Combat
    /// Effectiveness/Agility/Health grades) whenever
    /// ClassLetterOverrideActivePath is true - see this class's doc comment
    /// for the full test. statIndex order does NOT match ujr/
    /// MutationPointsPath's Agility/Health/Combat order - confirmed via real
    /// testing (typed 3 distinct letters into each slot, compared to which
    /// in-game badge showed which letter): statIndex 0 = Health, statIndex
    /// 1 = Agility, statIndex 2 = Combat (only Combat's position
    /// matches ujr's order).</summary>
    public static string[] ClassLetterPath(int petIndex, int statIndex) =>
        PetPath(petIndex).Append("E<S").Append(statIndex.ToString()).Append("1o6").ToArray();

    // The remaining fields below (found 2026-07-28 dumping a real pet's full
    // structure while building the Pets page's "Advanced Fields" section)
    // aren't in this class's own doc comment because they were missed
    // entirely by the original CreatureSave cross-reference - either newer
    // than that pass, or just overlooked. No guess at their purpose exists
    // yet; named by rough type/position only (m9o and JrL were in this same
    // batch but have since been confirmed - see RollSeedPrimaryPath/
    // RollSeedSecondaryPath above - and moved out of this "unknown" group).
    // Observed on a real, fully leveled, 161-Holo-Arena-victory pet:
    // 5L6="0x303D00FB0F5921", Q6I=false, IaE=true, "?&lt;V"=false, eK9=false,
    // WQX=true, isp=false.

    /// <summary>CONFIRMED NOT a Battle Ability roll co-input (2026-07-28,
    /// real isolated test with RollSeedPrimaryPath/RollSeedSecondaryPath
    /// both held fixed - regenerating this field alone produced no change to
    /// the badges or species name). Purpose otherwise unknown.</summary>
    public static string[] UnknownHexCPath(int petIndex) => PetPath(petIndex).Append("5L6").ToArray();

    /// <summary>Toggled true (real test, 2026-07-28) with no observable
    /// effect anywhere on the in-game pet screen - Battle Abilities badges,
    /// species name, Traits, Weight/Height, Holo-Arena Victories, and
    /// Genetic Profile all stayed identical. Doesn't rule out an effect
    /// outside what's visible there (e.g. something server-side, or a UI
    /// panel this app hasn't cross-referenced). Purpose otherwise
    /// unknown.</summary>
    public static string[] UnknownBoolAPath(int petIndex) => PetPath(petIndex).Append("Q6I").ToArray();

    /// <summary>Toggled (real test, 2026-07-28) with no observable effect
    /// anywhere on the in-game pet screen - same fields checked as
    /// UnknownBoolAPath, all stayed identical. Purpose otherwise
    /// unknown.</summary>
    public static string[] UnknownBoolBPath(int petIndex) => PetPath(petIndex).Append("IaE").ToArray();
    /// <summary>Toggled (real test, 2026-07-28) with no observable effect
    /// anywhere on the in-game pet screen - same fields checked as
    /// UnknownBoolAPath, all stayed identical. Purpose otherwise
    /// unknown.</summary>
    public static string[] UnknownBoolCPath(int petIndex) => PetPath(petIndex).Append("?<V").ToArray();
    /// <summary>Toggled (real test, 2026-07-28) with no observable effect
    /// anywhere on the in-game pet screen - same fields checked as
    /// UnknownBoolAPath, all stayed identical. Purpose otherwise
    /// unknown.</summary>
    public static string[] UnknownBoolDPath(int petIndex) => PetPath(petIndex).Append("eK9").ToArray();
    /// <summary>Toggled true->false (real test, 2026-07-28 - this one
    /// started true on every sampled pet, unlike the others) with no
    /// observable effect anywhere on the in-game pet screen - same fields
    /// checked as UnknownBoolAPath, all stayed identical. Purpose otherwise
    /// unknown.</summary>
    public static string[] UnknownBoolEPath(int petIndex) => PetPath(petIndex).Append("WQX").ToArray();

    /// <summary>CONFIRMED (2026-07-28, real testing) to be a mode switch for
    /// the Battle Abilities badges: false (the default on every wild-tamed
    /// pet sampled) means the badges come from the RollSeedPrimaryPath/
    /// RollSeedSecondaryPath roll; true means the game reads ClassLetterPath
    /// (E&lt;S) directly instead, ignoring the roll fields. See this class's
    /// doc comment for the full test - toggling this true alone (with E&lt;S
    /// left at its dormant default S/S/S) immediately maxed all three
    /// badges, and setting E&lt;S to distinct letters while this stayed true
    /// made the badges show exactly those letters.</summary>
    public static string[] ClassLetterOverrideActivePath(int petIndex) => PetPath(petIndex).Append("isp").ToArray();
}
