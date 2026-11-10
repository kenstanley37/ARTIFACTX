namespace ArtifactX.Maui.Services;

/// <summary>Owns preview access. Each returned frame belongs to the caller and is never saved.</summary>
public interface IPortalPreviewSource
{
    Task StartAsync(CancellationToken cancellationToken);
    Task<Stream?> ReadFrameAsync(CancellationToken cancellationToken);
    Task StopAsync();
}

public sealed class CameraScanException(string message) : Exception(message);
