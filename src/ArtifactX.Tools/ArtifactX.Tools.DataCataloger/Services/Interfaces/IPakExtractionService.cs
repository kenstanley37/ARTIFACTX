using ArtifactX.Tools.DataCataloger.Models;

namespace ArtifactX.Tools.DataCataloger.Services.Interfaces;

public interface IPakExtractionService
{
    byte[] ExtractEntry(string pakPath, PakEntry entry, PakHeader header);
    byte[] ExtractUncompressedFile(string pakPath, PakHeader header, PakEntry entry);


}

