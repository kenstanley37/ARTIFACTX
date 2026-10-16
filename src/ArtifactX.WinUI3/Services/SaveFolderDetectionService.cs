using ArtifactX.WinUI3.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Locates likely NMS save folders across supported storefronts. Pure filesystem
/// probing — no UI, no state — so it's easy to unit test and safe to call from a ViewModel.
/// </summary>
public static class SaveFolderDetectionService
{
    public static IReadOnlyList<SaveFolderCandidate> DetectCandidates()
    {
        var found = new List<SaveFolderCandidate>();

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Steam and GOG share the same HelloGames\NMS root; the leaf folder prefix
        // ("st_" / "gog_") tells them apart.
        AddAccountFolders(found, Path.Combine(appData, "HelloGames", "NMS"), isDevTest: false);
#if DEBUG
        AddAccountFolders(found, DevTestRoot, isDevTest: true);
#endif

        // Microsoft Store / Xbox: saves live in an opaque per-app container.
        // We can find the folder but can't validate its contents (see model SubText).
        string packagesRoot = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesRoot))
        {
            foreach (var packageDir in Directory.GetDirectories(packagesRoot, "HelloGames.NoMansSky*"))
            {
                string wgsPath = Path.Combine(packageDir, "SystemAppData", "wgs");
                if (Directory.Exists(wgsPath))
                {
                    found.Add(new SaveFolderCandidate
                    {
                        FolderPath = wgsPath,
                        Source = SaveFolderSource.MicrosoftStore,
                        DetectedSaveFileCount = 0,
                        IsValidated = false
                    });
                }
            }
        }

        return found;
    }

    /// <summary>Debug builds also scan this folder, so Save Selection can be
    /// tried with several accounts: put copies of an account folder here under
    /// made-up names (st_76561198000000001, gog_12345678...). It's outside both
    /// the repository and the game's own save folder, so test copies never get
    /// committed or uploaded by Steam Cloud.</summary>
    public static string DevTestRoot =>
        Path.Combine(LocalAppDataPaths.RootFolder, "DevFakeAccounts");

    /// <summary>Steam and GOG keep one folder per game account under the same
    /// HelloGames\NMS root, named st_&lt;SteamID64&gt; / gog_&lt;id&gt;.</summary>
    private static void AddAccountFolders(List<SaveFolderCandidate> found, string root, bool isDevTest)
    {
        if (!Directory.Exists(root)) return;
        foreach (var dir in Directory.GetDirectories(root))
        {
            string leaf = Path.GetFileName(dir);
            SaveFolderSource? source = leaf.StartsWith("st_", StringComparison.OrdinalIgnoreCase) ? SaveFolderSource.Steam
                : leaf.StartsWith("gog_", StringComparison.OrdinalIgnoreCase) ? SaveFolderSource.Gog
                : null;
            if (source is null) continue;

            string accountId = leaf[(leaf.IndexOf('_') + 1)..];
            found.Add(new SaveFolderCandidate
            {
                FolderPath = dir,
                Source = source.Value,
                AccountId = accountId,
                IsDevTest = isDevTest,
                DetectedSaveFileCount = CountSaveFiles(dir),
                IsValidated = CountSaveFiles(dir) > 0,
                PlayerName = source == SaveFolderSource.Steam ? SteamAccountsService.PersonaName(accountId) : null,
                AvatarPath = source == SaveFolderSource.Steam ? SteamAccountsService.AvatarPath(accountId) : null,
            });
        }
    }

    public static SaveFolderCandidate BuildCandidate(string path, SaveFolderSource source) =>
        new()
        {
            FolderPath = path,
            Source = source,
            DetectedSaveFileCount = CountSaveFiles(path),
            IsValidated = CountSaveFiles(path) > 0
        };

    public static int CountSaveFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return 0;

        try
        {
            return Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Count(f => Path.GetFileName(f).StartsWith("save", StringComparison.OrdinalIgnoreCase) &&
                            Path.GetExtension(f).Equals(".hg", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }
}