namespace ArtifactX.PortalScanning;

/// <summary>
/// Low-order shape statistics, not pixels or drawable glyph paths. Bounding-box normalization
/// tolerates differing display sizes. These describe camera input, not generated game content.
/// </summary>
public static class GlyphShape
{
    public const int FeatureCount = 21;

    public static double[] Describe(IReadOnlyList<(int X, int Y)> points)
    {
        if (points.Count < 4) throw new ArgumentException("Too few foreground pixels.", nameof(points));
        int left = points.Min(p => p.X), right = points.Max(p => p.X);
        int top = points.Min(p => p.Y), bottom = points.Max(p => p.Y);
        double[] result = new double[FeatureCount];
        result[0] = 0.3 * Math.Log((right - left + 1.0) / (bottom - top + 1.0));
        Span<double> px = stackalloc double[6], py = stackalloc double[6];
        foreach (var (x, y) in points)
        {
            Legendre(2.0 * (x - left) / Math.Max(1, right - left) - 1, px);
            Legendre(2.0 * (y - top) / Math.Max(1, bottom - top) - 1, py);
            int index = 1;
            for (int p = 0; p <= 5; p++)
                for (int q = 0; q <= 5 - p; q++)
                    if (p + q > 0) result[index++] += px[p] * py[q];
        }
        for (int i = 1; i < result.Length; i++) result[i] /= points.Count;
        return result;
    }

    private static void Legendre(double x, Span<double> values)
    {
        values[0] = 1; values[1] = x;
        for (int i = 2; i < values.Length; i++)
            values[i] = ((2 * i - 1) * x * values[i - 1] - (i - 1) * values[i - 2]) / i;
    }
}
