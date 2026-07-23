using NMS.Tools.DataCataloger.Models;

namespace NMS.Tools.DataCataloger.Services.Interfaces;

public interface IPakReaderService
{
    IReadOnlyList<PakEntry> Read(string pakPath);

    PakHeader ReadHeader(string pakPath);
}