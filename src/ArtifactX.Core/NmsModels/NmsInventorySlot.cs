using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

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

    /// <summary>True on every slot we'd ever seen until a real before/after test:
    /// a freshly-added item still missing required parts showed b76=false, then
    /// flipped to true after repairing it in-game (confirmed via save diff).
    /// Independent of Amount/MaxAmount, which barely moved in that same test.
    /// Best current understanding: functional/malfunctioning flag - only one
    /// confirmed transition observed so far, treat as strong evidence, not
    /// exhaustively proven across every tech type.</summary>
    [JsonProperty("b76")]
    public bool FlagB76 { get; set; }

    // Seen values so far: always false across every slot checked. Meaning not
    // yet confirmed - do not assume.
    [JsonProperty("5tH")]
    public bool Flag5tH { get; set; }

    /// <summary>Moved in lockstep with FlagB76 in the one confirmed test (1.0
    /// when the item was non-functional, 0.0 once repaired), so likely a
    /// malfunction-severity float rather than a flat flag - though only those
    /// two values (0.0/1.0) have been observed, nothing in between.</summary>
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