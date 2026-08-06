using Newtonsoft.Json;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// JSON Path: /vLc/6f=
/// Community-documented ArtifactX saves call this block PlayerStateData. It holds far
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
///   LyC -> Difficulty/game mode settings (see NmsDifficultySettings) - inferred, not community-documented
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

    [JsonProperty("LyC")]
    public NmsDifficultySettings? DifficultySettings { get; set; }

    [JsonIgnore]
    public string GameMode => DifficultySettings?.GameMode ?? "Unknown";

    // Path constants for the live-edit engine. Full parent chain included, so
    // a page can reference NmsPlayerStateData.UnitsPath directly with no need
    // to know vLc/6f= exist as separate steps.
    public static readonly string[] LocationDescriptionPath = { "vLc", "6f=", "n:R" };
    public static readonly string[] UnitsPath = { "vLc", "6f=", "wGS" };
    public static readonly string[] NanitesPath = { "vLc", "6f=", "7QL" };
    public static readonly string[] QuicksilverPath = { "vLc", "6f=", "kN;" };
    public static readonly string[] GameModePath = { "vLc", "6f=", "LyC", "brJ", "7ND" };

    /// <summary>"Rounded down" version of the current preset - when the
    /// current preset is Custom, this is the closest standard preset the
    /// game itself considers the custom settings equivalent to (see
    /// NmsDifficultySettings). Confirmed distinct from GameModePath by real
    /// data: a Custom-preset save read Preset=Custom, RoundedDownPreset=
    /// Creative, EasiestUsedPreset=Normal - three genuinely different
    /// values on the same save (2026-08-06).</summary>
    public static readonly string[] RoundedDownPresetPath = { "vLc", "6f=", "LyC", "qAf", "7ND" };

    /// <summary>The easiest standard preset this save has ever actually been
    /// set to - confirmed distinct from both GameModePath and
    /// RoundedDownPresetPath (see that path's doc comment for the real-data
    /// evidence). There's a symmetric HardestUsedPreset field per libMBIN's
    /// GcDifficultySettingsReplicatedState, but its own JSON key has never
    /// been observed populated in any sample checked (including
    /// Custom/Survival/Permadeath saves, not just Normal ones) - the save
    /// format compacts/omits it whenever unused, so it's unmapped for now.
    /// GeneralPage estimates a "hardest used" display from
    /// RoundedDownPresetPath/EasiestUsedPresetPath instead (per 2026-08-06
    /// user decision) rather than leaving it blank.</summary>
    public static readonly string[] EasiestUsedPresetPath = { "vLc", "6f=", "LyC", "4I:", "7ND" };

    /// <summary>Units/Nanites/Quicksilver are stored by the game itself as a
    /// signed 32-bit field that wraps to negative once the real balance
    /// exceeds int32.MaxValue (~2.1 billion) - confirmed 2026-08-01 directly
    /// in a real save with hundreds of hours played: Units read literally
    /// -84998516 in the save's own JSON (this is what the game itself wrote,
    /// not an ArtifactX parsing issue), yet the in-game HUD showed a huge,
    /// correct-looking positive balance. Reapplying 2^32 to the negative raw
    /// value lines up with the HUD's actual numbers, so the game's own
    /// display code must be reinterpreting the same 32-bit pattern as
    /// UNSIGNED when rendering it. ToDisplayValue undoes that wrap for
    /// ArtifactX's own UI; ToRawValue re-applies it when staging an edit so
    /// the written value keeps the exact bit pattern the game already
    /// tolerates (unverified whether writing the plain positive number
    /// instead would also work - not worth the risk of finding out the hard
    /// way on someone's real save when reproducing the game's own existing
    /// representation is guaranteed safe). Both are no-ops for every balance
    /// under ~2.1 billion, which is nearly every save.</summary>
    public static long ToDisplayValue(long raw) => raw < 0 ? raw + 4294967296L : raw;
    public static long ToRawValue(long display) => display > int.MaxValue ? display - 4294967296L : display;
}