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
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class AppSettingsJsonContext : JsonSerializerContext { }
