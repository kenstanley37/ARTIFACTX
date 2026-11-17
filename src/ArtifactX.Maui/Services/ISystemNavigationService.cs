namespace ArtifactX.Maui.Services;

public interface ISystemNavigationService
{
    Task StartScanAsync();
    Task OpenScanResultAsync(string portalCode, int galaxyNumber);
    Task OpenAsync(string portalCode, int galaxyNumber);
}
