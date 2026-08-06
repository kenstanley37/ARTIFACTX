using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /vLc/6f=/LyC
/// Unlike this file's siblings, this mapping is NOT community-documented - it's
/// inferred from libMBIN's GcDifficultySettingsReplicatedState (four
/// GcDifficultyPresetType fields in Index order: IsPermadeath(0), Preset(1),
/// RoundedDownPreset(2), EasiestUsedPreset(3), HardestUsedPreset(4), IsLocked(5)).
/// CONFIRMED 2026-08-06 by cross-checking real saves tagged Normal/Survival/
/// Permadeath/Custom on the Save Selection page: brJ/qAf/4I: track together
/// (all "Normal" on a Normal save, all "Survival" on a Survival save, all
/// "Permadeath" on a Permadeath save) EXCEPT on the Custom save, where they
/// genuinely diverge (brJ=Custom, qAf=Creative, 4I:=Normal) - proving these
/// are 3 distinct tracked values, not one value read 3 different ways.
/// IsPermadeath/HardestUsedPreset(4)/IsLocked were never observed populated
/// in any of those 4 samples - the save format compacts/omits fields at
/// their default, and this format's default-value compaction evidently
/// includes these bools/HardestUsedPreset in every real sample checked so far.
/// Keys:
///   brJ -> Current preset (index 1, GcDifficultyPresetType nested via "7ND")
///   qAf -> Rounded-down preset (index 2) - when brJ is Custom, this is the
///          closest standard preset the game itself considers it equivalent to
///   4I: -> Easiest used preset (index 3) - the easiest standard preset this
///          save has ever actually been set to
/// </summary>
public class NmsDifficultySettings
{
    [JsonProperty("brJ")]
    public NmsDifficultyPreset? Preset { get; set; }

    [JsonProperty("qAf")]
    public NmsDifficultyPreset? RoundedDownPreset { get; set; }

    [JsonProperty("4I:")]
    public NmsDifficultyPreset? EasiestUsedPreset { get; set; }

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
