using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Services.Interfaces;

public interface IPakReaderService
{
    IReadOnlyList<PakEntry> Read(string pakPath);

    PakHeader ReadHeader(string pakPath);
}