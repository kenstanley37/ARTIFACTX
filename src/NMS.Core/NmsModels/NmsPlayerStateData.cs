using Newtonsoft.Json;

namespace NMS.Core.NmsModels;

/// <summary>
/// JSON Path: /vLc/6f=
/// Community-documented NMS saves call this block PlayerStateData. It holds far
/// more than currency - location, tutorial flags, base-building slot budgets -
/// roughly 258 keys total. Only the fields below are mapped so far; this class
/// is read/reference-only (used for lightweight previews), never deserialized
/// then reserialized - live edits always go through SaveSessionManager's JSON
/// tree directly, using the path constants below, so unmapped keys are never at risk.
/// Keys:
///   n:R -> Current location description (e.g. "On Planet (Goyptianu)")
///   wGS -> Units
///   7QL -> Nanites
///   kN; -> Quicksilver
/// </summary>
public class NmsPlayerStateData
{
    [JsonProperty("n:R")]
    public string? LocationDescription { get; set; }

    [JsonProperty("wGS")]
    public long Units { get; set; }

    [JsonProperty("7QL")]
    public long Nanites { get; set; }

    [JsonProperty("kN;")]
    public long Quicksilver { get; set; }

    // Path constants for the live-edit engine. Full parent chain included, so
    // a page can reference NmsPlayerStateData.UnitsPath directly with no need
    // to know vLc/6f= exist as separate steps.
    public static readonly string[] LocationDescriptionPath = { "vLc", "6f=", "n:R" };
    public static readonly string[] UnitsPath = { "vLc", "6f=", "wGS" };
    public static readonly string[] NanitesPath = { "vLc", "6f=", "7QL" };
    public static readonly string[] QuicksilverPath = { "vLc", "6f=", "kN;" };
}