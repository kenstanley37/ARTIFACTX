namespace ArtifactX.Core.Universe;

/// <summary>
/// An affine transform in double precision (column vectors: p' = R p + T), as the building layout port composes its
/// node transforms. M holds the 3x3 rotation-scale rows, then the translation (the <see cref="LayoutLocator"/> order).
/// </summary>
public readonly struct LayoutTransform
{
    private readonly double[] m;

    public LayoutTransform(double[] values) => m = values;

    public static LayoutTransform Identity => new([1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]);

    public double this[int i] => m[i];

    /// <summary>The translation (the point the transform takes the origin to).</summary>
    public (double X, double Y, double Z) Position => (m[9], m[10], m[11]);

    /// <summary>this * other: other applied first.</summary>
    public LayoutTransform Then(LayoutTransform o)
    {
        var r = new double[12];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                r[i * 3 + j] = m[i * 3] * o.m[j] + m[i * 3 + 1] * o.m[3 + j] + m[i * 3 + 2] * o.m[6 + j];
            r[9 + i] = m[i * 3] * o.m[9] + m[i * 3 + 1] * o.m[10] + m[i * 3 + 2] * o.m[11] + m[9 + i];
        }
        return new(r);
    }

    public (double X, double Y, double Z) Apply(double x, double y, double z) => (
        m[0] * x + m[1] * y + m[2] * z + m[9],
        m[3] * x + m[4] * y + m[5] * z + m[10],
        m[6] * x + m[7] * y + m[8] * z + m[11]);

    public LayoutTransform Inverse()
    {
        double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], k = m[8];
        double c00 = e * k - f * h, c01 = c * h - b * k, c02 = b * f - c * e;
        double det = a * c00 + d * c01 + g * c02;
        var r = new double[12]
        {
            c00 / det, c01 / det, c02 / det,
            (f * g - d * k) / det, (a * k - c * g) / det, (c * d - a * f) / det,
            (d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det, 0, 0, 0,
        };
        for (int i = 0; i < 3; i++)
            r[9 + i] = -(r[i * 3] * m[9] + r[i * 3 + 1] * m[10] + r[i * 3 + 2] * m[11]);
        return new(r);
    }

    /// <summary>A scene node's local transform: translation * Rx * Ry * Rz * scale, angles in degrees.</summary>
    public static LayoutTransform FromScene(double tx, double ty, double tz, double rx, double ry, double rz, double sx, double sy, double sz)
    {
        rx *= Math.PI / 180; ry *= Math.PI / 180; rz *= Math.PI / 180;
        double cx = Math.Cos(rx), sxn = Math.Sin(rx), cy = Math.Cos(ry), syn = Math.Sin(ry), cz = Math.Cos(rz), szn = Math.Sin(rz);
        // Rx * Ry * Rz
        double r00 = cy * cz, r01 = -cy * szn, r02 = syn;
        double r10 = sxn * syn * cz + cx * szn, r11 = -sxn * syn * szn + cx * cz, r12 = -sxn * cy;
        double r20 = -cx * syn * cz + sxn * szn, r21 = cx * syn * szn + sxn * cz, r22 = cx * cy;
        return new([r00 * sx, r01 * sy, r02 * sz, r10 * sx, r11 * sy, r12 * sz, r20 * sx, r21 * sy, r22 * sz, tx, ty, tz]);
    }
}
