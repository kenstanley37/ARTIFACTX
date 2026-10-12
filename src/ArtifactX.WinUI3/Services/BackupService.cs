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
/// replaced by this). Lives in the backups folder (BackupLocation - by default
/// %LOCALAPPDATA%\ArtifactX\Backups, or one the player chose such as OneDrive),
/// never alongside the real save files - keeps the user's actual NMS save
/// folder untouched as backups accumulate, and there's no risk of the game
/// itself mistaking a backup for a real save (NMS only looks for save*.hg).
///
/// Keeps the newest KeepPerSlot backups of each slot (default 20, "all" is an
/// option), shown and changed on the Settings page. This used to keep every
/// backup forever on the assumption saves are small; a real main save had 98
/// backups taking 554 MB by 2026-10-12, since every save from ArtifactX makes
/// one. Older backups are only removed when a newer one is made for that same
/// slot, or when the player asks on the Settings page.
/// </summary>
public static class BackupService
{
    private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

    /// <summary>Folder for backups of the player's own data (UserDatabase), not a game account.</summary>
    public const string UserDataFolder = "user-db";

    public const int DefaultKeepPerSlot = 20;
    private const string KeepPerSlotKey = "SaveBackupsKeepPerSlot";

    /// <summary>How many backups to keep per save slot; 0 keeps them all.</summary>
    public static int KeepPerSlot
    {
        get => int.TryParse(SettingsStore.Get(SettingsStore.Scope.Profile, KeepPerSlotKey), out int keep) && keep >= 0 ? keep : DefaultKeepPerSlot;
        set => SettingsStore.Set(SettingsStore.Scope.Profile, KeepPerSlotKey, value.ToString(CultureInfo.InvariantCulture));
    }

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
        string backupsRoot = BackupLocation.Root;
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
        string restorePointFolder = BackupLocation.Sub(Path.Combine(groupKey, timestamp));

        foreach (var path in existing)
            File.Copy(path, Path.Combine(restorePointFolder, Path.GetFileName(path)), overwrite: true);

        Prune(groupKey, KeepPerSlot);
    }

    /// <summary>Every restore point for this group, newest first.</summary>
    public static List<BackupRestorePoint> GetRestorePoints(string groupKey)
    {
        string groupFolder = BackupLocation.Sub(groupKey);

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

    /// <summary>One slot's (or an account's AccountData) backups, for the Settings page.</summary>
    public sealed record BackupGroupInfo(string AccountFolder, string Group, int Count, long Bytes, DateTime? Newest, int CountBeyondKeep, long BytesBeyondKeep);

    /// <summary>Every group of game save backups in the backups folder, with
    /// counts and sizes - and how much keeping only `keep` per group would free.</summary>
    public static List<BackupGroupInfo> Summarize(int keep)
    {
        var groups = new List<BackupGroupInfo>();
        foreach (var account in Directory.GetDirectories(BackupLocation.Root))
        {
            string accountName = Path.GetFileName(account);
            if (string.Equals(accountName, UserDataFolder, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var group in Directory.GetDirectories(account))
            {
                var points = GetRestorePoints(Path.Combine(accountName, Path.GetFileName(group)));
                if (points.Count == 0) continue;
                long Size(BackupRestorePoint p) => Directory.GetFiles(p.FolderPath).Sum(f => new FileInfo(f).Length);
                var sizes = points.Select(Size).ToList();
                var beyond = keep > 0 ? sizes.Skip(keep).ToList() : new List<long>();
                groups.Add(new BackupGroupInfo(accountName, Path.GetFileName(group), points.Count, sizes.Sum(), points[0].Timestamp, beyond.Count, beyond.Sum()));
            }
        }
        return groups;
    }

    /// <summary>Deletes all but the newest `keep` backups in one group (0 keeps all).
    /// Returns how many were removed.</summary>
    public static int Prune(string groupKey, int keep)
    {
        if (keep <= 0) return 0;
        int removed = 0;
        foreach (var old in GetRestorePoints(groupKey).Skip(keep))
        {
            try { Directory.Delete(old.FolderPath, recursive: true); removed++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return removed;
    }

    /// <summary>Prune every group to `keep`.</summary>
    public static int PruneAll(int keep) =>
        Summarize(keep).Sum(g => Prune(Path.Combine(g.AccountFolder, g.Group), keep));

    /// <summary>A readable name for a backup's account folder ("Steam_76561198..."):
    /// the account's nickname or player name when ArtifactX has seen it.</summary>
    public static string AccountLabel(string accountFolder)
    {
        int split = accountFolder.IndexOf('_');
        string platform = split > 0 ? accountFolder[..split] : accountFolder;
        string id = split > 0 ? accountFolder[(split + 1)..] : "";
        string? name = platform switch
        {
            "Steam" => GameAccountsService.Name("steam:" + id),
            "GOG" => GameAccountsService.Name("gog:" + id),
            "Folder" => GameAccountsService.Name("folder:" + id),
            _ => null,
        };
        string shortId = id.Length >= 4 ? $"\u2026{id[^4..]}" : id;
        return platform switch
        {
            "Folder" => name ?? "Other save folder",
            _ => name is null ? $"{platform} account {shortId}" : $"{platform} \u00B7 {name}",
        };
    }

    private static string SanitizeForPath(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }
}
