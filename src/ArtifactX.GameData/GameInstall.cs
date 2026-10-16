using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ArtifactX.GameData;

/// <summary>
/// A No Man's Sky install on this PC, and which build of the game it is.
///
/// The build is read from the install itself - never typed into ArtifactX
/// (the old hand-written "NMS: SWARM" label went a whole major update out of
/// date). <see cref="Fingerprint"/> changes whenever the game's data does:
/// NMS.exe's version plus every PAK file's name and size. The catalog records
/// the fingerprint it was built from; a different one means the game has
/// updated since (GameDataInfo).
/// </summary>
public sealed record GameInstall(string RootFolder, string PcbanksPath, string? ExeVersion, string? SteamBuildId, string Fingerprint)
{
    public const string SteamAppId = "275850";

    /// <summary>Accepts the game folder, its GAMEDATA folder or PCBANKS itself.
    /// Null when it isn't a No Man's Sky install.</summary>
    public static GameInstall? Inspect(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        folder = folder.Trim().Trim('"');
        string? pcbanks = new[]
        {
            Path.Combine(folder, "GAMEDATA", "PCBANKS"),
            Path.Combine(folder, "PCBANKS"),
            folder,
        }.FirstOrDefault(p => File.Exists(Path.Combine(p, "NMSARC.globals.pak")));
        if (pcbanks is null) return null;

        string root = Path.GetFullPath(Path.Combine(pcbanks, "..", ".."));
        string exe = Path.Combine(root, "Binaries", "NMS.exe");
        string? exeVersion = null;
        try
        {
            if (File.Exists(exe)) exeVersion = FileVersionInfo.GetVersionInfo(exe).FileVersion;
        }
        catch (IOException) { }

        return new GameInstall(root, pcbanks, exeVersion, SteamBuildIdFor(root), FingerprintOf(pcbanks, exeVersion));
    }

    /// <summary>The first install found on this PC (Steam, GOG, Xbox app), or null.</summary>
    public static GameInstall? Find() => CandidateFolders().Select(Inspect).FirstOrDefault(g => g is not null);

    /// <summary>"build 180836" - the game's own build number, for showing.</summary>
    public string BuildLabel => ExeVersion is { Length: > 0 } v ? $"build {v}" : SteamBuildId is { Length: > 0 } s ? $"Steam build {s}" : "unknown build";

    private static string FingerprintOf(string pcbanks, string? exeVersion)
    {
        var text = new StringBuilder(exeVersion ?? "");
        foreach (var pak in Directory.GetFiles(pcbanks, "*.pak").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            text.Append('|').Append(Path.GetFileName(pak).ToUpperInvariant()).Append(':').Append(new FileInfo(pak).Length);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16];
    }

    // ---------------------------------------------------------------- finding it

    private static IEnumerable<string> CandidateFolders()
    {
        foreach (var library in SteamLibraries())
            yield return Path.Combine(library, "steamapps", "common", "No Man's Sky");
        foreach (var gog in GogFolders())
            yield return gog;
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            yield return Path.Combine(drive.RootDirectory.FullName, "XboxGames", "No Man's Sky", "Content");
            yield return Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "Steam", "steamapps", "common", "No Man's Sky");
            yield return Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "No Man's Sky");
            yield return Path.Combine(drive.RootDirectory.FullName, "GOG Games", "No Man's Sky");
        }
    }

    /// <summary>Steam's library folders (Steam's own path from the registry,
    /// then every "path" in its libraryfolders.vdf).</summary>
    private static IEnumerable<string> SteamLibraries()
    {
        var found = new List<string>();
        try
        {
            if (OperatingSystem.IsWindows()
                && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string steam)
            {
                steam = steam.Replace('/', '\\');
                found.Add(steam);
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        found.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return found.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>GOG Galaxy records each installed game's folder in the registry.</summary>
    private static IEnumerable<string> GogFolders()
    {
        var found = new List<string>();
        if (!OperatingSystem.IsWindows()) return found;
        try
        {
            using var games = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
            if (games is null) return found;
            foreach (var id in games.GetSubKeyNames())
            {
                using var game = games.OpenSubKey(id);
                if ((game?.GetValue("gameName") as string)?.Contains("No Man", StringComparison.OrdinalIgnoreCase) == true
                    && game.GetValue("path") is string path)
                    found.Add(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return found;
    }

    /// <summary>Steam's build id for the game, from the library's app
    /// manifest (steamapps/appmanifest_275850.acf), when it's a Steam install.</summary>
    private static string? SteamBuildIdFor(string root)
    {
        try
        {
            var steamapps = Directory.GetParent(root)?.Parent; // ...\steamapps\common\No Man's Sky
            string manifest = steamapps is null ? "" : Path.Combine(steamapps.FullName, $"appmanifest_{SteamAppId}.acf");
            if (!File.Exists(manifest)) return null;
            var m = Regex.Match(File.ReadAllText(manifest), "\"buildid\"\\s+\"(\\d+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch (IOException) { return null; }
    }
}
