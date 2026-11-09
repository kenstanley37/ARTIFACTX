using ArtifactX.PortalScanning;

namespace ArtifactX.Maui.Services;

public interface IPortalPhotoScanService
{
    Task<PortalScanResult?> ScanAsync(bool useCamera, CancellationToken cancellationToken);
}
