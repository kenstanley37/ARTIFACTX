using Newtonsoft.Json;

namespace NMS.Core.NmsModels;

/// <summary>
/// JSON Path: /
/// Represents the top-level save metadata block.
/// Keys:
///   F2P  -> Total play time (seconds)
///   8>q  -> Platform/build identifier (e.g., "Win|Final")
///   XTp  -> Save type ("Main", "Expedition", etc.)
///   <h0  -> Save header block (contains save name, flags, player state)
///   vLc  -> Universe / currency block
/// </summary>
public class NmsSaveFile
{
    // Total play time in seconds
    [JsonProperty("F2P")]
    public int PlayTimeSeconds { get; set; }

    // Platform/build string (e.g., "Win|Final")
    [JsonProperty("8>q")]
    public string PlatformBuild { get; set; }

    // Save type: "Main", "Expedition", etc.
    [JsonProperty("XTp")]
    public string SaveType { get; set; }

    // Save header block (<h0)
    [JsonProperty("<h0")]
    public NmsSaveHeader Header { get; set; }

    // Universe / currency block (vLc)
    [JsonProperty("vLc")]
    public NmsUniverseState Universe { get; set; }

    /// <summary>
    /// Convenience shim: exposes currency values at the top level
    /// even though they actually live under vLc.
    /// </summary>
    [JsonIgnore]
    public NmsCurrencyState CurrencyData =>
        Universe == null
            ? null
            : new NmsCurrencyState
            {
                Units = Universe.Units,
                Nanites = Universe.Nanites,
                Quicksilver = Universe.Quicksilver
            };
}
