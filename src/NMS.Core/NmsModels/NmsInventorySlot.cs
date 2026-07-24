using Newtonsoft.Json;

namespace NMS.Core.NmsModels;

/// <summary>
/// JSON shape: one occupied slot inside any inventory container's :No array.
/// Confirmed identical across every inventory type checked so far (Exosuit
/// Tech/Cargo; the same shape also appears under Ship/Multi-Tool/Vehicle tech).
/// Values verified against a real save's in-game display (e.g. Gold 6,496;
/// Nav Data 24/40) - not guessed.
/// </summary>
public class NmsInventorySlot
{
    [JsonProperty("b2n")]
    public string? ItemId { get; set; }

    [JsonProperty("1o9")]
    public int Amount { get; set; }

    [JsonProperty("F9q")]
    public int MaxAmount { get; set; }

    [JsonProperty("Vn8")]
    public NmsInventoryCategory? Category { get; set; }

    [JsonProperty("3ZH")]
    public NmsGridPosition? Position { get; set; }

    // Seen values so far: always true/false/0.0 respectively across every
    // slot checked. Meaning not yet confirmed - do not assume.
    [JsonProperty("b76")]
    public bool FlagB76 { get; set; }

    [JsonProperty("5tH")]
    public bool Flag5tH { get; set; }

    [JsonProperty("eVk")]
    public float FlagEvk { get; set; }
}

public class NmsInventoryCategory
{
    /// <summary>"Technology", "Product", "Substance" - seen so far.</summary>
    [JsonProperty("elv")]
    public string? Label { get; set; }
}

public class NmsGridPosition
{
    [JsonProperty(">Qh")]
    public int X { get; set; }

    [JsonProperty("XJ>")]
    public int Y { get; set; }
}