using Pfim;
using SkiaSharp;

namespace ArtifactX.GameData.Services;

public class IconExtractionService
{
    private readonly ExtractionService _extractor;
    private readonly GlobalFileIndexService _index;
    private readonly int _targetSize;

    // Keyed by source texture path so the same icon (referenced by hundreds of rows)
    // is only ever extracted/decoded/resized once per run. Concurrent: ConvertAll
    // fills it from several threads.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Converts every one of these textures at once, on all but one of
    /// the PC's cores (2026-10-14: ~123 ms per icon one at a time - about 8 of
    /// a 13-minute build's minutes for ~4,000 icons). Results land in the cache
    /// for <see cref="ExtractAndConvert"/>. Safe in parallel: each read opens its
    /// own handle on the PAK, the file index is read-only by now, and Pfim/Skia
    /// decode into per-call buffers.</summary>
    public void ConvertAll(IReadOnlyCollection<string> texturePaths, Action<double>? progress = null)
    {
        int done = 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        Parallel.ForEach(texturePaths, options, path =>
        {
            ExtractAndConvert(path);
            int n = Interlocked.Increment(ref done);
            if (n % 50 == 0) progress?.Invoke((double)n / texturePaths.Count);
        });
    }

    public IconExtractionService(ExtractionService extractor, GlobalFileIndexService index, int targetSize = 128,
        string? diskCacheFolder = null)
    {
        _extractor = extractor;
        _index = index;
        _targetSize = targetSize;
        _diskCache = diskCacheFolder;
        if (_diskCache is not null)
        {
            try { Directory.CreateDirectory(_diskCache); }
            catch (Exception ex) { LogService.Write($"  Icon cache unavailable ({ex.Message}); converting every icon."); _diskCache = null; }
        }
    }

    /// <summary>
    /// Converted icons kept between builds, one PNG per source texture content: the file name is the SHA-256 of the
    /// texture's bytes and the target size, so an icon a game update didn't touch is read back instead of decoded,
    /// resized and encoded again (converting all ~4,300 was 64 of a 3m16s build, 2026-11-17), and one it changed gets a
    /// new name. The same pipeline wrote them, so the catalog's bytes are unchanged. Null: no cache.
    /// </summary>
    private readonly string? _diskCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _usedCacheFiles = new(StringComparer.OrdinalIgnoreCase);
    private int _cacheHits;

    /// <summary>Icons read back from the disk cache this run.</summary>
    public int CacheHits => _cacheHits;

    /// <summary>Deletes cached icons this run didn't use (textures the game no longer has, or replaced), so the cache
    /// never outgrows one build's icons. Call once, after converting everything the build references.</summary>
    public void PruneDiskCache()
    {
        if (_diskCache is null) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(_diskCache, "*.png"))
                if (!_usedCacheFiles.ContainsKey(Path.GetFileName(file)))
                    try { File.Delete(file); } catch (IOException) { }
        }
        catch (Exception ex) { LogService.Write($"  Icon cache clean-up skipped: {ex.Message}"); }
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

        return _cache.GetOrAdd(texturePath, ExtractAndConvertCore);
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

        string? cacheFile = null;
        if (_diskCache is not null)
        {
            string name = $"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes))}_{_targetSize}.png";
            _usedCacheFiles[name] = 0;
            cacheFile = Path.Combine(_diskCache, name);
            try
            {
                if (File.Exists(cacheFile) && File.ReadAllBytes(cacheFile) is { Length: > 0 } stored)
                {
                    Interlocked.Increment(ref _cacheHits);
                    return stored;
                }
            }
            catch (IOException) { /* unreadable: convert again below */ }
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

            byte[] png = data.ToArray();
            if (cacheFile is not null)
                try { File.WriteAllBytes(cacheFile, png); } catch (IOException) { /* still converted; cached next time */ }
            return png;
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
    ///
    /// Pixels are reordered to RGBA in a managed buffer and copied into the bitmap a row at
    /// a time. This used SKBitmap.SetPixel per pixel - a native call for each of ~65,000
    /// pixels, ~120 ms an icon and, it turned out, no faster spread over many threads; with
    /// players now waiting on the build that was most of it (2026-10-14). Same pixels, so
    /// the PNGs come out byte-for-byte the same.
    /// </summary>
    private static SKBitmap DecodeDds(byte[] ddsBytes)
    {
        using IImage pfimImage = Pfimage.FromStream(new MemoryStream(ddsBytes));

        int width = pfimImage.Width, height = pfimImage.Height;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);

        int bytesPerPixel = pfimImage.BitsPerPixel / 8;
        byte[] source = pfimImage.Data;
        byte[] row = new byte[width * 4];
        IntPtr pixels = bitmap.GetPixels();

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * pfimImage.Stride;

            for (int x = 0; x < width; x++)
            {
                int i = rowStart + x * bytesPerPixel;
                int o = x * 4;

                row[o] = source[i + 2];     // r
                row[o + 1] = source[i + 1]; // g
                row[o + 2] = source[i];     // b
                row[o + 3] = bytesPerPixel >= 4 ? source[i + 3] : (byte)255;
            }

            System.Runtime.InteropServices.Marshal.Copy(row, 0, pixels + y * bitmap.RowBytes, row.Length);
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