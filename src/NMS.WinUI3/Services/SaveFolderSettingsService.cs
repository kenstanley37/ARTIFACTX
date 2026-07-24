using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;

namespace NMS.WinUI3.Services;

public static class SaveFolderSettingsService
{
    private const string SelectedPathKey = "SaveFolderPath";
    private const string CustomFoldersKey = "CustomSaveFolders";

    public static string? GetSelectedFolder() =>
        ApplicationData.Current.LocalSettings.Values.TryGetValue(SelectedPathKey, out var value)
            ? value as string
            : null;

    public static void SetSelectedFolder(string folderPath) =>
        ApplicationData.Current.LocalSettings.Values[SelectedPathKey] = folderPath;

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
    }
}

[JsonSerializable(typeof(List<string>))]
internal partial class SaveFolderJsonContext : JsonSerializerContext { }