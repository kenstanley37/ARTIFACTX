using ArtifactX.GameData.Models;

namespace ArtifactX.GameData.Services.Interfaces;

public interface IPakReaderService
{
    IReadOnlyList<PakEntry> Read(string pakPath);

    PakHeader ReadHeader(string pakPath);
}