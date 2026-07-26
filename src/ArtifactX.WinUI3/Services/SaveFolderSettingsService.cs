using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;

namespace ArtifactX.WinUI3.Services;

public static class SaveFolderSettingsService
{
    private const string CustomFoldersKey = "CustomSaveFolders";
    private const string ExpandedFoldersKey = "ExpandedSaveFolders";
    private const string FolderNamesKey = "SaveFolderCustomNames";

    /// <summary>Which folder cards the user left expanded, so re-launching the
    /// app doesn't dump them back into a fully-collapsed list. Null (as opposed
    /// to an empty list) means "never saved" - first launch ever - which the
    /// caller treats differently from "user explicitly collapsed everything".</summary>
    public static IReadOnlyList<string>? GetExpandedFolders()
    {
        if (ApplicationData.Current.LocalSettings.Values.TryGetValue(ExpandedFoldersKey, out var raw) &&
            raw is string json && !string.IsNullOrWhiteSpace(json))
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
        ApplicationData.Current.LocalSettings.Values[ExpandedFoldersKey] = json;
    }

    public static IReadOnlyList<string> GetCustomFolders()
    {
        if (ApplicationData.Current.LocalSettings.Values.TryGetValue(CustomFoldersKey, out var raw) &&
            raw is string json && !string.IsNullOrWhiteSpace(json))
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
        ApplicationData.Current.LocalSettings.Values[CustomFoldersKey] = json;
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
        ApplicationData.Current.LocalSettings.Values[FolderNamesKey] = json;
    }

    private static Dictionary<string, string> GetCustomFolderNames()
    {
        if (ApplicationData.Current.LocalSettings.Values.TryGetValue(FolderNamesKey, out var raw) &&
            raw is string json && !string.IsNullOrWhiteSpace(json))
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