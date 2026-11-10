using System.Text.Json;
using ArtifactX.Core.NmsModels;
using ArtifactX.PortalScanning;
using SkiaSharp;

namespace ArtifactX.Maui.Services;

/// <summary>Reads transient preview frames locally. Galaxy OCR is restricted to the area below the detected glyph strip.</summary>
public sealed class PortalFrameAnalyzer(IGalaxyTextReader textReader) : IPortalFrameAnalyzer
{
    private PortalGlyphModel? _model;
    private ScanRegion? _trackedRegion;
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
        var (glyphs, crop, target) = await Task.Run(() =>
        {
            using var image = PortalPhotoDecoder.Decode(frame);
            var result = RecognizeFrame(image, cancellationToken);
            byte[]? bytes = null;
            PortalScanTarget? targetBounds = null;
            if (result.Region is { } region && result.HasCandidate && !result.NeedsCorrection)
            {
                int height = Math.Max(1, region.Bottom - region.Top);
                var rectangle = new SKRectI(Math.Max(0, region.Left - height), Math.Max(0, region.Bottom - height / 4),
                    Math.Min(image.Width, region.Right + height), Math.Min(image.Height, region.Bottom + height * 4));
                targetBounds = PortalScanTarget.FromRegion(new ScanRegion(rectangle.Left,
                    Math.Max(0, region.Top - height / 4), rectangle.Right,
                    Math.Min(image.Height, region.Bottom + height * 2)), image.Width, image.Height);
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
            return (result, bytes, targetBounds);
        }, cancellationToken);
        if (crop is null) return new(glyphs, null) { Target = target };
        var lines = await textReader.ReadLinesAsync(crop, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(glyphs, _galaxies.Match(lines)) { Target = target };
    }

    private PortalScanResult RecognizeFrame(SKBitmap image, CancellationToken cancellationToken)
    {
        var recognizer = new PortalGlyphRecognizer(_model!);
        if (_trackedRegion is { } previous)
        {
            int height = Math.Max(8, previous.Bottom - previous.Top);
            int margin = Math.Max(height, (previous.Right - previous.Left) / 12);
            int verticalMargin = Math.Max(12, height / 2);
            var rectangle = new SKRectI(Math.Max(0, previous.Left - margin), Math.Max(0, previous.Top - verticalMargin),
                Math.Min(image.Width, previous.Right + margin), Math.Min(image.Height, previous.Bottom + verticalMargin));
            PortalScanDiagnostics.Write($"Tracked glyph search: {rectangle.Width} x {rectangle.Height}");
            if (rectangle.Width > 0 && rectangle.Height > 0)
            {
                // Keep the full-frame coordinate system: cropping shifts quantized candidate bands and
                // can change the classifier's sampling. Only the search mask is restricted.
                var tracked = recognizer.Recognize(image, cancellationToken,
                    new ScanRegion(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom));
                if (tracked.HasCandidate && !tracked.NeedsCorrection)
                {
                    _trackedRegion = tracked.Region;
                    return tracked;
                }
            }
        }
        // Moving the phone away from the previous strip must recover through a full-frame search.
        var result = recognizer.Recognize(image, cancellationToken);
        _trackedRegion = result.Region;
        return result;
    }

}
