using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Persists as a flat key/value JSON file under LocalAppDataPaths rather than
/// Windows.Storage.ApplicationData.Current.LocalSettings (which threw
/// InvalidOperationException when this app moved to running unpackaged - see
/// LocalAppDataPaths). Each value is itself a JSON-serialized blob, same
/// nested-JSON-string shape LocalSettings.Values held before, to keep this
/// change to a storage-backend swap rather than a data-shape rewrite.
/// </summary>
public static class SaveFolderSettingsService
{
    private const string CustomFoldersKey = "CustomSaveFolders";
    private const string ExpandedFoldersKey = "ExpandedSaveFolders";
    private const string FolderNamesKey = "SaveFolderCustomNames";

    private static readonly string SettingsFilePath = Path.Combine(LocalAppDataPaths.RootFolder, "settings.json");

    private static Dictionary<string, string>? _cache;

    private static Dictionary<string, string> LoadSettings()
    {
        if (_cache != null) return _cache;

        if (File.Exists(SettingsFilePath))
        {
            try
            {
                string fileJson = File.ReadAllText(SettingsFilePath);
                _cache = JsonSerializer.Deserialize(fileJson, SaveFolderJsonContext.Default.DictionaryStringString)
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

    private static bool TryGetSetting(string key, out string value)
    {
        return LoadSettings().TryGetValue(key, out value!);
    }

    private static void SetSetting(string key, string value)
    {
        var settings = LoadSettings();
        settings[key] = value;
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, SaveFolderJsonContext.Default.DictionaryStringString));
    }

    /// <summary>Which folder cards the user left expanded, so re-launching the
    /// app doesn't dump them back into a fully-collapsed list. Null (as opposed
    /// to an empty list) means "never saved" - first launch ever - which the
    /// caller treats differently from "user explicitly collapsed everything".</summary>
    public static IReadOnlyList<string>? GetExpandedFolders()
    {
        if (TryGetSetting(ExpandedFoldersKey, out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return JsonSerializer.Deserialize(json, SaveFolderJsonContext.Default.ListString);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return null;
    }

    public static void SetExpandedFolders(IEnumerable<string> paths)
    {
        string json = JsonSerializer.Serialize(paths.ToList(), SaveFolderJsonContext.Default.ListString);
        SetSetting(ExpandedFoldersKey, json);
    }

    public static IReadOnlyList<string> GetCustomFolders()
    {
        if (TryGetSetting(CustomFoldersKey, out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return JsonSerializer.Deserialize(json, SaveFolderJsonContext.Default.ListString) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return [];
    }

    public static void SetCustomFolders(IEnumerable<string> paths)
    {
        string json = JsonSerializer.Serialize(paths.ToList(), SaveFolderJsonContext.Default.ListString);
        SetSetting(CustomFoldersKey, json);
    }

    public static void AddCustomFolder(string folderPath)
    {
        var existing = GetCustomFolders().ToList();
        if (existing.Any(p => string.Equals(p, folderPath, System.StringComparison.OrdinalIgnoreCase)))
            return;

        existing.Add(folderPath);
        SetCustomFolders(existing);
    }

    public static void RemoveCustomFolder(string folderPath)
    {
        var remaining = GetCustomFolders()
            .Where(p => !string.Equals(p, folderPath, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        SetCustomFolders(remaining);
        SetCustomFolderName(folderPath, null);
    }

    /// <summary>User-given label for a folder, keyed by path - pure display
    /// metadata, never touches the actual folder on disk. Null means no custom
    /// name has been set, so the caller falls back to its default label.</summary>
    public static string? GetCustomFolderName(string folderPath)
    {
        var names = GetCustomFolderNames();
        return names.TryGetValue(folderPath, out var name) ? name : null;
    }

    public static void SetCustomFolderName(string folderPath, string? name)
    {
        var names = GetCustomFolderNames();

        if (string.IsNullOrWhiteSpace(name))
            names.Remove(folderPath);
        else
            names[folderPath] = name.Trim();

        string json = JsonSerializer.Serialize(names, SaveFolderJsonContext.Default.DictionaryStringString);
        SetSetting(FolderNamesKey, json);
    }

    private static Dictionary<string, string> GetCustomFolderNames()
    {
        if (TryGetSetting(FolderNamesKey, out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize(json, SaveFolderJsonContext.Default.DictionaryStringString);
                if (deserialized is not null)
                    return new Dictionary<string, string>(deserialized, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                // fall through to empty
            }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class SaveFolderJsonContext : JsonSerializerContext { }
