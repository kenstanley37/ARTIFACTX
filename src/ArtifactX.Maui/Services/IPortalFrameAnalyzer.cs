using ArtifactX.PortalScanning;

namespace ArtifactX.Maui.Services;

public interface IPortalFrameAnalyzer
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<LivePortalObservation> AnalyzeAsync(Stream frame, CancellationToken cancellationToken);
}

public interface IGalaxyTextReader
{
    Task<IReadOnlyList<string>> ReadLinesAsync(byte[] image, CancellationToken cancellationToken);
}
