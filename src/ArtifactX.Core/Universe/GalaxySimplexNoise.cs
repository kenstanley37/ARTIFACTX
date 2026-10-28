namespace ArtifactX.Core.Universe;

/// <summary>
/// The galaxy map's 3D simplex noise with analytic derivatives (NMS build 180836
/// <c>0x142D75250</c>) and the derivative-damped fBm built on it (<c>0x142D746E0</c>).
/// Ported operation by operation from the SSE code so every float matches: the corner
/// ranking is the game's mask network, and sums keep the game's pairwise order.
/// The permutation is Ken Perlin's reference table (the exe stores it twice at
/// <c>0x14501D440</c>); the 16 gradients are written at startup by <c>0x1401997A0</c>
/// into <c>0x1459245E0</c>. Both are engine constants, not game-file data; the research
/// script scripts/research/region-generator/noisetables.py dumps them for comparison.
/// </summary>
public static class GalaxySimplexNoise
{
    private const float F3 = 1f / 3f, G3 = 1f / 6f;

    private static readonly int[] Perm = BuildPermutation();

    private static readonly float[] Gradients =
    {
        1, 0, 1, 0,   0, 1, 1, 0,   -1, 0, 1, 0,   0, -1, 1, 0,
        1, 0, -1, 0,  0, 1, -1, 0,  -1, 0, -1, 0,  0, -1, -1, 0,
        1, -1, 0, 0,  1, 1, 0, 0,   -1, 1, 0, 0,   -1, -1, 0, 0,
        1, 0, 1, 0,   -1, 0, 1, 0,  0, 1, -1, 0,   0, -1, -1, 0,
    };

    private static int[] BuildPermutation()
    {
        int[] p =
        {
            151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37,
            240, 21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177,
            33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146,
            158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25,
            63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100,
            109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206,
            59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153,
            101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246,
            97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49, 192,
            214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114,
            67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180,
        };
        var doubled = new int[512];
        for (int i = 0; i < 512; i++) doubled[i] = p[i & 255];
        return doubled;
    }

    /// <summary>Noise value and its gradient (dx, dy, dz) at a point.</summary>
    public static float Noise(float x, float y, float z, out float dx, out float dy, out float dz)
    {
        // Skew: s = ((y + x) + z) * F3, floor per lane (truncate, then -1 where above).
        float s = ((y + x) + z) * F3;
        float sx = x + s, sy = y + s, sz = z + s, sw = 0f + s;
        float fi = Floor(sx), fj = Floor(sy), fk = Floor(sz), fl = Floor(sw);
        float t = ((fj + fi) + fk) * G3;
        float x0 = x - (fi - t), y0 = y - (fj - t), z0 = z - (fk - t), w0 = 0f - (fl - t);
        int i = (int)fi, j = (int)fj, k = (int)fk;

        // Corner ranking: the SSE mask network at 0x142D7531A..0x142D75411, lane for lane.
        int Mask(bool b) => b ? -1 : 0;
        int[] a4 = { 0, 0, 1, 0 }, a14 = { 1, 1, 0, 0 };
        int mLtXZ = Mask(x0 < z0), mLeZX = Mask(z0 <= x0), mLeYX = Mask(y0 <= x0);
        var m5 = new[] { Mask(z0 <= y0), Mask(z0 <= y0), Mask(z0 <= y0), Mask(y0 <= x0) };
        var m7 = new[] { Mask(y0 < z0), Mask(y0 < z0), Mask(y0 < z0), Mask(x0 < y0) };
        int[] one0 = { 1, 0, 0, 0 }, c0110 = { 0, 1, 1, 0 }, c0100 = { 0, 1, 0, 0 }, c1010 = { 1, 0, 1, 0 };
        var i1 = new int[4];
        var i2 = new int[4];
        for (int l = 0; l < 4; l++)
        {
            int m9 = mLeZX & c1010[l] ^ a4[l];
            int x0m = (c0100[l] ^ a4[l]) & m7[l] ^ c0100[l];
            int m6 = mLtXZ & (c0110[l] ^ a14[l]) ^ a14[l];
            int x1m = (m6 ^ c0110[l]) & m7[l] ^ m6;
            int v6 = (m9 ^ one0[l]) & m5[l] ^ m9 ^ x0m;
            v6 = v6 & mLeYX ^ x0m;
            int v14 = (a14[l] ^ c1010[l]) & m5[l] ^ c1010[l] ^ x1m;
            v14 = v14 & mLeYX ^ x1m;
            i1[l] = v6; i2[l] = v14;
        }

        // Corner offsets (x - rank + G3, x - rank2 + 2*G3, x - 1 + 0.5).
        float[] X = { x0, x0 - i1[0] + G3, x0 - i2[0] + 2f * G3, x0 - 1f + 0.5f };
        float[] Y = { y0, y0 - i1[1] + G3, y0 - i2[1] + 2f * G3, y0 - 1f + 0.5f };
        float[] Z = { z0, z0 - i1[2] + G3, z0 - i2[2] + 2f * G3, z0 - 1f + 0.5f };

        int ii = i & 255, jj = j & 255, kk = k & 255;
        int[] g =
        {
            Perm[Perm[Perm[kk] + jj] + ii] & 15,
            Perm[i1[0] + Perm[i1[1] + Perm[kk + i1[2]] + jj] + ii] & 15,
            Perm[i2[0] + Perm[i2[1] + Perm[kk + i2[2]] + jj] + ii] & 15,
            Perm[Perm[Perm[kk + 1] + jj + 1] + ii + 1] & 15,
        };

        var t4 = new float[4];
        var t2 = new float[4];
        var tt = new float[4];
        var n = new float[4];
        var dot = new float[4];
        for (int c = 0; c < 4; c++)
        {
            float tc = 0.6f - X[c] * X[c] - Y[c] * Y[c] - Z[c] * Z[c];
            tc = MathF.Max(tc, 0f);
            tt[c] = tc;
            t2[c] = tc * tc;
            t4[c] = t2[c] * t2[c];
            float gx = Gradients[g[c] * 4], gy = Gradients[g[c] * 4 + 1], gz = Gradients[g[c] * 4 + 2];
            dot[c] = gx * X[c] + gy * Y[c] + gz * Z[c];
            n[c] = dot[c] * t4[c];
        }
        float noise = HorizontalSum(n) * 28f;

        var ax = new float[4];
        var ay = new float[4];
        var az = new float[4];
        var gxt = new float[4];
        var gyt = new float[4];
        var gzt = new float[4];
        for (int c = 0; c < 4; c++)
        {
            float t3 = t2[c] * tt[c];
            float d = dot[c] * t3;
            ax[c] = d * X[c]; ay[c] = d * Y[c]; az[c] = d * Z[c];
            gxt[c] = Gradients[g[c] * 4] * t4[c];
            gyt[c] = Gradients[g[c] * 4 + 1] * t4[c];
            gzt[c] = Gradients[g[c] * 4 + 2] * t4[c];
        }
        dx = (HorizontalSum(ax) * -8f + HorizontalSum(gxt)) * 28f;
        dy = (HorizontalSum(ay) * -8f + HorizontalSum(gyt)) * 28f;
        dz = (HorizontalSum(az) * -8f + HorizontalSum(gzt)) * 28f;
        return noise;
    }

    /// <summary>
    /// Derivative-damped fBm (0x142D746E0): starts at 0.5; each octave adds
    /// noise * amplitude / (1 + |accumulated derivative|²), then frequency *= lacunarity
    /// and amplitude *= gain.
    /// </summary>
    public static float Fbm(float x, float y, float z, int octaves, float lacunarity, float gain)
    {
        float sum = 0.5f, frequency = 1f, amplitude = 1f;
        float ax = 0f, ay = 0f, az = 0f;
        for (int o = 0; o < octaves; o++)
        {
            float n = Noise(frequency * x, frequency * y, frequency * z, out float dx, out float dy, out float dz);
            ax = dx + ax; ay = dy + ay; az = dz + az;
            float weighted = n * amplitude;
            frequency *= lacunarity;
            amplitude *= gain;
            float damping = az * az + (ay * ay + ax * ax) + 1f;
            sum += weighted / damping;
        }
        return sum;
    }

    private static readonly float[] Gradients2 = { -1, -1, 1, 0, -1, 0, 1, 1, -1, 1, 0, -1, 0, 1, 1, -1 }; // 0x14501DC40
    private static readonly float F2 = BitConverter.Int32BitsToSingle(0x3EBB67AE);      // (sqrt(3)-1)/2 at 0x14351AC68
    private static readonly float G2 = BitConverter.Int32BitsToSingle(0x3E58658C);      // (3-sqrt(3))/6 at 0x1435332A4
    private static readonly float TwoG2 = BitConverter.Int32BitsToSingle(0x3ED8658C);   // at 0x1435332D0

    /// <summary>
    /// 2D simplex noise value (0x142D74870 called through 0x142D74860, no derivative outputs):
    /// Gustavson's scalar algorithm with the "fast floor", 8 gradients and a 40x scale.
    /// </summary>
    public static float Noise2(float x, float y)
    {
        float s = (x + y) * F2;
        float xs = s + x, ys = s + y;
        int i = xs <= 0f ? (int)xs - 1 : (int)xs;
        int j = ys <= 0f ? (int)ys - 1 : (int)ys;
        float t = (j + i) * G2;
        float x0 = x - (i - t), y0 = y - (j - t);
        int i1 = x0 > y0 ? 1 : 0, j1 = x0 > y0 ? 0 : 1;
        float x1 = x0 - i1 + G2, y1 = y0 - j1 + G2;
        float x2 = x0 - 1f + TwoG2, y2 = y0 - 1f + TwoG2;
        int ii = i & 255, jj = j & 255;

        float n0 = 0f, n1 = 0f, n2 = 0f;
        float t0 = 0.5f - x0 * x0 - y0 * y0;
        if (!(0f > t0))
        {
            int g = (Perm[Perm[jj] + ii] & 7) * 2;
            float t2 = t0 * t0;
            n0 = (Gradients2[g + 1] * y0 + Gradients2[g] * x0) * (t2 * t2);
        }
        float t1 = 0.5f - x1 * x1 - y1 * y1;
        if (!(0f > t1))
        {
            int g = (Perm[Perm[j1 + jj] + i1 + ii] & 7) * 2;
            float t2 = t1 * t1;
            n1 = (Gradients2[g + 1] * y1 + Gradients2[g] * x1) * (t2 * t2);
        }
        float t3 = 0.5f - x2 * x2 - y2 * y2;
        if (!(0f > t3))
        {
            int g = (Perm[Perm[jj + 1] + 1 + ii] & 7) * 2;
            float t2 = t3 * t3;
            n2 = (Gradients2[g + 1] * y2 + Gradients2[g] * x2) * (t2 * t2);
        }
        return ((n1 + n0) + n2) * 40f;
    }

    /// <summary>The SSE horizontal sum: (v3 + v2) + (v1 + v0).</summary>
    private static float HorizontalSum(float[] v) => (v[3] + v[2]) + (v[1] + v[0]);

    private static float Floor(float v)
    {
        float truncated = (int)v;
        return v < truncated ? truncated - 1f : truncated;
    }
}
