using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /
/// Represents the top-level save metadata block.
/// Keys:
///   F2P  -> NOT total/lifetime play time despite this class's original
///           field name - see PlayTimeSeconds' own doc comment.
///   8>q  -> Platform/build identifier (e.g., "Win|Final")
///   XTp  -> Save type ("Main", "Expedition", etc.)
///   <h0  -> Save header block (contains save name, flags, player state)
///   vLc  -> Universe / currency block
/// </summary>
public class NmsSaveFile
{
    /// <summary>NOT this save's total/lifetime play time, despite the
    /// original assumption baked into this property's name - caught
    /// 2026-08-06 when the Save Selection page showed "1h 18m" for a
    /// character with ~2 billion nanites and other clear signs of long-term
    /// play. Confirmed by direct inspection: F2P read 4733 (~1h19m) on that
    /// exact save, while GLOBAL_STATS' own "TIME" stat (see
    /// NmsPlayerStatsPaths, read via SaveFolderIndexingService's
    /// ReadTotalPlayTimeSeconds since that runs before any slot is loaded
    /// into SaveSessionManager) read 585337.25 seconds (~162.6 hours) on the
    /// SAME file - a far more plausible lifetime total. F2P is more likely a
    /// current-session or since-last-continue counter instead, unconfirmed
    /// which exactly. Kept mapped (not removed) since it may still be useful
    /// for something later, but nothing in this app should treat this as
    /// "total play time" going forward.</summary>
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
