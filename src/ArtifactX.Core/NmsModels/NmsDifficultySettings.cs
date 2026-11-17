using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /vLc/6f=/LyC
/// Unlike this file's siblings, this mapping is NOT community-documented - it's
/// inferred from libMBIN's GcDifficultySettingsReplicatedState (four
/// GcDifficultyPresetType fields in Index order: IsPermadeath(0), Preset(1),
/// RoundedDownPreset(2), EasiestUsedPreset(3), HardestUsedPreset(4), IsLocked(5)).
/// CONFIRMED 2026-08-06 via a controlled in-game test (see
/// NmsPlayerStateData.EasiestUsedPresetPath/HardestUsedPresetPath for the
/// full writeup): brJ/qAf/4I: are 3 distinct tracked values - brJ is the
/// current preset, qAf is the EASIEST standard preset ever used, 4I: is the
/// HARDEST standard preset ever used. An earlier version of this mapping
/// had qAf/4I: swapped (guessed from libMBIN's declared field INDEX order
/// with no value-based check) - that guess looked self-consistent against
/// every single-sample save checked before the controlled test, since both
/// labelings preserve the same relative severity ordering; only a save
/// where Current genuinely diverged from both tracked extremes could force
/// the correct read. IsPermadeath/RoundedDownPreset/IsLocked have never
/// been observed as separate fields in ANY sample (Normal/Survival/
/// Permadeath/Custom, nor the controlled Relaxed/Survival/Creative test) -
/// RoundedDownPreset in particular may simply not be persisted to the save
/// at all (a client-side-only computed value), rather than being
/// compacted/omitted the way this doc comment previously assumed.
/// Keys:
///   brJ -> Current preset (index 1, GcDifficultyPresetType nested via "7ND")
///   qAf -> Easiest used preset - the easiest standard preset this save has
///          ever actually been set to
///   4I: -> Hardest used preset - the hardest standard preset this save has
///          ever actually been set to
/// </summary>
public class NmsDifficultySettings
{
    [JsonProperty("brJ")]
    public NmsDifficultyPreset? Preset { get; set; }

    [JsonProperty("qAf")]
    public NmsDifficultyPreset? EasiestUsedPreset { get; set; }

    [JsonProperty("4I:")]
    public NmsDifficultyPreset? HardestUsedPreset { get; set; }

    [JsonIgnore]
    public string GameMode => Preset?.DisplayName ?? "Unknown";

    /// <summary>
    /// The save's stack-limit setting ("High", "Normal", "Low"): LyC/:fe is the active GcDifficultySettingsData, its
    /// fields in libMBIN Index order (8 flags, then DeathConsequences n7p "ItemGrave", ... ActiveSurvivalBars tEx "All",
    /// ..., InventoryStackLimits kZ5 at index 15, ... CreatureHostility BbG "FullEcosystem", ... Fishing B;B
    /// "NormalCatchWindow", NPCPopulation Z52 "Full"), each option wrapping its enum (kZ5.?SS). Checked 2026-11-12 on
    /// the owner's Slot 1 (Normal preset): kZ5.?SS = "High", and every stored slot MaxAmount fits the High sizes.
    /// </summary>
    public static readonly string[] InventoryStackLimitsPath = ["vLc", "6f=", "LyC", ":fe", "kZ5", "?SS"];
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
