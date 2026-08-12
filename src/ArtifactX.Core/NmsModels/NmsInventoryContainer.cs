using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON shape: one full inventory grid - confirmed for Exosuit Technology
/// (vLc/6f=/PMT) and Exosuit Cargo (vLc/6f=/;l5) against real save data.
/// Several sibling keys on the container itself (B@N, WA4, @bB, MMm, =Tb,
/// N9>, iF:, NKm, 2c0, a locally-scoped F2P) are NOT yet mapped - total grid
/// capacity (locked + unlocked) is likely one of these, or may be a fixed
/// game constant not stored here at all. Needs confirming before rendering
/// locked slots.
/// </summary>
public class NmsInventoryContainer
{
    /// <summary>Occupied slots only - cells that actually contain an item.</summary>
    [JsonProperty(":No")]
    public List<NmsInventorySlot> OccupiedSlots { get; set; } = new();

    /// <summary>Unlocked slot positions - coordinates only, no item data.</summary>
    [JsonProperty("hl?")]
    public List<NmsGridPosition> UnlockedPositions { get; set; } = new();

    // Both confirmed Exosuit containers point here - identical shape, only
    // the JSON location differs.
    public static readonly string[] ExosuitTechnologyPath = { "vLc", "6f=", "PMT" };
    public static readonly string[] ExosuitCargoPath = { "vLc", "6f=", ";l5" };

    // Multi-tools live in an array (SuJ) of up to 6 slots, only some of which are
    // owned. Each owned entry has exactly one inventory container (OsQ,
    // WA4.rri == "Default") - confirmed against real save data; unlike ships
    // there's no separate cargo container. "SuJ" itself is a sibling of the
    // array of ships (@Cs), both under PlayerStateData. Occupancy is NTx.93M
    // (model scene path) non-empty, NOT NKm (Name) == "" - a brand-new save's
    // starter tool is genuinely owned (real scene path, real seed) but hasn't
    // had a Name written yet, so NKm=="" there too; confirmed 2026-08-10 by
    // decrypting a save that hadn't left the starting planet, where MultiToolPage
    // was filtering the real starter tool out entirely on the old NKm check.
    public static readonly string[] MultiToolArrayPath = { "vLc", "6f=", "SuJ" };

    /// <summary>Path to one owned multi-tool's Technology container. The engine's
    /// path resolver (SaveEditSession.Resolve/Apply) already treats a numeric
    /// segment as an array index when the current node is a JArray - no special
    /// handling needed here beyond building the right string path.</summary>
    public static string[] MultiToolTechnologyPath(int toolIndex) =>
        new[] { "vLc", "6f=", "SuJ", toolIndex.ToString(), "OsQ" };

    /// <summary>Top-level mirror of whichever multi-tool is CURRENTLY EQUIPPED -
    /// confirmed via real save diff to hold a live copy of that tool's inventory,
    /// separate from its own stored copy at SuJ[i].OsQ. The two are kept in sync
    /// by the game, but which one it actually reads/writes at runtime is unclear -
    /// so when the selected tool IS the active one, edit THIS path instead of its
    /// SuJ entry, to avoid a risk of edits landing somewhere the game ignores or
    /// getting clobbered by a later Kgt->SuJ resync.</summary>
    public static readonly string[] MultiToolActivePath = { "vLc", "6f=", "Kgt" };

    // Ships live in @Cs, the sibling array to SuJ noted above - up to 12 slots,
    // only some owned. Confirmed against real save data: each owned entry has
    // NKm (name), NTx.93M (model scene path), PMT (Technology container,
    // 12-key shape matching this class - B@N.1o6 Class letter confirmed), and
    // ;l5 (Cargo container, same 12-key shape). A third container-shaped
    // sibling, "gan", also exists per-ship but its purpose is unconfirmed
    // (empty in every save sampled) - deliberately not exposed here.
    // Occupancy is NTx.93M non-empty, NOT NKm == "" - see MultiToolArrayPath's
    // comment above for why (same fresh-save bug, same fix, same date).
    public static readonly string[] ShipArrayPath = { "vLc", "6f=", "@Cs" };

    public static string[] ShipTechnologyPath(int shipIndex) =>
        new[] { "vLc", "6f=", "@Cs", shipIndex.ToString(), "PMT" };

    public static string[] ShipCargoPath(int shipIndex) =>
        new[] { "vLc", "6f=", "@Cs", shipIndex.ToString(), ";l5" };

    /// <summary>A ship's own rolled bonus stats - same "continuous stat roll"
    /// shape/editing pattern as Multi-Tool's OsQ.@bB and Freighter's
    /// FreighterStatBonusesPath. Confirmed via real save data across 7 owned
    /// ships: every ship carries exactly "^SHIP_DAMAGE"/"^SHIP_SHIELD"/
    /// "^SHIP_HYPERDRIVE"/"^SHIP_AGILE" (plus "^ALIEN_SHIP" on at least one
    /// Living Ship, left alone by SetStatValue's key-matched rebuild), and the
    /// magnitudes tracked Class hard: a lone C-class Fighter sampled near zero
    /// on every stat (9.5/0/0/10.1) while every S-class ship sampled well
    /// above it (Damage up to 77, Shield up to 115, Hyperdrive up to 93, Agile
    /// up to 43) - confirming Class only gates the roll RANGE at generation
    /// time, not a value read live off the Class letter. This is exactly why
    /// ShipsPage's Class buttons (SetClass) alone don't make a ship
    /// perform any better - they never touched this array.</summary>
    public static string[] ShipStatBonusesPath(int shipIndex) =>
        ShipTechnologyPath(shipIndex).Append("@bB").ToArray();

    /// <summary>A ship's model seed - the hex value that (together with its
    /// model scene path) drives the ship's procedural look, same "@EL"
    /// second-element shape as Freighter's Model Seed (bIR.@EL[1]) and Crew
    /// Seed (Sjw.@EL[1]). Confirmed via real save data: every owned ship's
    /// "NTx" object (already read for its scene path - see
    /// ShipsPage.GetShipTypeAndClass) carries the exact same [bool, hex
    /// string] "@EL" pair. One sampled Living Ship ("The Wraith") had seed
    /// "0x0" - those are quest-crafted rather than seed-rolled, so editing
    /// this field on one may not visibly change anything in-game.</summary>
    public static string[] ShipModelSeedPath(int shipIndex) =>
        new[] { "vLc", "6f=", "@Cs", shipIndex.ToString(), "NTx", "@EL", "1" };

    /// <summary>Plain integer index into @Cs of whichever ship is CURRENTLY
    /// EQUIPPED - unlike Multi-Tool's Kgt (a full mirrored container), this is
    /// just a scalar pointer. Confirmed against 15 real save files across 3
    /// different characters/saves: this value always pointed at a genuinely
    /// owned (non-empty NKm) ship, and was NOT simply the count of owned ships
    /// (several samples had aBE != owned-ship-count, ruling that out) - e.g. one
    /// save had 6 owned ships but aBE=5, pointing at a specific one of them by
    /// index, not the count.</summary>
    public static readonly string[] ActiveShipIndexPath = { "vLc", "6f=", "aBE" };

    // Unlike ships/multi-tools, a player only ever actively manages ONE
    // freighter's own Technology/Cargo for editing purposes (owning a wider
    // Frigate Fleet - the "nlG" array - is a separate, much bigger system left
    // untouched here) - so these are plain top-level siblings, not array
    // entries, matching Exosuit's own pattern. Identified via real save data:
    // "0wS" holds unambiguous Freighter Technology items (^F_HYPERDRIVE,
    // ^F_TELEPORT, ^F_SCANNER, ^F_HACCESS*, ^F_HDRIVEBOOST*) and its unlocked
    // count (30) exactly matched the real FREIGHTER_C_TECH catalog value
    // already extracted for Ships (GcInventoryTable also indexes a "Freighter"
    // ship type). "8ZP" is Freighter Cargo - confirmed directly by the user:
    // its single occupied item (^U_LASER1) resolves in the catalog to "MINING
    // BEAM MODULE" (GcProductTable), exactly matching what they saw in-game,
    // and its unlocked count (60) exactly matches FREIGHTER_C_CARGO. An
    // earlier guess ("Bq<") looked plausible from item content (bulk crafting
    // materials) but was confirmed WRONG by the user - that's actually one of
    // their player-placed Base Storage Containers (BS0-BS9), a same-shaped but
    // unrelated container.
    public static readonly string[] FreighterTechnologyPath = { "vLc", "6f=", "0wS" };
    public static readonly string[] FreighterCargoPath = { "vLc", "6f=", "8ZP" };

    /// <summary>The freighter's own rarity/quality Class (S/A/B/C) badge -
    /// fixed 2026-08-12, same root cause and same fix pattern as
    /// FreighterStatBonusesPath: 0wS (Technology) has its own independent
    /// B@N.1o6 field too, and it's the one this used to read/write. A user
    /// insisted their freighter was Class B in-game while ArtifactX showed
    /// S; checking the save directly found 0wS.B@N.1o6="S" but BOTH 8ZP
    /// (Cargo) and a third, unused mirror container ("FdP", same 12-key
    /// shape as 0wS/8ZP) independently agreed on "B" - two containers
    /// against one, matching the user's own live observation. Now reads/
    /// writes 8ZP only; 0wS's own copy is left as-is (not kept in sync) -
    /// same treatment as FreighterStatBonusesPath, since it's already
    /// confirmed to not affect anything the game displays.
    ///
    /// Residual uncertainty: the earlier (wrong) reasoning that "Storage
    /// Space: 120 matches FREIGHTER_S_CARGO" seemed to confirm Class S was
    /// real - it doesn't fit Class B's own FREIGHTER_B_CARGO (80) either,
    /// so Storage Space's exact derivation still isn't fully understood
    /// (it may include a bonus from installed Construction Module upgrades
    /// that ArtifactX's own capacity lookup doesn't model at all) - not
    /// chased further, since the S-vs-B question itself is what mattered
    /// here and is now resolved with strong, multi-container evidence.</summary>
    public static string[] FreighterClassPath => FreighterCargoPath.Append("B@N").Append("1o6").ToArray();

    /// <summary>The active freighter's display name - a plain string, not
    /// nested inside a container. Confirmed via real save data: renaming the
    /// freighter in-game and re-saving changed exactly this value.</summary>
    public static readonly string[] FreighterNamePath = { "vLc", "6f=", "vxi" };

    /// <summary>The freighter hull's model scene path ("Type" - Normal/Capital/
    /// Dreadnought). Confirmed via cross-referencing 3 different real
    /// characters' saves plus a reference tool showing the same three
    /// options for the same freighters. "@EL"'s second element (index 1) is
    /// that reference's own "Model Seed" - confirmed by an exact value match
    /// against a real save.</summary>
    public static readonly string[] FreighterTypePath = { "vLc", "6f=", "bIR", "93M" };
    public static readonly string[] FreighterModelSeedPath = { "vLc", "6f=", "bIR", "@EL", "1" };

    /// <summary>The freighter's "Home Seed" - a top-level [bool, hex] pair
    /// directly under the freighter object (same shape as Model Seed's
    /// bIR.@EL and Crew Seed's Sjw.@EL, but its own separate field, "kYq"),
    /// found 2026-08-12 via a real value match against a reference tool's own
    /// "Home Seed" field. Unlike Model/Crew Seed, this is only 12 hex digits
    /// (48-bit) rather than 16 (64-bit) - and its decimal value was confirmed
    /// to exactly equal this same freighter's own PersistentPlayerBase
    /// GalacticAddress ("F?0" array, the entry whose peI.DPp is
    /// "FreighterBase", field "oZw"). That strongly suggests "Home Seed" is
    /// actually the freighter's registered home system address rather than a
    /// cosmetic procedural-look seed like Model/Crew Seed - so unlike those
    /// two, this is NOT wired into NmsSeedGenerator's random-reroll button or
    /// into Full Build Templates: randomizing or copying a location address
    /// onto a different save's freighter could have consequences beyond
    /// appearance that haven't been tested. Exposed as a plain paste/copy
    /// field only, to match what a reference tool shows.</summary>
    public static readonly string[] FreighterHomeSeedPath = { "vLc", "6f=", "kYq", "1" };

    /// <summary>The freighter's crew captain - a SEPARATE model reference from
    /// the hull itself (bIR above), confirmed via real save data: the
    /// captain's model path (".../NPCVYKEEN.SCENE.MBIN") and its seed
    /// (@EL[1]) exactly matched a reference tool's own "Crew Race" and "Crew
    /// Seed" fields for the same freighter.</summary>
    public static readonly string[] FreighterCrewRacePath = { "vLc", "6f=", "Sjw", "93M" };
    public static readonly string[] FreighterCrewSeedPath = { "vLc", "6f=", "Sjw", "@EL", "1" };

    /// <summary>Freighter Cargo's (NOT Technology's) continuous stat bonuses
    /// array - fixed 2026-08-12, a real bug found via a user-reported stat
    /// mismatch. The ORIGINAL path here was FreighterTechnologyPath (0wS),
    /// which does hold its own "^FREI_HYPERDRIVE"/"^FREI_FLEET"-keyed @bB
    /// array too - identical key NAMES, which is exactly why the original
    /// "confirmed... matching a reference tool's own Base Stats fields" claim
    /// was wrong: it matched on key names alone, never checked the actual
    /// VALUES. Editing that path silently did nothing in-game - confirmed by
    /// the user setting both stats to 500 there, saving, reloading, and
    /// seeing no matching change. A recursive full-save search for the exact
    /// values a reference tool displayed (14.440342903137207,
    /// 16.516082763671875) found them at FreighterCargoPath's own @bB
    /// instead - an EXACT floating-point match, not a formula/approximation.
    /// A third container, "FdP" (same 12-key shape as 0wS/8ZP, not otherwise
    /// used by this app), mirrors 8ZP's @bB exactly - likely a live-active
    /// mirror analogous to Multi-Tool's Kgt, not independently investigated
    /// further since 8ZP is the one a live reference-tool read confirmed.</summary>
    public static string[] FreighterStatBonusesPath => FreighterCargoPath.Append("@bB").ToArray();

    /// <summary>Player-placed Base Storage Containers ("Chests") - unlike every
    /// other container on this class, these are scattered top-level siblings
    /// under vLc/6f= with NO fixed/known key names (each container gets a
    /// fresh short key when placed), so there's no static path list here the
    /// way ShipTechnologyPath/etc. have. Confirmed via real save data: renaming
    /// 10 containers in-game to "BS0".."BS9" and searching the whole save found
    /// all 10 as top-level 12-key containers, each with WA4.rri == "Chest" -
    /// that field is the reliable structural marker WinUI3's discovery code
    /// uses to find every owned container regardless of how many exist or what
    /// they're named. Capacity is fixed (BaseStorageCapacity), not Class-based -
    /// B@N.1o6 was uniformly "C" with no in-game way to change it.
    ///
    /// WA4.rri=="Chest" alone isn't ownership though - confirmed 2026-08-11
    /// (see [[project_fresh_save_bug_sweep]]) that a genuinely zero-progress
    /// save already has 13 such containers pre-allocated from the start,
    /// same pre-allocated-pool pattern as every other array on this class.
    /// A controlled test (10 real containers built and named BASE0-BASE9
    /// purely to verify against the raw save) showed the real 10 are always
    /// the FIRST 10 of the 13 Chest-shaped entries in the save's own
    /// property order, with the same trailing 3 permanently unreachable in
    /// extensive in-game testing - matching the in-game UI's own 0-9
    /// container numbering. BaseStoragePage's DiscoverContainers() takes
    /// only those first 10 - see that method's own doc comment for the full
    /// evidence trail.</summary>
    public static string[] BaseStorageContainerPath(string containerKey) =>
        new[] { "vLc", "6f=", containerKey };

    // Exocraft live in a FIXED 7-slot array, one slot per GcVehicleType.
    // VehicleTypeEnum member - see ExocraftType's own doc comment for the full
    // confirmation trail (libMBIN's fixed Size=0x7 declarations, cross-checked
    // stable across 3 real saves, real names from the game's own loc strings).
    // Unlike Ships/Multi-Tool this array's SIZE never varies and its POSITIONS
    // never reorder, so there's no "array path + numeric index of an owned
    // entry" concept here, just a direct type -> fixed index map. Unlike
    // Ships' NKm, NTx.93M being empty is NOT a reliable "not built yet"
    // signal - a real save showed a fully tech'd-out, actively-used Colossus
    // (300/300 Fusion Engine, real quantities) with an empty NTx.93M, so
    // ArtifactX doesn't attempt to detect/show a "built" state at all here.
    public static readonly string[] ExocraftArrayPath = { "vLc", "6f=", "P;m" };

    public static string[] ExocraftTechnologyPath(ExocraftType type) =>
        new[] { "vLc", "6f=", "P;m", ((int)type).ToString(), "PMT" };

    public static string[] ExocraftCargoPath(ExocraftType type) =>
        new[] { "vLc", "6f=", "P;m", ((int)type).ToString(), ";l5" };

    /// <summary>Same "@EL" second-element shape as every other confirmed
    /// procedural seed in this app (Ships/Freighter hull+crew).</summary>
    public static string[] ExocraftModelSeedPath(ExocraftType type) =>
        new[] { "vLc", "6f=", "P;m", ((int)type).ToString(), "NTx", "@EL", "1" };

    /// <summary>The Corvette Workshop Cache - a single top-level sibling (not
    /// array-indexed; there's only ever one), holding the proc-gen building
    /// parts used to construct/upgrade a player's Corvette. Confirmed via real
    /// save data: found by its distinctive 500-max-stack fingerprint (13 items,
    /// all F9q==500), then verified via WA4.rri == "BaseCapsule" (a role value
    /// not seen on any other container) and by cross-referencing all 13 item
    /// ids (^B_DECO_A, ^B_WNG_I, etc.) against the catalog - every one resolves
    /// under GcProductTable/UsageCategory "BuildingPart" to a real part name
    /// (e.g. "MEDUSA-CLASS REACTOR", "THUNDERBIRD-CLASS COCKPIT") matching the
    /// in-game panel exactly. Capacity is CorvetteCacheCapacity (10x16=160),
    /// also confirmed via this container's own =Tb/N9> fields.</summary>
    public static readonly string[] CorvetteCachePath = { "vLc", "6f=", "wem" };
}