using Pfim;
using SkiaSharp;

namespace ArtifactX.Tools.DataCataloger.Services;

public class IconExtractionService
{
    private readonly ExtractionService _extractor;
    private readonly GlobalFileIndexService _index;
    private readonly int _targetSize;

    // Keyed by source texture path so the same icon (referenced by hundreds of rows)
    // is only ever extracted/decoded/resized once per run.
    private readonly Dictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IconExtractionService(ExtractionService extractor, GlobalFileIndexService index, int targetSize = 128)
    {
        _extractor = extractor;
        _index = index;
        _targetSize = targetSize;
    }

    /// <summary>
    /// Resolves a texture path (as referenced from a table row's Icon/HeroIcon field),
    /// converts DDS -> resized PNG entirely in memory, and returns the PNG bytes - or null
    /// if the source texture couldn't be found/decoded. Results are cached by source path.
    /// </summary>
    public byte[]? ExtractAndConvert(string texturePath)
    {
        if (_cache.TryGetValue(texturePath, out var cached))
            return cached;

        byte[]? result = ExtractAndConvertCore(texturePath);
        _cache[texturePath] = result;
        return result;
    }

    private byte[]? ExtractAndConvertCore(string texturePath)
    {
        if (!_index.TryFind(texturePath, out var pakPath, out var entry, out var header))
        {
            LogService.Write($"  Icon not found in any PAK: {texturePath}");
            return null;
        }

        byte[]? sourceBytes = _extractor.ExtractEntryBytes(pakPath, entry, header);
        if (sourceBytes == null || sourceBytes.Length == 0)
        {
            LogService.Write($"  Failed to extract icon bytes: {texturePath}");
            return null;
        }

        try
        {
            using SKBitmap bitmap = texturePath.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase)
                ? SKBitmap.Decode(sourceBytes)
                : DecodeDds(sourceBytes);

            using SKBitmap resized = ResizeToFit(bitmap, _targetSize);
            using SKImage image = SKImage.FromBitmap(resized);

            // NOTE: verify SKFilterQuality vs SKSamplingOptions against whichever exact
            // SkiaSharp version you land on - the resize API has shifted between major
            // versions and I can't reach nuget.org from this sandbox to pin the exact
            // signature for 4.150.1. The overall shape (decode -> resize -> encode) is
            // correct regardless; only the resize call's parameter type may need a tweak.
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);

            return data.ToArray();
        }
        catch (Exception ex)
        {
            LogService.Write($"  Failed to convert icon {texturePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The static entry point is `Pfimage` (renamed from `Pfim` upstream specifically to
    /// avoid colliding with the `Pfim` namespace itself). Pfim decodes to a raw byte buffer
    /// in BGRA order for the common 32bpp formats ArtifactX UI icons use. Stride can include row
    /// padding, so index by (row * Stride) rather than assuming Width * bytesPerPixel.
    /// Uses SKBitmap.SetPixel per-pixel rather than raw pointer access - slower, but this
    /// is a one-shot batch tool (not a hot path) and avoids needing &lt;AllowUnsafeBlocks&gt;
    /// in the csproj for what's a few thousand icons total.
    /// </summary>
    private static SKBitmap DecodeDds(byte[] ddsBytes)
    {
        using IImage pfimImage = Pfimage.FromStream(new MemoryStream(ddsBytes));

        var info = new SKImageInfo(pfimImage.Width, pfimImage.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);

        int bytesPerPixel = pfimImage.BitsPerPixel / 8;

        for (int y = 0; y < pfimImage.Height; y++)
        {
            int rowStart = y * pfimImage.Stride;

            for (int x = 0; x < pfimImage.Width; x++)
            {
                int i = rowStart + x * bytesPerPixel;

                byte b = pfimImage.Data[i];
                byte g = pfimImage.Data[i + 1];
                byte r = pfimImage.Data[i + 2];
                byte a = bytesPerPixel >= 4 ? pfimImage.Data[i + 3] : (byte)255;

                bitmap.SetPixel(x, y, new SKColor(r, g, b, a));
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Resize preserving aspect ratio, fitting within targetSize x targetSize, never
    /// upscaling anything already smaller (mirrors ImageSharp's ResizeMode.Max behavior).
    /// </summary>
    private static SKBitmap ResizeToFit(SKBitmap source, int targetSize)
    {
        double scale = Math.Min(1.0, (double)targetSize / Math.Max(source.Width, source.Height));
        int dstWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        int dstHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

        if (dstWidth == source.Width && dstHeight == source.Height)
            return source.Copy();

        var dstInfo = new SKImageInfo(dstWidth, dstHeight, source.ColorType, source.AlphaType);

        // SKFilterQuality is obsolete/removed in current SkiaSharp; SKSamplingOptions is
        // the live API. Linear filtering + linear mipmap mode gives good quality for
        // downscaling, which is all this pipeline ever does (never upscales).
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        SKBitmap? resized = source.Resize(dstInfo, sampling);

        // Resize can return null on failure (e.g. degenerate 0-size source) - fall back
        // to an unscaled copy rather than propagating a null into the caller.
        return resized ?? source.Copy();
    }
}