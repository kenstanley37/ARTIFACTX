using NMS.Tools.DataCataloger.Models;

namespace NMS.Tools.DataCataloger.Services.Interfaces;

public interface IPakDiscoveryService
{
    IReadOnlyList<PakInfo> Discover(string pcbanksPath);
}