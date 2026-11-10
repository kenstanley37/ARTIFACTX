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
    public PortalScanResult Recognize(SKBitmap image, CancellationToken cancellationToken = default)
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
        int[] integral = BuildIntegral(brightness, width, height);
        (GlyphMatch[] Matches, double Score)? best = null;
        foreach (int threshold in new[] { 150, 190, 220 })
        {
            byte[] mask = new byte[brightness.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    int l = Math.Max(0, x - 10), r = Math.Min(width, x + 11);
                    int t = Math.Max(0, y - 10), b = Math.Min(height, y + 11);
                    double background = RectSum(integral, width + 1, l, t, r, b) / (double)((r - l) * (b - t));
                    mask[i] = (byte)(brightness[i] >= threshold && saturation[i] < (suppressScreenPattern ? 140 : 110) &&
                        brightness[i] - background > (suppressScreenPattern ? 8 : 12) ? 1 : 0);
                }
            if (suppressScreenPattern) ScreenPatternSuppression.RemoveSpeckles(mask, width, height);
            foreach (double slope in new[] { 0, 0.02, -0.02, 0.04, -0.04, 0.08, -0.08, 0.12, -0.12 })
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sheared = Shear(mask, width, height, slope);
                int shearedHeight = sheared.Length / width;
                var bands = FindBands(sheared, width, shearedHeight);
                int[] columnPrefix = ColumnPrefix(sheared, width, shearedHeight);
                foreach (var (top, bottom) in bands)
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
                            var points = Points(sheared, width, strip[slot].Left, top, strip[slot].Right, bottom);
                            if (points.Count < 8 || points.Max(p => p.Y) - points.Min(p => p.Y) < h * 0.35)
                            { valid = false; break; }
                            matches[slot] = model.Match(GlyphShape.Describe(points));
                            if (matches[slot].Distance > 0.55) { valid = false; break; }
                        }
                        if (!valid) continue;
                        if (!PortalScanQuality.CanReview(matches)) continue;
                        double score = matches.Average(m => m.Distance) + matches.Max(m => m.Distance) * 0.25;
                        if (best is null || score < best.Value.Score)
                        {
                            best = (matches, score);
                            diagnostic?.Invoke($"threshold={threshold}, slope={slope}, band={top}..{bottom}, x={strip[0].Left}..{strip[11].Right}: {new string(matches.Select(m => m.Digit).ToArray())}, score={score:F3}, certain={matches.Count(m => m.IsCertain)}");
                        }
                    }
                }
            }
        }
        if (best is null)
            return new(null, [], "Could not read all 12 glyphs. Move closer, keep the strip level, and avoid glare. You can also enter the glyphs by hand.");
        var found = best.Value.Matches;
        return new(new string(found.Select(m => m.Digit).ToArray()), found,
            found.All(m => m.IsCertain) ? "Review the 12 glyphs before using this address." :
            "Check the marked positions carefully before using this address.");
    }

    internal static List<(int X, int Y)> Points(byte[] mask, int width, int left, int top, int right, int bottom)
    {
        var points = new List<(int, int)>();
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (mask[y * width + x] != 0) points.Add((x, y));
        return points;
    }

    private static byte[] Shear(byte[] mask, int width, int height, double slope)
    {
        int pad = (int)Math.Ceiling(Math.Abs(slope) * width);
        byte[] result = new byte[width * (height + 2 * pad)];
        for (int x = 0; x < width; x++)
        {
            int shift = pad - (int)Math.Round(slope * x);
            for (int y = 0; y < height; y++) result[(y + shift) * width + x] = mask[y * width + x];
        }
        return result;
    }

    private static HashSet<(int Top, int Bottom)> FindBands(byte[] mask, int width, int height)
    {
        var bands = new HashSet<(int, int)>();
        bool[] seen = new bool[mask.Length];
        var queue = new Queue<int>();
        for (int i = 0; i < mask.Length; i++)
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

    private static int[] ColumnPrefix(byte[] mask, int width, int height)
    {
        int[] result = new int[width * (height + 1)];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[(y + 1) * width + x] = result[y * width + x] + mask[y * width + x];
        return result;
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
