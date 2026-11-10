using Camera.MAUI;

namespace ArtifactX.Maui.Services;

/// <summary>Samples the in-app preview, with one owned in-memory frame at a time. Does not take still photos or record.</summary>
public sealed class CameraPreviewSource(CameraView camera) : IPortalPreviewSource
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            throw new CameraScanException("Allow camera access to scan the screen. You can still enter an address by hand.");
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, EventArgs args) => loaded.TrySetResult();
        camera.CamerasLoaded += OnLoaded;
        try
        {
            if (camera.Cameras.Count == 0)
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            camera.Camera = camera.Cameras.FirstOrDefault(c => c.Position == CameraPosition.Back)
                ?? camera.Cameras.FirstOrDefault()
                ?? throw new CameraScanException("No camera was found. You can enter an address by hand.");
            camera.FlashMode = FlashMode.Disabled;
            if (await camera.StartCameraAsync(new Size(1920, 1080)) != CameraResult.Success)
                throw new CameraScanException("The camera could not start. Close other camera apps and try again.");
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { camera.CamerasLoaded -= OnLoaded; }
    }

    public async Task<Stream?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Snapshot APIs sample preview pixels: there is no still capture, shutter or gallery write.
        using var frame = camera.GetSnapShotStream(Camera.MAUI.ImageFormat.PNG);
        if (frame is null) return null;
        var owned = new MemoryStream();
        try
        {
            await frame.CopyToAsync(owned, cancellationToken);
            owned.Position = 0;
            return owned;
        }
        catch { owned.Dispose(); throw; }
    }

    public async Task StopAsync() => await camera.StopCameraAsync();
}
