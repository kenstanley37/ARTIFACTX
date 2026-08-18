using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Services.Interfaces;

public interface IPakDiscoveryService
{
    IReadOnlyList<PakInfo> Discover(string pcbanksPath);
}