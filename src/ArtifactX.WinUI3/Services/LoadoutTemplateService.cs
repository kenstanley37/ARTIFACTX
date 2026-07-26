using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Persists loadout templates as individual JSON files under
/// ApplicationData.Current.LocalFolder\Loadouts\{id}.json - real disk storage
/// via Windows.Storage, not the small per-value settings store already used
/// elsewhere (SaveFolderSettingsService) for simple config. A single loadout
/// can hold up to 60 tech items, which comfortably fits a file but would be
/// tight in that settings store once more than a couple templates existed.
/// </summary>
public static class LoadoutTemplateService
{
    private const string FolderName = "Loadouts";

    public static async Task SaveAsync(NmsLoadoutTemplate template)
    {
        StorageFolder folder = await ApplicationData.Current.LocalFolder
            .CreateFolderAsync(FolderName, CreationCollisionOption.OpenIfExists);

        string json = JsonSerializer.Serialize(template, LoadoutJsonContext.Default.NmsLoadoutTemplate);

        StorageFile file = await folder.CreateFileAsync($"{template.Id}.json", CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, json);
    }

    public static async Task<List<NmsLoadoutTemplate>> LoadAllAsync()
    {
        var results = new List<NmsLoadoutTemplate>();

        StorageFolder folder;
        try
        {
            folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(FolderName);
        }
        catch (FileNotFoundException)
        {
            return results; // no templates saved yet - not an error
        }

        foreach (var file in await folder.GetFilesAsync())
        {
            if (!file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                string json = await FileIO.ReadTextAsync(file);
                var template = JsonSerializer.Deserialize(json, LoadoutJsonContext.Default.NmsLoadoutTemplate);
                if (template is not null) results.Add(template);
            }
            catch
            {
                // Skip a corrupt/unreadable template file rather than failing the whole list.
            }
        }

        return results.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public static async Task DeleteAsync(string id)
    {
        try
        {
            StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(FolderName);
            StorageFile file = await folder.GetFileAsync($"{id}.json");
            await file.DeleteAsync();
        }
        catch (FileNotFoundException)
        {
            // Already gone - nothing to do.
        }
    }
}

[JsonSerializable(typeof(NmsLoadoutTemplate))]
internal partial class LoadoutJsonContext : JsonSerializerContext { }