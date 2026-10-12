using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Where every backup goes - game save backups (BackupService) and copies of
/// the player's own data (UserDatabase.Backup). Default:
/// %LOCALAPPDATA%\ArtifactX\Backups. The player can point it at another folder,
/// e.g. inside OneDrive, to keep copies off this PC (Settings page).
///
/// Kept in backup-folder.txt next to user.db rather than inside user.db: the
/// way to find user.db's backups must not depend on user.db itself being
/// readable, and user.db's own upgrade step makes a backup before anything
/// else in it can be read. Only backups move - the live user.db stays in
/// %LOCALAPPDATA%, since sync tools can corrupt a database that's open.
/// </summary>
public static class BackupLocation
{
    private static string ConfigFile => Path.Combine(LocalAppDataPaths.RootFolder, "backup-folder.txt");

    public static string DefaultRoot => Path.Combine(LocalAppDataPaths.RootFolder, "Backups");

    private static string? _root;

    /// <summary>The backups folder in use (created if missing).</summary>
    public static string Root
    {
        get
        {
            if (_root is null)
            {
                string? chosen = null;
                try
                {
                    if (File.Exists(ConfigFile)) chosen = File.ReadAllText(ConfigFile).Trim();
                }
                catch (IOException) { }
                _root = chosen is { Length: > 0 } ? chosen : DefaultRoot;
            }
            try { Directory.CreateDirectory(_root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A chosen folder that's gone (unplugged drive, OneDrive signed out):
                // fall back to the default so backups are never silently skipped.
                Debug.WriteLine($"[BackupLocation] {_root} unavailable ({ex.Message}); using the default.");
                _root = DefaultRoot;
                Directory.CreateDirectory(_root);
            }
            return _root;
        }
    }

    public static bool IsDefault => string.Equals(Path.GetFullPath(Root), Path.GetFullPath(DefaultRoot), StringComparison.OrdinalIgnoreCase);

    /// <summary>A subfolder of the backups folder (created if missing).</summary>
    public static string Sub(string name)
    {
        string path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Switches to a new backups folder (null = the default), moving
    /// the existing backups there when asked. Returns an error, or null.</summary>
    public static async Task<string?> ChangeAsync(string? newRoot, bool moveExisting)
    {
        string target = Path.GetFullPath(newRoot is { Length: > 0 } ? newRoot : DefaultRoot);
        string current = Path.GetFullPath(Root);
        if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase)) return null;
        if (target.StartsWith(current + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return "Pick a folder outside the current backups folder.";

        try
        {
            Directory.CreateDirectory(target);
            if (moveExisting) await Task.Run(() => MoveContents(current, target));
            if (string.Equals(target, Path.GetFullPath(DefaultRoot), StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(ConfigFile)) File.Delete(ConfigFile);
            }
            else
            {
                File.WriteAllText(ConfigFile, target);
            }
            _root = target;
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Couldn't use that folder: {ex.Message}";
        }
    }

    /// <summary>Moves every backup folder across; a copy-then-delete when the
    /// target is on another drive. Anything already at the target is kept.</summary>
    private static void MoveContents(string from, string to)
    {
        if (!Directory.Exists(from)) return;
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string dest = Path.Combine(to, Path.GetRelativePath(from, file));
            if (File.Exists(dest)) continue;
            File.Copy(file, dest);
            File.Delete(file);
        }
        // Then the emptied folders, deepest first.
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            if (Directory.GetFileSystemEntries(dir).Length == 0)
                try { Directory.Delete(dir); } catch (IOException) { }
    }
}
