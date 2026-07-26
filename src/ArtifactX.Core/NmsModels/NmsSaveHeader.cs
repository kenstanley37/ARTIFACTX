using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /<h0
/// Represents the save header block.
/// Keys:
///   Pk4 -> Save name (user-defined)
///   Lg8 -> Save version / revision
///   kPF -> Manual save flag (restore point)
///   wb: -> Auto save flag
///   Wh< -> Expedition save flag
///   j3Y -> Player state block
/// </summary>
public class NmsSaveHeader
{
    [JsonProperty("Pk4")]
    public string SaveName { get; set; }

    [JsonProperty("Lg8")]
    public int SaveVersion { get; set; }

    [JsonProperty("kPF")]
    public bool IsManualSave { get; set; }

    [JsonProperty("wb:")]
    public bool IsAutoSave { get; set; }

    [JsonProperty("Wh<")]
    public bool IsExpeditionSave { get; set; }

    [JsonProperty("j3Y")]
    public NmsPlayerHazardState HazardState { get; set; }
}
