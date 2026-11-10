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
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using var image = PortalPhotoDecoder.Decode(frame);
            PortalScanDiagnostics.Write($"Decode {image.Width} x {image.Height}: {watch.ElapsedMilliseconds} ms");
            var result = RecognizeFrame(image, cancellationToken);
            PortalScanDiagnostics.Write($"Glyphs {result.Candidate ?? "none"} (certain={!result.NeedsCorrection}): {watch.ElapsedMilliseconds} ms " +
                string.Join(" ", result.Glyphs.Select((g, i) => g.IsCertain ? "" : $"#{i + 1}:{g.Digit} d={g.Distance:F3} m={g.Margin:F2}")).Trim());
            byte[]? bytes = null;
            PortalScanTarget? targetBounds = null;
            // Strips with one uncertain symbol still count toward multi-frame agreement, so read their galaxy too.
            if (result.Region is { } region && result.HasCandidate)
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
        var ocrWatch = System.Diagnostics.Stopwatch.StartNew();
        var lines = await textReader.ReadLinesAsync(crop, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        int? galaxy = _galaxies.Match(lines);
        PortalScanDiagnostics.Write($"Galaxy OCR {ocrWatch.ElapsedMilliseconds} ms -> {galaxy?.ToString() ?? "no match"} [{string.Join(" | ", lines)}]");
        return new(glyphs, galaxy) { Target = target };
    }

    private PortalScanResult RecognizeFrame(SKBitmap image, CancellationToken cancellationToken)
    {
        var recognizer = new PortalGlyphRecognizer(_model!);
        if (_trackedRegion is { } previous)
        {
            var area = PortalGlyphRecognizer.TrackingArea(previous, image.Width, image.Height);
            PortalScanDiagnostics.Write($"Tracked glyph search: {area.Right - area.Left} x {area.Bottom - area.Top}");
            if (area.Right > area.Left && area.Bottom > area.Top)
            {
                // The recognizer crops to this area and returns full-frame coordinates.
                var tracked = recognizer.Recognize(image, cancellationToken, area);
                if (tracked.HasCandidate)
                {
                    PortalScanDiagnostics.Write("Tracked search hit");
                    _trackedRegion = tracked.Region;
                    return tracked;
                }
                PortalScanDiagnostics.Write("Tracked search missed; full-frame search");
            }
        }
        // Moving the phone away from the previous strip must recover through a full-frame search.
        var result = recognizer.Recognize(image, cancellationToken);
        _trackedRegion = result.Region;
        return result;
    }

}
