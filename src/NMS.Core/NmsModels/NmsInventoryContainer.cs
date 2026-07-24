using Newtonsoft.Json;
using System.Collections.Generic;

namespace NMS.Core.NmsModels;

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
}