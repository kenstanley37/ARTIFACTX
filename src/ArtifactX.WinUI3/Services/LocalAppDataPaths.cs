using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The app's own data folder: %AppData%\ArtifactX (Roaming, next to the game's
/// own HelloGames folder). Plain Win32 paths, not Windows.Storage.ApplicationData,
/// which needs package identity and throws in this unpackaged app.
///
/// It was %LocalAppData%\ArtifactX until 2026-10-15. That's where the Velopack
/// installer puts the app itself, and it manages that whole folder (an
/// uninstall removes it) - the first Setup.exe even reported "already
/// installed" because the data was there. <see cref="MoveFromOldFolder"/>
/// carries existing data across once, at startup.
/// </summary>
public static class LocalAppDataPaths
{
    private static readonly Lazy<string> RootLazy = new(() =>
    {
        Directory.CreateDirectory(NewRoot);
        return NewRoot;
    });

    private static string NewRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArtifactX");
    private static string OldRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtifactX");

    public static string RootFolder => RootLazy.Value;

    /// <summary>The unpacked-save cache. It sat next to the exe until
    /// 2026-10-15 - not writable when installed for all users (Program Files),
    /// and replaced on every update anyway.</summary>
    public static string WorkingFolder => Path.Combine(RootFolder, "Working");

    /// <summary>Returns (and ensures the existence of) a named subfolder under
    /// the app's data root, e.g. "Backups".</summary>
    public static string GetSubfolder(string name)
    {
        string path = Path.Combine(RootFolder, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>The data ArtifactX keeps (never the installer's own files, which
    /// share the old folder once installed: current\, Update.exe, packages\).</summary>
    private static readonly string[] OwnFiles = { "user.db", "user.db-wal", "user.db-shm", "backup-folder.txt" };
    private static readonly string[] OwnFolders = { "GameData", "Backups", "logs", "DevFakeAccounts" };

    /// <summary>Moves data from %LocalAppData%\ArtifactX to the new folder,
    /// once, before anything opens it (App constructor). Only when the new
    /// folder has no user.db yet, so it never overwrites. The old folder is
    /// removed if nothing else is left in it.</summary>
    public static void MoveFromOldFolder()
    {
        try
        {
            if (!Directory.Exists(OldRoot) || File.Exists(Path.Combine(NewRoot, "user.db"))) return;
            Directory.CreateDirectory(NewRoot);
            foreach (string name in OwnFiles)
            {
                string from = Path.Combine(OldRoot, name), to = Path.Combine(NewRoot, name);
                if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
            }
            foreach (string name in OwnFolders)
            {
                string from = Path.Combine(OldRoot, name), to = Path.Combine(NewRoot, name);
                if (Directory.Exists(from) && !Directory.Exists(to)) Directory.Move(from, to);
            }
            // Leftovers from the one-time import of the old settings files.
            foreach (string file in Directory.GetFiles(OldRoot, "*.imported"))
                File.Move(file, Path.Combine(NewRoot, Path.GetFileName(file)), overwrite: true);
            foreach (string dir in Directory.GetDirectories(OldRoot, "*.imported"))
            {
                string to = Path.Combine(NewRoot, Path.GetFileName(dir));
                if (!Directory.Exists(to)) Directory.Move(dir, to);
            }

            // A backup folder that was the old default now points at the new one.
            string config = Path.Combine(NewRoot, "backup-folder.txt");
            if (File.Exists(config) && Path.GetFullPath(File.ReadAllText(config).Trim()).TrimEnd('\\')
                    .Equals(Path.Combine(OldRoot, "Backups"), StringComparison.OrdinalIgnoreCase))
                File.Delete(config);

            if (!Directory.EnumerateFileSystemEntries(OldRoot).Any()) Directory.Delete(OldRoot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocalAppDataPaths] Couldn't move data from {OldRoot}: {ex.Message}");
        }
    }
}
