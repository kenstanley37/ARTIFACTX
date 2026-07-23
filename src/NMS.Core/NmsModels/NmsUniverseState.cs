using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NMS.Core.NmsModels;

public class NmsUniverseState
{
    // Galaxy index
    [JsonProperty("idA")]
    public int GalaxyIndex { get; set; }

    // Current system raw block
    [JsonProperty("6f=")]
    public JToken? RawSystem { get; set; }

    // Location description
    [JsonProperty("n:R")]
    public string? LocationDescription { get; set; }

    // Currency values
    [JsonProperty("wGS")]
    public long Units { get; set; }

    [JsonProperty("7QL")]
    public long Nanites { get; set; }

    [JsonProperty("kN;")]
    public long Quicksilver { get; set; }
}