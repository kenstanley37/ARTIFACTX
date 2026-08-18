using ArtifactX.Tools.DataCataloger.Models;
using System.Text.Json;

namespace ArtifactX.Tools.DataCataloger.Services;

public static class SettingsService
{
    private static readonly string ConfigPath = "config.json";

    public static AppSettings Load()
    {
        if (!File.Exists(ConfigPath)) return new AppSettings();
        string json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static bool IsValid(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.NmsInstallationPath)) return false;

        // Check if the folder exists AND if at least one core file is present
        string corePakPath = Path.Combine(settings.NmsInstallationPath, "NMSARC.globals.pak");
        return Directory.Exists(settings.NmsInstallationPath) && File.Exists(corePakPath);
    }
}