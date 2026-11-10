using System.Text.Json;
using ArtifactX.PortalScanning;

namespace ArtifactX.Maui.Services;

/// <summary>Platform media access; images are processed locally and are never uploaded or saved to the gallery.</summary>
public sealed class PortalPhotoScanService : IPortalPhotoScanService
{
    public async Task<PortalScanResult?> ScanAsync(CancellationToken cancellationToken)
    {
        PortalGlyphModel model;
        try
        {
            using var modelStream = await FileSystem.OpenAppPackageFileAsync("portal-glyph-model.json");
            model = await JsonSerializer.DeserializeAsync(modelStream,
                PortalGlyphModelJsonContext.Default.PortalGlyphModel, cancellationToken)
                ?? throw new InvalidDataException("The scanner data is empty.");
            model.Validate();
        }
        catch (FileNotFoundException)
        {
            return new(null, [], "Scanner data is not installed in this build. You can enter the glyphs by hand.");
        }

        var options = new MediaPickerOptions { Title = "Read the 12 portal glyphs" };
        FileResult? photo;
        try
        {
            photo = (await MediaPicker.Default.PickPhotosAsync(options)).FirstOrDefault();
        }
        catch (PermissionException)
        {
            return new(null, [], "Photo access was denied. You can enter the glyphs by hand.");
        }
        if (photo is null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = await photo.OpenReadAsync();
        return await Task.Run(() =>
        {
            using var image = PortalPhotoDecoder.Decode(stream);
            return new PortalGlyphRecognizer(model).Recognize(image, cancellationToken);
        }, cancellationToken);
    }
}
