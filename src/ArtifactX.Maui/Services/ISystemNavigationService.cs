namespace ArtifactX.Maui.Services;

public interface ISystemNavigationService
{
    Task OpenAsync(string portalCode, int galaxyNumber);
}
