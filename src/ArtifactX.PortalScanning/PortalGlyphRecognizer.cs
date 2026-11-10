using SkiaSharp;

namespace ArtifactX.PortalScanning;

/// <summary>
/// Experimental on-device white-glyph strip detector. Searches multiple brightness levels and
/// shallow viewing angles; requires twelve regularly spaced symbols. Never fabricates an address
/// when no strip fits. Initial reference pair: owner's screenshot and Pixel photo, 2026-11-09.
/// This is not general OCR and does not infer the galaxy from the glyphs.
/// </summary>
public sealed class PortalGlyphRecognizer(PortalGlyphModel model, Action<string>? diagnostic = null)
{
    private const string NotFound = "Could not read all 12 glyphs. Move closer, keep the strip level, and avoid glare. You can also enter the glyphs by hand.";

    /// <summary>
    /// Reads the strip from the whole image, or only from <paramref name="searchRegion"/> when tracking a
    /// strip found in an earlier camera frame. The region is cropped, not masked: on the Pixel 7 Pro a
    /// masked full-frame search took as long as an untracked one (~2.6 s Debug, 2026-11-10).
    /// </summary>
    public PortalScanResult Recognize(SKBitmap image, CancellationToken cancellationToken = default, ScanRegion? searchRegion = null)
    {
        if (searchRegion is not { } area) return RecognizeAllPasses(image, cancellationToken);
        // An even top keeps the band quantization (top / 2 * 2) aligned with the full frame.
        int left = Math.Clamp(area.Left, 0, image.Width), right = Math.Clamp(area.Right, 0, image.Width);
        int top = Math.Clamp(area.Top, 0, image.Height) & ~1, bottom = Math.Clamp(area.Bottom, 0, image.Height);
        if (right - left < 24 || bottom - top < 8) return new(null, [], NotFound);
        using var view = new SKBitmap();
        if (!image.ExtractSubset(view, new SKRectI(left, top, right, bottom))) return new(null, [], NotFound);
        using var crop = view.Copy();
        var result = RecognizeAllPasses(crop, cancellationToken);
        return result.Region is { } found
            ? result with { Region = new(found.Left + left, found.Top + top, found.Right + left, found.Bottom + top) }
            : result;
    }

    /// <summary>
    /// Search area around a strip found in the previous frame, allowing for hand movement between frames
    /// (~0.7 s apart). A half-strip vertical margin missed most hand-held Pixel frames on 2026-11-10.
    /// </summary>
    public static ScanRegion TrackingArea(ScanRegion previous, int imageWidth, int imageHeight)
    {
        int height = Math.Max(8, previous.Bottom - previous.Top);
        int margin = Math.Max(height * 2, (previous.Right - previous.Left) / 8);
        int verticalMargin = Math.Max(16, height);
        return new(Math.Max(0, previous.Left - margin), Math.Max(0, previous.Top - verticalMargin),
            Math.Min(imageWidth, previous.Right + margin), Math.Min(imageHeight, previous.Bottom + verticalMargin));
    }

    private PortalScanResult RecognizeAllPasses(SKBitmap image, CancellationToken cancellationToken)
    {
        // Preserve small, neutral screenshot strokes. Only try screen-specific colour/moire
        // recovery after that pass fails; every pass uses the same whole-strip acceptance gate.
        var direct = RecognizeCore(image, 0, cancellationToken);
        if (direct.HasCandidate) return direct;
        diagnostic?.Invoke("Trying screen-pattern suppression and blue-cast compensation.");
        var compensated = RecognizeCore(image, 1, cancellationToken);
        if (compensated.HasCandidate) return compensated;
        diagnostic?.Invoke("Trying smoothed blue-channel contrast.");
        return RecognizeCore(image, 2, cancellationToken);
    }

    private PortalScanResult RecognizeCore(SKBitmap image, int pass, CancellationToken cancellationToken)
    {
        bool suppressScreenPattern = pass != 0;
        model.Validate();
        int width = image.Width, height = image.Height;
        if (width > PortalPhotoDecoder.MaxDimension || height > PortalPhotoDecoder.MaxDimension)
            throw new ArgumentException("Decode the photo with PortalPhotoDecoder first.", nameof(image));
        byte[] brightness = new byte[width * height];
        byte[] saturation = new byte[brightness.Length];
        var colors = image.Pixels;
        for (int i = 0; i < colors.Length; i++)
        {
            int min = Math.Min(colors[i].Red, Math.Min(colors[i].Green, colors[i].Blue));
            brightness[i] = pass switch
            {
                // Blue/red opposition separates the blue-tinted white glyphs from a warm display.
                // The blue-only pass also retains strokes that have little opposing-colour signal.
                1 => (byte)Math.Clamp(128 + 2 * (colors[i].Blue - colors[i].Red), 0, 255),
                2 => colors[i].Blue,
                _ => (byte)min
            };
            saturation[i] = (byte)(Math.Max(colors[i].Red, Math.Max(colors[i].Green, colors[i].Blue)) - min);
        }
        if (suppressScreenPattern)
        {
            // Average at the working resolution to suppress the monitor's RGB subpixel grid.
            // Keep the unsmoothed pass first: very small screenshot glyphs need their thin strokes.
            int[] source = BuildIntegral(brightness, width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int l = Math.Max(0, x - 1), r = Math.Min(width, x + 2);
                    int t = Math.Max(0, y - 1), b = Math.Min(height, y + 2);
                    brightness[y * width + x] = (byte)(RectSum(source, width + 1, l, t, r, b) / ((r - l) * (b - t)));
                }
        }
        // Local contrast does not depend on the brightness threshold: evaluate it once per pass.
        int[] integral = BuildIntegral(brightness, width, height);
        bool[] contrasted = new bool[brightness.Length];
        int saturationLimit = suppressScreenPattern ? 140 : 110, contrastLimit = suppressScreenPattern ? 8 : 12;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (saturation[i] >= saturationLimit) continue;
                int l = Math.Max(0, x - 10), r = Math.Min(width, x + 11);
                int t = Math.Max(0, y - 10), b = Math.Min(height, y + 11);
                double background = RectSum(integral, width + 1, l, t, r, b) / (double)((r - l) * (b - t));
                contrasted[i] = brightness[i] - background > contrastLimit;
            }
        // Every threshold/slope pair is independent. Searching them on a few cores and then merging
        // in the original sequential order with the same strict comparison selects the identical strip.
        // At most four workers keeps a camera preview and UI responsive on modest phones.
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 1, 1, 4)
        };
        byte[][] masks = new byte[Thresholds.Length][];
        Parallel.For(0, Thresholds.Length, options, n =>
        {
            int threshold = Thresholds[n];
            byte[] mask = masks[n] = new byte[brightness.Length];
            for (int i = 0; i < mask.Length; i++)
                mask[i] = (byte)(contrasted[i] && brightness[i] >= threshold ? 1 : 0);
            if (suppressScreenPattern) ScreenPatternSuppression.RemoveSpeckles(mask, width, height);
        });

        var found = new Candidate?[Thresholds.Length * Slopes.Length];
        int maxPad = (int)Math.Ceiling(Slopes.Max(Math.Abs) * width);
        Parallel.For(0, found.Length, options,
            () => new Workspace(width * (height + 2 * maxPad), width),
            (index, _, workspace) =>
            {
                found[index] = SearchSlope(masks[index / Slopes.Length], width, height,
                    Slopes[index % Slopes.Length], suppressScreenPattern, workspace, cancellationToken);
                return workspace;
            },
            workspace => workspace.Dispose());
        cancellationToken.ThrowIfCancellationRequested();

        Candidate? best = null;
        for (int index = 0; index < found.Length; index++)
        {
            if (found[index] is not { } candidate || best is not null && candidate.Score >= best.Score) continue;
            best = candidate;
            diagnostic?.Invoke($"threshold={Thresholds[index / Slopes.Length]}, slope={Slopes[index % Slopes.Length]}, region={candidate.Region}: {new string(candidate.Matches.Select(m => m.Digit).ToArray())}, score={candidate.Score:F3}, certain={candidate.Matches.Count(m => m.IsCertain)}");
        }
        if (best is null)
            return new(null, [], NotFound);
        var glyphs = best.Matches;
        return new(new string(glyphs.Select(m => m.Digit).ToArray()), glyphs,
            glyphs.All(m => m.IsCertain) ? "Review the 12 glyphs before using this address." :
            "Check the marked positions carefully before using this address.") { Region = best.Region };
    }

    private static readonly int[] Thresholds = [150, 190, 220];
    private static readonly double[] Slopes = [0, 0.02, -0.02, 0.04, -0.04, 0.08, -0.08, 0.12, -0.12];

    private sealed record Candidate(GlyphMatch[] Matches, double Score, ScanRegion Region);

    /// <summary>
    /// Pooled per-worker buffers. A live camera scan otherwise allocated ~12 MB for each of up to
    /// 81 threshold/slope/pass searches per frame, which dominated phone frame time through GC.
    /// </summary>
    private sealed class Workspace(int length, int width) : IDisposable
    {
        public readonly byte[] Sheared = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
        public readonly bool[] Seen = System.Buffers.ArrayPool<bool>.Shared.Rent(length);
        public readonly int[] Prefix = System.Buffers.ArrayPool<int>.Shared.Rent(length + width);
        public readonly Queue<int> Queue = new();

        public void Dispose()
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(Sheared);
            System.Buffers.ArrayPool<bool>.Shared.Return(Seen);
            System.Buffers.ArrayPool<int>.Shared.Return(Prefix);
        }
    }

    private Candidate? SearchSlope(byte[] mask, int width, int height, double slope, bool suppressScreenPattern,
        Workspace workspace, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return null;
        byte[] sheared = workspace.Sheared;
        int shearedHeight = Shear(mask, width, height, slope, sheared);
        var bands = FindBands(sheared, width, shearedHeight, workspace.Seen, workspace.Queue);
        int[] columnPrefix = workspace.Prefix;
        ColumnPrefix(sheared, width, shearedHeight, columnPrefix);
        Candidate? best = null;
        foreach (var (top, bottom) in bands)
        {
            if (cancellationToken.IsCancellationRequested) return null;
            int h = bottom - top;
            var runs = FindRuns(columnPrefix, width, top, bottom,
                Math.Max(0, h / (suppressScreenPattern ? 7 : 18)));
            if (runs.Count < 10 || runs.Count > 160) continue;
            // Neighbouring wide horizon glyphs may meet after blur/downsampling.
            double medianWidth = runs.Select(r => r.Right - r.Left).Order().ElementAt(runs.Count / 2);
            runs = runs.SelectMany(r => r.Right - r.Left > medianWidth * 1.8 &&
                r.Right - r.Left < medianWidth * 2.8
                ? new[] { (r.Left, (r.Left + r.Right) / 2), ((r.Left + r.Right) / 2, r.Right) }
                : new[] { r }).ToList();
            // A run's match depends only on the run and band; overlapping 12-run windows reuse it
            // instead of re-describing each symbol up to twelve times. Null marks an unusable run.
            var matchCache = new GlyphMatch?[runs.Count];
            var matched = new bool[runs.Count];
            GlyphMatch? MatchRun(int index)
            {
                if (matched[index]) return matchCache[index];
                matched[index] = true;
                var points = Points(sheared, width, runs[index].Left, top, runs[index].Right, bottom);
                if (points.Count < 8 || points.Max(p => p.Y) - points.Min(p => p.Y) < h * 0.35) return null;
                var match = model.Match(GlyphShape.Describe(points));
                return matchCache[index] = match.Distance > 0.55 ? null : match;
            }
            for (int start = 0; start + 12 <= runs.Count; start++)
            {
                var strip = runs.Skip(start).Take(12).ToArray();
                double[] centers = strip.Select(r => (r.Left + r.Right) / 2.0).ToArray();
                double pitch = (centers[11] - centers[0]) / 11;
                if (pitch < h * 0.65 || pitch > h * 3 ||
                    centers.Skip(1).Select((v, i) => Math.Abs(v - centers[i] - pitch)).Max() > pitch * 0.30)
                    continue;
                GlyphMatch[] matches = new GlyphMatch[12];
                bool valid = true;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (MatchRun(start + slot) is not { } match) { valid = false; break; }
                    matches[slot] = match;
                }
                if (!valid) continue;
                if (!PortalScanQuality.CanReview(matches)) continue;
                double score = matches.Average(m => m.Distance) + matches.Max(m => m.Distance) * 0.25;
                if (best is null || score < best.Score)
                {
                    int pad = (int)Math.Ceiling(Math.Abs(slope) * width);
                    int shiftLeft = (int)Math.Round(slope * strip[0].Left) - pad;
                    int shiftRight = (int)Math.Round(slope * strip[11].Right) - pad;
                    best = new(matches, score, new ScanRegion(strip[0].Left, Math.Max(0, top + Math.Min(shiftLeft, shiftRight)),
                        strip[11].Right, Math.Min(height, bottom + Math.Max(shiftLeft, shiftRight))));
                }
            }
        }
        return best;
    }

    internal static List<(int X, int Y)> Points(byte[] mask, int width, int left, int top, int right, int bottom)
    {
        var points = new List<(int, int)>();
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (mask[y * width + x] != 0) points.Add((x, y));
        return points;
    }

    /// <summary>Shears <paramref name="mask"/> into the start of <paramref name="result"/>; returns the sheared height.</summary>
    private static int Shear(byte[] mask, int width, int height, double slope, byte[] result)
    {
        int pad = (int)Math.Ceiling(Math.Abs(slope) * width);
        int shearedHeight = height + 2 * pad;
        Array.Clear(result, 0, width * shearedHeight);
        for (int x = 0; x < width; x++)
        {
            int shift = pad - (int)Math.Round(slope * x);
            for (int y = 0; y < height; y++) result[(y + shift) * width + x] = mask[y * width + x];
        }
        return shearedHeight;
    }

    private static HashSet<(int Top, int Bottom)> FindBands(byte[] mask, int width, int height, bool[] seen, Queue<int> queue)
    {
        var bands = new HashSet<(int, int)>();
        int length = width * height;
        Array.Clear(seen, 0, length);
        queue.Clear();
        for (int i = 0; i < length; i++)
        {
            if (mask[i] == 0 || seen[i]) continue;
            int minX = i % width, maxX = minX, minY = i / width, maxY = minY, count = 0;
            queue.Enqueue(i); seen[i] = true;
            while (queue.TryDequeue(out int p))
            {
                count++; int x = p % width, y = p / width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int n = ny * width + nx;
                        if (mask[n] == 0 || seen[n]) continue;
                        seen[n] = true; queue.Enqueue(n);
                    }
            }
            int h = maxY - minY + 1, w = maxX - minX + 1;
            if (count < 8 || h is < 7 or > 140 || w < 3 || w > h * 4) continue;
            foreach (double scale in new[] { 1.05, 1.25, 1.5 })
            {
                int bandHeight = (int)(h * scale) + 2;
                int top = Math.Max(0, (minY + maxY - bandHeight) / 2);
                // Quantizing suppresses equivalent component-derived bands.
                top = top / 2 * 2;
                bands.Add((top, Math.Min(height, top + bandHeight / 2 * 2)));
            }
        }
        return bands;
    }

    private static List<(int Left, int Right)> FindRuns(int[] prefix, int width, int top, int bottom, int gap)
    {
        var runs = new List<(int, int)>();
        int start = -1, last = -1;
        for (int x = 0; x < width; x++)
        {
            bool active = prefix[bottom * width + x] > prefix[top * width + x];
            if (active) { if (start < 0) start = x; last = x; }
            if (start >= 0 && (!active && x - last > gap || x == width - 1))
            {
                if (last - start >= 2) runs.Add((start, last + 1));
                start = -1;
            }
        }
        return runs;
    }

    private static void ColumnPrefix(byte[] mask, int width, int height, int[] result)
    {
        Array.Clear(result, 0, width);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[(y + 1) * width + x] = result[y * width + x] + mask[y * width + x];
    }

    private static int[] BuildIntegral(byte[] pixels, int width, int height)
    {
        int stride = width + 1;
        int[] result = new int[stride * (height + 1)];
        for (int y = 0; y < height; y++)
        {
            int row = 0;
            for (int x = 0; x < width; x++)
            {
                row += pixels[y * width + x];
                result[(y + 1) * stride + x + 1] = result[y * stride + x + 1] + row;
            }
        }
        return result;
    }

    private static int RectSum(int[] values, int stride, int left, int top, int right, int bottom) =>
        values[bottom * stride + right] - values[top * stride + right] -
        values[bottom * stride + left] + values[top * stride + left];
}
