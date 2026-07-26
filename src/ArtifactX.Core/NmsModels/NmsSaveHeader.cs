using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /<h0
/// Represents the save header block.
/// Keys:
///   Pk4 -> Save name (user-defined)
///   Lg8 -> Save version / revision
///   kPF -> NOT a reliable "this file was a manual save" discriminator -
///          observed True on every real sample checked (including autosave
///          writes), so it doesn't vary the way the name implies.
///   wb: -> Same caveat as kPF - observed True on every real sample checked.
///   Wh< -> NOT "this save file is from Expedition mode" - that's
///          NmsSaveFile.SaveType (XTp) instead. Observed False only on
///          freshly-created characters and True on every established save
///          regardless of current game mode, so this more likely tracks
///          "has this character ever touched Expedition content" (Expedition
///          rewards are known to carry over permanently into a player's main
///          save) - unconfirmed, but confidently not a per-file save-type flag.
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
    public bool HasExpeditionHistory { get; set; }

    [JsonProperty("j3Y")]
    public NmsPlayerHazardState HazardState { get; set; }
}
