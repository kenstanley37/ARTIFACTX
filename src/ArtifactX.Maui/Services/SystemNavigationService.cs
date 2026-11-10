namespace ArtifactX.Maui.Services;

public sealed class SystemNavigationService : ISystemNavigationService
{
    public Task StartScanAsync() => Shell.Current.GoToAsync("live-portal-scan");

    public Task OpenScanResultAsync(string portalCode, int galaxyNumber) => Shell.Current.GoToAsync(
        $"../system-details?code={Uri.EscapeDataString(portalCode)}&galaxy={galaxyNumber}");

    public Task OpenAsync(string portalCode, int galaxyNumber) => Shell.Current.GoToAsync(
        $"system-details?code={Uri.EscapeDataString(portalCode)}&galaxy={galaxyNumber}");
}
