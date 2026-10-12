using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// General app-level flags that aren't specific to save-folder management
/// (see SaveFolderSettingsService for that) - its own file so the two
/// concerns don't get tangled together. Same flat key/value JSON-under-
/// LocalAppDataPaths pattern.
/// </summary>
public static class AppSettingsService
{
    private const string DisclaimerAcceptedKey = "DisclaimerAccepted";
    private const string ThemeIdKey = "Theme";
    private const string HyperdriveRangeKey = "HyperdriveRange";

    private static readonly string SettingsFilePath = Path.Combine(LocalAppDataPaths.RootFolder, "app-settings.json");

    private static Dictionary<string, string>? _cache;

    private static Dictionary<string, string> LoadSettings()
    {
        if (_cache != null) return _cache;

        if (File.Exists(SettingsFilePath))
        {
            try
            {
                string fileJson = File.ReadAllText(SettingsFilePath);
                _cache = JsonSerializer.Deserialize(fileJson, AppSettingsJsonContext.Default.DictionaryStringString)
                    ?? new Dictionary<string, string>();
                return _cache;
            }
            catch (JsonException)
            {
                // fall through to a fresh, empty settings store
            }
        }

        _cache = new Dictionary<string, string>();
        return _cache;
    }

    private static void SetSetting(string key, string value)
    {
        var settings = LoadSettings();
        settings[key] = value;
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.DictionaryStringString));
    }

    /// <summary>Whether the user has clicked through the first-launch
    /// disclaimer dialog - checked once at startup (see MainWindow) to
    /// decide whether to show it again.</summary>
    public static bool HasAcceptedDisclaimer =>
        LoadSettings().TryGetValue(DisclaimerAcceptedKey, out var value) && value == "true";

    public static void SetDisclaimerAccepted() => SetSetting(DisclaimerAcceptedKey, "true");

    /// <summary>Id of the user's chosen visual theme (see ThemeService), or
    /// null if they've never picked one.</summary>
    public static string? ThemeId =>
        LoadSettings().TryGetValue(ThemeIdKey, out var value) ? value : null;

    public static void SetThemeId(string themeId) => SetSetting(ThemeIdKey, themeId);

    /// <summary>The player's hyperdrive range in light years for jump estimates
    /// (see JumpPlanner), or null if never set.</summary>
    public static double? HyperdriveRange =>
        LoadSettings().TryGetValue(HyperdriveRangeKey, out var value)
        && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double range)
        && range >= 1 ? range : null;

    public static void SetHyperdriveRange(double? range) =>
        SetSetting(HyperdriveRangeKey, range is double r ? r.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");

    /// <summary>Simple on/off flags for one-time UI hints (e.g. a dismissed tip).</summary>
    public static bool GetFlag(string key) => LoadSettings().TryGetValue(key, out var value) && value == "true";

    public static void SetFlag(string key, bool value) => SetSetting(key, value ? "true" : "false");
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class AppSettingsJsonContext : JsonSerializerContext { }
