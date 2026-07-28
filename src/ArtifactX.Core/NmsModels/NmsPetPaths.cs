using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for the Animal Companions ("Pets") array. Unlike the
/// standard NmsInventoryContainer shape, a pet entry is NOT a :No/hl?
/// inventory grid - it's a flat 30-slot array of per-pet stat objects, one
/// slot per owned/tamed pet (empty slots have fH8 == "").
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
///    construction "recipe", separate from the seeds below.
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
///    confirmed via exact "X/10" matches. E&lt;S is CONFIRMED NOT to drive the
///    in-game "Battle Abilities" badges (Combat Effectiveness/Agility/
///    Health) it was originally guessed to control - real-save testing
///    forced one pet's E&lt;S to S/S/S across multiple saves/reloads with its
///    badges staying exactly what they were before the edit, a second,
///    never-edited pet's badges (S/C/C) didn't match its own untouched E&lt;S
///    (C/C/C), grinding a third pet's actual mutation POINTS up through real
///    battles didn't move its badges either, and a WTp/6fX reroll didn't
///    either. A PAK search for a per-species stat table (gccreatureglobals.
///    mbin) and the Creature-Builder contract itself (no such field exists
///    anywhere in it) both came up empty too. Working conclusion: these
///    badges are computed by the game's own logic from data outside what
///    save-editing or MBIN data tables can reach, not stored anywhere in
///    this object - E&lt;S is deliberately not exposed for editing.
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
    /// Confirmed via real reroll-and-reload testing to drive color variance
    /// (see this class's doc comment); BoneScaleSeedPath below is kept in
    /// sync with this one on every reroll since every sampled pet showed
    /// them mirrored.</summary>
    public static string[] SeedPath(int petIndex) => PetPath(petIndex).Append("WTp").Append("1").ToArray();

    /// <summary>BoneScaleSeed - always mirrored WTp's value in every sample
    /// gathered so far. Not exposed as its own editable field; only ever
    /// written by GenerateSeedBtn_Click alongside SeedPath, to preserve that
    /// observed mirror rather than letting the two diverge into an untested
    /// state.</summary>
    public static string[] BoneScaleSeedPath(int petIndex) => PetPath(petIndex).Append("6fX").Append("1").ToArray();
}
