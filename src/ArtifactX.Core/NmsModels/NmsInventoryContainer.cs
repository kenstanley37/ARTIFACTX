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
    // owned (empty slots have NKm == ""). Each owned entry has exactly one
    // inventory container (OsQ, WA4.rri == "Default") - confirmed against real
    // save data; unlike ships there's no separate cargo container. "SuJ" itself
    // is a sibling of the array of ships (@Cs), both under PlayerStateData.
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
    // only some owned (empty slots have NKm == ""). Confirmed against real save
    // data: each owned entry has NKm (name), NTx.93M (model scene path), PMT
    // (Technology container, 12-key shape matching this class - B@N.1o6 Class
    // letter confirmed), and ;l5 (Cargo container, same 12-key shape). A third
    // container-shaped sibling, "gan", also exists per-ship but its purpose is
    // unconfirmed (empty in every save sampled) - deliberately not exposed here.
    public static readonly string[] ShipArrayPath = { "vLc", "6f=", "@Cs" };

    public static string[] ShipTechnologyPath(int shipIndex) =>
        new[] { "vLc", "6f=", "@Cs", shipIndex.ToString(), "PMT" };

    public static string[] ShipCargoPath(int shipIndex) =>
        new[] { "vLc", "6f=", "@Cs", shipIndex.ToString(), ";l5" };

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

    /// <summary>The active freighter's display name - a plain string, not
    /// nested inside a container. Confirmed via real save data: renaming the
    /// freighter in-game and re-saving changed exactly this value.</summary>
    public static readonly string[] FreighterNamePath = { "vLc", "6f=", "vxi" };

    /// <summary>The freighter hull's model scene path ("Type" - Normal/Capital/
    /// Dreadnought). Confirmed via cross-referencing 3 different real
    /// characters' saves plus NomNom (an established NMS save editor) showing
    /// the same three options for the same freighters. "@EL"'s second element
    /// (index 1) is NomNom's "Model Seed" - confirmed by an exact value match
    /// against a real save.</summary>
    public static readonly string[] FreighterTypePath = { "vLc", "6f=", "bIR", "93M" };
    public static readonly string[] FreighterModelSeedPath = { "vLc", "6f=", "bIR", "@EL", "1" };

    /// <summary>The freighter's crew captain - a SEPARATE model reference from
    /// the hull itself (bIR above), confirmed via real save data: the
    /// captain's model path (".../NPCVYKEEN.SCENE.MBIN") and its seed
    /// (@EL[1]) exactly matched NomNom's "Crew Race" and "Crew Seed" fields
    /// for the same freighter.</summary>
    public static readonly string[] FreighterCrewRacePath = { "vLc", "6f=", "Sjw", "93M" };
    public static readonly string[] FreighterCrewSeedPath = { "vLc", "6f=", "Sjw", "@EL", "1" };

    /// <summary>Freighter Technology's continuous stat bonuses array - same
    /// shape and editing pattern as Multi-Tool's OsQ.@bB (WEAPON_DAMAGE/etc.).
    /// Confirmed via real save data: holds exactly two entries keyed
    /// "^FREI_HYPERDRIVE" and "^FREI_FLEET", matching NomNom's "Hyperdrive"
    /// and "Fleet Coordination" Base Stats fields.</summary>
    public static string[] FreighterStatBonusesPath => FreighterTechnologyPath.Append("@bB").ToArray();

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
    /// B@N.1o6 was uniformly "C" with no in-game way to change it.</summary>
    public static string[] BaseStorageContainerPath(string containerKey) =>
        new[] { "vLc", "6f=", containerKey };

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