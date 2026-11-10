using ArtifactX.PortalScanning;

namespace ArtifactX.Maui.Services;

public interface IPortalPhotoScanService
{
    Task<PortalScanResult?> ScanAsync(CancellationToken cancellationToken);
}
