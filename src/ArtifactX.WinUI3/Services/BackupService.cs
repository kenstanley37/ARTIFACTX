using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>One past commit's backed-up files, all sharing the same timestamp
/// since a single commit can write more than one real file at once (a save
/// slot's rotating save.hg/save2.hg pair both get written - and therefore
/// both backed up - together).</summary>
public sealed record BackupRestorePoint(string FolderPath, DateTime Timestamp, IReadOnlyList<string> FileNames);

/// <summary>
/// Timestamped, per-commit backups of whatever's currently on disk, taken
/// right before SaveSessionManager/AccountSessionManager overwrite it -
/// added 2026-08-05 after the user pointed out neither had this at all for
/// per-slot saves (AccountSessionManager had a single one-time backup, since
/// replaced by this). Lives entirely under LocalAppDataPaths, never alongside
/// the real save files - keeps the user's actual NMS save folder untouched
/// as backups accumulate, and there's no risk of the game itself mistaking a
/// backup for a real save (NMS only looks for save*.hg by name).
///
/// Deliberately keeps every backup indefinitely - no automatic pruning. A
/// save file is small (a few MB at most), so even a long play history's
/// worth of backups is a trivial amount of disk space, and silently
/// discarding an old backup risks removing the exact one a user needed.
/// </summary>
public static class BackupService
{
    private const string RootSubfolder = "Backups";
    private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

    public static string BuildSlotGroupKey(string accountFolder, int slotId) =>
        Path.Combine(AccountKey(accountFolder), $"Slot{slotId}");

    public static string BuildAccountDataGroupKey(string accountFolder) =>
        Path.Combine(AccountKey(accountFolder), "AccountData");

    /// <summary>The backup folder name for a game account's save folder:
    /// "Steam_76561198090992118" / "GOG_12345678" from st_/gog_ folder names,
    /// else "Folder_" plus a hash of the path (Xbox, browsed folders).
    /// Keyed by account, never by the card's display name: two Steam accounts
    /// both shown as "Steam" used to share one backup folder, so restoring a
    /// slot could offer the other account's saves (fixed 2026-10-12).</summary>
    public static string AccountKey(string accountFolder)
    {
        string leaf = Path.GetFileName(accountFolder.TrimEnd('\\', '/'));
        if (leaf.StartsWith("st_", StringComparison.OrdinalIgnoreCase) && leaf.Length > 3) return SanitizeForPath("Steam_" + leaf[3..]);
        if (leaf.StartsWith("gog_", StringComparison.OrdinalIgnoreCase) && leaf.Length > 4) return SanitizeForPath("GOG_" + leaf[4..]);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(accountFolder.ToUpperInvariant()));
        return "Folder_" + Convert.ToHexString(hash)[..8];
    }

    /// <summary>Moves backups made before AccountKey (Backups\Steam,
    /// Backups\Custom Folder...) into their account's folder - but only when
    /// exactly one account could have made them, so nobody's backups get
    /// attached to the wrong account. Anything ambiguous is left untouched on
    /// disk. legacyNames maps each account folder to the display name the old
    /// code keyed it by.</summary>
    public static void MigrateLegacyFolders(IReadOnlyDictionary<string, string> legacyNameByAccountFolder)
    {
        string backupsRoot = LocalAppDataPaths.GetSubfolder(RootSubfolder);
        foreach (var byName in legacyNameByAccountFolder.GroupBy(kv => SanitizeForPath(kv.Value), StringComparer.OrdinalIgnoreCase))
        {
            if (byName.Count() != 1) continue;
            string legacy = Path.Combine(backupsRoot, byName.Key);
            string target = Path.Combine(backupsRoot, AccountKey(byName.First().Key));
            if (!Directory.Exists(legacy) || string.Equals(legacy, target, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                foreach (var group in Directory.GetDirectories(legacy))
                {
                    string targetGroup = Path.Combine(target, Path.GetFileName(group));
                    Directory.CreateDirectory(targetGroup);
                    foreach (var point in Directory.GetDirectories(group))
                    {
                        string targetPoint = Path.Combine(targetGroup, Path.GetFileName(point));
                        if (!Directory.Exists(targetPoint)) Directory.Move(point, targetPoint);
                    }
                    if (Directory.GetFileSystemEntries(group).Length == 0) Directory.Delete(group);
                }
                if (Directory.GetFileSystemEntries(legacy).Length == 0) Directory.Delete(legacy);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Copies every currently-existing file in sourceFilePaths into a
    /// NEW timestamped subfolder under this group, preserving each file's own
    /// name. Missing files (e.g. a slot with only one real file even though
    /// its rotating pair could theoretically have two) are skipped rather
    /// than failing the whole backup.</summary>
    public static void BackupFiles(IEnumerable<string> sourceFilePaths, string groupKey)
    {
        var existing = sourceFilePaths.Where(File.Exists).ToList();
        if (existing.Count == 0) return;

        string timestamp = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string restorePointFolder = LocalAppDataPaths.GetSubfolder(Path.Combine(RootSubfolder, groupKey, timestamp));

        foreach (var path in existing)
            File.Copy(path, Path.Combine(restorePointFolder, Path.GetFileName(path)), overwrite: true);
    }

    /// <summary>Every restore point for this group, newest first.</summary>
    public static List<BackupRestorePoint> GetRestorePoints(string groupKey)
    {
        string groupFolder = LocalAppDataPaths.GetSubfolder(Path.Combine(RootSubfolder, groupKey));

        var points = new List<BackupRestorePoint>();
        foreach (var dir in Directory.EnumerateDirectories(groupFolder))
        {
            string folderName = Path.GetFileName(dir);
            if (!DateTime.TryParseExact(folderName, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                continue; // not one of ours (or a stray folder) - skip rather than guess

            var fileNames = Directory.GetFiles(dir).Select(Path.GetFileName).Where(f => f is not null).Select(f => f!).ToList();
            if (fileNames.Count == 0) continue;

            points.Add(new BackupRestorePoint(dir, timestamp, fileNames));
        }

        return points.OrderByDescending(p => p.Timestamp).ToList();
    }

    /// <summary>Restores every file in restorePoint back to its original
    /// location, matched by filename against currentFilePathsByName (the
    /// CALLER'S known-current file set - restoring doesn't try to guess
    /// where a file "should" live on its own). Backs up whatever's currently
    /// on disk first, under the same group, so a restore is itself
    /// undoable - it just becomes the newest restore point.</summary>
    public static void Restore(BackupRestorePoint restorePoint, IReadOnlyDictionary<string, string> currentFilePathsByName, string groupKey)
    {
        BackupFiles(currentFilePathsByName.Values, groupKey);

        foreach (var fileName in restorePoint.FileNames)
        {
            if (!currentFilePathsByName.TryGetValue(fileName, out var destinationPath)) continue;
            string sourcePath = Path.Combine(restorePoint.FolderPath, fileName);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static string SanitizeForPath(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }
}
