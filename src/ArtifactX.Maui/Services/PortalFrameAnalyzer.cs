using System.Text.Json;
using ArtifactX.Core.NmsModels;
using ArtifactX.PortalScanning;
using SkiaSharp;

namespace ArtifactX.Maui.Services;

/// <summary>Reads transient preview frames locally. Galaxy OCR is restricted to the area below the detected glyph strip.</summary>
public sealed class PortalFrameAnalyzer(IGalaxyTextReader textReader) : IPortalFrameAnalyzer
{
    private PortalGlyphModel? _model;
    private readonly GalaxyNameMatcher _galaxies = new(NmsGalaxyInfo.AllGalaxies.ToDictionary(g => g.Number, g => g.Name));

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_model is not null) return;
        using var stream = await FileSystem.OpenAppPackageFileAsync("portal-glyph-model.json");
        var model = await JsonSerializer.DeserializeAsync(stream, PortalGlyphModelJsonContext.Default.PortalGlyphModel, cancellationToken)
            ?? throw new InvalidDataException("Scanner data is empty.");
        model.Validate();
        _model = model;
    }

    public async Task<LivePortalObservation> AnalyzeAsync(Stream frame, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        // Decode, glyph matching and crop/encode never block the UI thread.
        var (glyphs, crop) = await Task.Run(() =>
        {
            using var image = PortalPhotoDecoder.Decode(frame);
            var result = new PortalGlyphRecognizer(_model!).Recognize(image, cancellationToken);
            byte[]? bytes = null;
            if (result.Region is { } region && result.HasCandidate && !result.NeedsCorrection)
            {
                int height = Math.Max(1, region.Bottom - region.Top);
                var rectangle = new SKRectI(Math.Max(0, region.Left - height), Math.Max(0, region.Bottom - height / 4),
                    Math.Min(image.Width, region.Right + height), Math.Min(image.Height, region.Bottom + height * 4));
                if (rectangle.Width > 0 && rectangle.Height >= 8)
                {
                    using var cropped = new SKBitmap();
                    if (image.ExtractSubset(cropped, rectangle))
                    {
                        // Enlarge the text crop for small on-screen galaxy lettering; this does not invent detail.
                        using var enlarged = cropped.Resize(new SKImageInfo(cropped.Width * 2, cropped.Height * 2),
                            new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new InvalidDataException("Galaxy text crop could not be prepared.");
                        using var encoded = enlarged.Encode(SKEncodedImageFormat.Png, 100);
                        bytes = encoded.ToArray();
                    }
                }
            }
            return (result, bytes);
        }, cancellationToken);
        if (crop is null) return new(glyphs, null);
        var lines = await textReader.ReadLinesAsync(crop, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(glyphs, _galaxies.Match(lines));
    }
}
