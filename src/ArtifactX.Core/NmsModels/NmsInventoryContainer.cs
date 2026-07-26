using Newtonsoft.Json;
using System.Collections.Generic;

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
}