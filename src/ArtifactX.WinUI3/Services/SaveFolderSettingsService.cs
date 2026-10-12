using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Save Selection's per-PC choices - folders the player browsed to, which
/// cards were left open, names given to browsed folders - in user.db's
/// device_settings (SettingsStore, Device scope): they're paths on this PC,
/// so restoring a backup on another PC keeps that PC's own. A Steam/GOG
/// account's nickname isn't a path setting: it lives on the game account
/// (GameAccountsService), so it follows the account. These were settings.json
/// until 2026-10-12. Each value is a JSON blob, the shape they always had.
/// </summary>
public static class SaveFolderSettingsService
{
    private const string CustomFoldersKey = "CustomSaveFolders";
    private const string ExpandedFoldersKey = "ExpandedSaveFolders";
    private const string FolderNamesKey = "SaveFolderCustomNames";

    private static bool TryGetSetting(string key, out string value)
    {
        string? stored = SettingsStore.Get(SettingsStore.Scope.Device, key);
        value = stored ?? "";
        return stored is not null;
    }

    private static void SetSetting(string key, string value) => SettingsStore.Set(SettingsStore.Scope.Device, key, value);

    private static bool IsGameAccount(string folderPath) =>
        !GameAccountsService.KeyFor(folderPath).StartsWith("folder:", StringComparison.Ordinal);

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
        if (IsGameAccount(folderPath)) return GameAccountsService.Nickname(folderPath);
        var names = GetCustomFolderNames();
        return names.TryGetValue(folderPath, out var name) ? name : null;
    }

    public static void SetCustomFolderName(string folderPath, string? name)
    {
        if (IsGameAccount(folderPath))
        {
            GameAccountsService.SetNickname(folderPath, name);
            return;
        }
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
