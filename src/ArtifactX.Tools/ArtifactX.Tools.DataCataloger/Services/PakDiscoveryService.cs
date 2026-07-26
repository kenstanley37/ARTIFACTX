using ArtifactX.Tools.DataCataloger.Models;
using ArtifactX.Tools.DataCataloger.Services.Interfaces;

namespace ArtifactX.Tools.DataCataloger.Services;

public class PakDiscoveryService : IPakDiscoveryService
{
    public IReadOnlyList<PakInfo> Discover(string pcbanksPath)
    {
        if (string.IsNullOrWhiteSpace(pcbanksPath))
            throw new ArgumentException("PCBANKS path is null or empty.", nameof(pcbanksPath));

        if (!Directory.Exists(pcbanksPath))
            throw new DirectoryNotFoundException($"PCBANKS path does not exist: {pcbanksPath}");

        var pakFiles = Directory.EnumerateFiles(pcbanksPath, "*.pak", SearchOption.TopDirectoryOnly);

        // for testing
        //var pakFiles = Directory.EnumerateFiles(pcbanksPath, "ArtifactXARC.globals.pak", SearchOption.TopDirectoryOnly);

        var result = new List<PakInfo>();

        foreach (var fullPath in pakFiles)
        {
            var fileInfo = new FileInfo(fullPath);

            result.Add(new PakInfo
            {
                FileName = fileInfo.Name,
                FullPath = fileInfo.FullName,
                FileSize = fileInfo.Length,
                Hash = string.Empty // placeholder for now
            });
        }

        return result;
    }
}