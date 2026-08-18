using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /vLc
/// Keys:
///   idA -> Galaxy index
///   6f= -> Player state block (see NmsPlayerStateData) - location, currency, etc.
///   rnc -> Spawn state block (position/rotation-shaped entries) - not yet modeled
/// </summary>
public class NmsUniverseState
{
    [JsonProperty("idA")]
    public int GalaxyIndex { get; set; }

    [JsonProperty("6f=")]
    public NmsPlayerStateData? PlayerState { get; set; }

    [JsonProperty("rnc")]
    public JToken? SpawnState { get; set; }

    [JsonIgnore]
    public string? LocationDescription => PlayerState?.LocationDescription;

    [JsonIgnore]
    public long Units => PlayerState?.Units ?? 0;

    [JsonIgnore]
    public long Nanites => PlayerState?.Nanites ?? 0;

    [JsonIgnore]
    public long Quicksilver => PlayerState?.Quicksilver ?? 0;

    [JsonIgnore]
    public string GameMode => PlayerState?.GameMode ?? "Unknown";
}