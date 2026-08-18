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
        string helloGamesRoot = Path.Combine(appData, "HelloGames", "NMS");
        if (Directory.Exists(helloGamesRoot))
        {
            foreach (var dir in Directory.GetDirectories(helloGamesRoot))
            {
                string leaf = Path.GetFileName(dir);
                SaveFolderSource? source = leaf.StartsWith("st_", StringComparison.OrdinalIgnoreCase) ? SaveFolderSource.Steam
                    : leaf.StartsWith("gog_", StringComparison.OrdinalIgnoreCase) ? SaveFolderSource.Gog
                    : null;

                if (source is not null)
                    found.Add(BuildCandidate(dir, source.Value));
            }
        }

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