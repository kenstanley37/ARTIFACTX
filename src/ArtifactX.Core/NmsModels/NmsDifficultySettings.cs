using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /vLc/6f=/LyC
/// Unlike this file's siblings, this mapping is NOT community-documented - it's
/// inferred from libMBIN's GcDifficultySettingsReplicatedState (four
/// GcDifficultyPresetType fields in Index order: IsPermadeath(0), Preset(1),
/// RoundedDownPreset(2), EasiestUsedPreset(3), HardestUsedPreset(4), IsLocked(5))
/// and cross-checked against real save dumps, where "brJ" was consistently the
/// first of three visible preset-shaped children under LyC (the two bools and
/// HardestUsedPreset appear to be omitted here, matching this format's usual
/// default-value compaction). Self-consistent across every sample checked so
/// far, but none of those samples were a known-Permadeath or known-Survival
/// save, so the specific label attribution (as opposed to "a preset value
/// lives here") is unconfirmed - flag it if a slot ever shows the wrong mode.
/// Keys:
///   brJ -> Current preset (GcDifficultyPresetType, itself nested one level via "7ND")
/// </summary>
public class NmsDifficultySettings
{
    [JsonProperty("brJ")]
    public NmsDifficultyPreset? Preset { get; set; }

    [JsonIgnore]
    public string GameMode => Preset?.DisplayName ?? "Unknown";
}

/// <summary>
/// JSON Path: .../LyC/brJ
/// Mirrors libMBIN's GcDifficultyPresetType, which wraps a single enum field -
/// the raw value already matches the desired display text 1:1 (Normal,
/// Creative, Relaxed, Survival, Permadeath, Custom), so no translation table
/// is needed beyond falling back on an unrecognized/missing value.
/// </summary>
public class NmsDifficultyPreset
{
    [JsonProperty("7ND")]
    public string? RawValue { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrEmpty(RawValue) || RawValue == "Invalid"
        ? "Unknown"
        : RawValue;
}
