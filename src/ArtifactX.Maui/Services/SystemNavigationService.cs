namespace ArtifactX.Maui.Services;

public sealed class SystemNavigationService : ISystemNavigationService
{
    public Task OpenAsync(string portalCode, int galaxyNumber) => Shell.Current.GoToAsync(
        $"system-details?code={Uri.EscapeDataString(portalCode)}&galaxy={galaxyNumber}");
}
