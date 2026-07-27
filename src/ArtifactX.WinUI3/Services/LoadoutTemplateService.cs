using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Persists loadout templates as individual JSON files under
/// LocalAppDataPaths.GetSubfolder("Loadouts") - plain System.IO rather than
/// Windows.Storage.ApplicationData.Current.LocalFolder, which threw
/// InvalidOperationException when this app moved to running unpackaged (see
/// LocalAppDataPaths). A single loadout can hold up to 60 tech items, which
/// comfortably fits a file but would be tight in a single flat settings blob.
/// </summary>
public static class LoadoutTemplateService
{
    private static string FolderPath => LocalAppDataPaths.GetSubfolder("Loadouts");

    public static async Task SaveAsync(NmsLoadoutTemplate template)
    {
        string json = JsonSerializer.Serialize(template, LoadoutJsonContext.Default.NmsLoadoutTemplate);
        string path = Path.Combine(FolderPath, $"{template.Id}.json");
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<List<NmsLoadoutTemplate>> LoadAllAsync()
    {
        var results = new List<NmsLoadoutTemplate>();

        foreach (var path in Directory.EnumerateFiles(FolderPath, "*.json"))
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);
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

    public static Task DeleteAsync(string id)
    {
        string path = Path.Combine(FolderPath, $"{id}.json");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

[JsonSerializable(typeof(NmsLoadoutTemplate))]
internal partial class LoadoutJsonContext : JsonSerializerContext { }
