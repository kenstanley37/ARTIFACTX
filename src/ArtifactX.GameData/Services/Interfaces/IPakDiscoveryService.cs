using ArtifactX.GameData.Models;

namespace ArtifactX.GameData.Services.Interfaces;

public interface IPakDiscoveryService
{
    IReadOnlyList<PakInfo> Discover(string pcbanksPath);
}