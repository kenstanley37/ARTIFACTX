using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// General app-level choices that aren't specific to save-folder management
/// (see SaveFolderSettingsService for that). Stored per profile in user.db
/// (SettingsStore, Profile scope), so they come along in a backup; they were
/// app-settings.json until 2026-10-12 (LegacyDataImporter moved them in).
/// </summary>
public static class AppSettingsService
{
    private const string DisclaimerAcceptedKey = "DisclaimerAccepted";
    private const string ThemeIdKey = "Theme";
    private const string HyperdriveRangeKey = "HyperdriveRange";
    private const string GlyphViewSizeKey = "GlyphViewSize";

    private static bool TryGet(string key, out string value)
    {
        string? stored = SettingsStore.Get(SettingsStore.Scope.Profile, key);
        value = stored ?? "";
        return stored is not null;
    }

    private static void SetSetting(string key, string value) => SettingsStore.Set(SettingsStore.Scope.Profile, key, value);

    /// <summary>Whether the user has clicked through the first-launch
    /// disclaimer dialog - checked once at startup (see MainWindow) to
    /// decide whether to show it again.</summary>
    public static bool HasAcceptedDisclaimer =>
        TryGet(DisclaimerAcceptedKey, out var value) && value == "true";

    public static void SetDisclaimerAccepted() => SetSetting(DisclaimerAcceptedKey, "true");

    /// <summary>Id of the user's chosen visual theme (see ThemeService), or
    /// null if they've never picked one.</summary>
    public static string? ThemeId =>
        TryGet(ThemeIdKey, out var value) ? value : null;

    public static void SetThemeId(string themeId) => SetSetting(ThemeIdKey, themeId);

    /// <summary>The player's hyperdrive range in light years for jump estimates
    /// (see JumpPlanner), or null if never set.</summary>
    public static double? HyperdriveRange =>
        TryGet(HyperdriveRangeKey, out var value)
        && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double range)
        && range >= 1 ? range : null;

    public static void SetHyperdriveRange(double? range) =>
        SetSetting(HyperdriveRangeKey, range is double r ? r.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");

    /// <summary>Glyph size (px) the player last picked in the large glyph view
    /// (GlyphViewer); 96 until they choose.</summary>
    public static int GlyphViewSize =>
        TryGet(GlyphViewSizeKey, out var value) && int.TryParse(value, out int size) ? size : 96;

    public static void SetGlyphViewSize(int size) => SetSetting(GlyphViewSizeKey, size.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Simple on/off flags for one-time UI hints (e.g. a dismissed tip).</summary>
    public static bool GetFlag(string key) => TryGet(key, out var value) && value == "true";

    public static void SetFlag(string key, bool value) => SetSetting(key, value ? "true" : "false");
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class AppSettingsJsonContext : JsonSerializerContext { }
