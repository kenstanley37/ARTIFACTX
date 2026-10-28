using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>The noise settings of one terrain layer (TkNoiseUberData) the uber noise reads.</summary>
public readonly record struct UberNoiseSettings(float AltitudeErosion, float AmplifyFeatures, float Gain, float Lacunarity,
    float PerturbFeatures, float RemapFromMax, float RemapFromMin, float RemapToMax, float RemapToMin, float RidgeErosion,
    float SharpToRoundFeatures, float SlopeBias, float SlopeErosion, float SlopeGain);

/// <summary>
/// The terrain noise of NMS.exe (build 183791, docs/research/PLANET_BUILDING_PLACEMENT.md), float32 operation for
/// operation so results are bit-exact: the simplex noise with derivatives inlined at 0x1419BCC50, the scalar uber noise
/// 0x1419BCA30 and the scalar path of the simplex fBm 0x1419BBAE0 (up to 3 octaves; the terrain height and the site
/// query never use more). The permutation is Ken Perlin's published one; the gradients and constants are the game's
/// (read from NMS.exe: 0x144AFAEA0, 0x144AFADA0, 0x144AFAFA0..0x144AFAFD0, 0x144BE9418).
/// Checked against the game's code run in an emulator: uber noise 500/500, fBm 88/88 bit-exact (Python port), and
/// this C# port against that Python port.
/// </summary>
internal static class TerrainNoise
{
    private static readonly byte[] Perm =
    {
        151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240,
        21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88,
        237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83,
        111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216,
        80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186,
        3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17,
        182, 189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129,
        22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238,
        210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184,
        84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195,
        78, 66, 215, 61, 156, 180,
    };

    // Gradients (lane 0 unused, x/y/z in lanes 1-3), the 12 cube edges and 4 repeats.
    private static readonly Vector4[] Grad =
    {
        new(0, 1, 0, 1), new(0, 0, 1, 1), new(0, -1, 0, 1), new(0, 0, -1, 1),
        new(0, 1, 0, -1), new(0, 0, 1, -1), new(0, -1, 0, -1), new(0, 0, -1, -1),
        new(0, 1, -1, 0), new(0, 1, 1, 0), new(0, -1, 1, 0), new(0, -1, -1, 0),
        new(0, 1, 0, 1), new(0, -1, 0, 1), new(0, 0, 1, -1), new(0, 0, -1, -1),
    };

    private static readonly float F3 = Bits(0x3EAAAAAB), G3 = Bits(0x3E2AAAAB);
    private static readonly Vector4 V1 = new(0, G3, G3, G3), V2 = new(0, F3, F3, F3), V3 = new(0, -0.5f, -0.5f, -0.5f);
    private static readonly Vector4 VR = new(Bits(0x3F19999A)), VD = new(-8f), VS = new(28f);
    private static readonly float SecondOctave = Bits(0x3F666666), SlopeAccent = Bits(0x3F4CCCCD), RidgeFlat = Bits(0x3F19999A);

    /// <summary>fBm normalisation by octave count (0x144BE9418).</summary>
    private static readonly float[] FbmNorm = { 0f, 1f, Bits(0x3F2AAAAB), Bits(0x3F124925) };

    /// <summary>The scalar uber noise at a point (0x1419BCA30).</summary>
    public static float Uber(float px, float py, float pz, int octaves, in UberNoiseSettings a)
    {
        float sharp = MathF.Sign(a.SharpToRoundFeatures) * MathF.Floor(MathF.Abs(a.SharpToRoundFeatures) + 0.5f);
        float sharpPos = Max(0f, sharp), sharpNeg = Min(0f, sharp);
        float freq = 1f, amp = octaves == 1 ? 1f : SecondOctave, ridgeAmp = amp;
        float sum = 0f;
        float ax = 0f, ay = 0f, az = 0f, bx = 0f, by = 0f, bz = 0f, ox = 0f, oy = 0f, oz = 0f;
        float gainTerm = a.AmplifyFeatures + a.Gain;
        for (int o = 0; o < octaves; o++)
        {
            float y = py * freq + oy, x = px * freq + ox, z = pz * freq + oz;
            float n = Simplex(x, y, z, out var d);
            float n1 = (n * n - n) * sharpPos + n;
            float ndx = n * d.Y, ndy = n * d.Z, ndz = n * d.W;
            ax += ndx * a.SlopeErosion; ay += ndy * a.SlopeErosion; az += ndz * a.SlopeErosion;
            bx += ndx * SlopeAccent; by += ndy * SlopeAccent; bz += ndz * SlopeAccent;
            ox += ndx * a.PerturbFeatures; oy += ndy * a.PerturbFeatures; oz += ndz * a.PerturbFeatures;
            float slopeA = ay * ay + ax * ax + az * az + 1f;
            float n2 = ((1f - MathF.Abs(n)) * RidgeFlat - n1) * MathF.Abs(sharpNeg) + n1;
            freq *= a.Lacunarity;
            float contribution = 1f / slopeA * (n2 * ridgeAmp);
            sum += contribution;
            float c = Min(1f, Max(0f, sum - 0f));
            float smooth = (3f - (c + c)) * (c * c);
            float g = (smooth * gainTerm - gainTerm) * a.AltitudeErosion + gainTerm;
            gainTerm = a.Gain;
            amp *= g;
            float slopeB = by * by + bx * bx + bz * bz + 1f;
            ridgeAmp = (1f - a.RidgeErosion / slopeB) * amp;
        }
        float v = sum;
        if (sharp == 0f) v = (v + 1f) * 0.5f;
        v = (v - a.RemapFromMin) / (a.RemapFromMax - a.RemapFromMin);
        v = Min(1f, Max(0f, v));
        v = v * (a.RemapToMax - a.RemapToMin) + a.RemapToMin;
        float v2 = v + v, r;
        if (0.5f > v) r = v2 / ((1f / a.SlopeGain - 2f) * (1f - v2) + 1f) * 0.5f;
        else
        {
            float t = v2 - 1f;
            r = t / ((1f / (1f - a.SlopeGain) - 2f) * (1f - t) + 1f) * 0.5f + 0.5f;
        }
        return r / ((1f / a.SlopeBias - 2f) * (1f - r) + 1f);
    }

    /// <summary>The scalar simplex fBm (0x1419BBAE0), 1 to 3 octaves.</summary>
    public static float Fbm(float px, float py, float pz, int octaves)
    {
        if (octaves is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(octaves));
        float freq = 1f, amp = 1f, total = 0f;
        for (int o = 0; o < octaves; o++)
        {
            total += Simplex(px * freq, py * freq, pz * freq, out _) * amp;
            amp *= 0.5f;
            freq += freq;
        }
        return total * FbmNorm[octaves];
    }

    /// <summary>Simplex noise with its derivatives (lanes 1-3 of <paramref name="derivatives"/>).</summary>
    private static float Simplex(float x, float y, float z, out Vector4 derivatives)
    {
        float s = (y + x + z) * F3;
        int i = TruncFloor(s + x), j = TruncFloor(s + y), k = TruncFloor(s + z);
        float t = (k + j + i) * G3;
        float x0 = x - (i - t), y0 = y - (j - t), z0 = z - (k - t);
        int sil = x0 >= y0 ? 1 : 0, dil = y0 >= z0 ? 1 : 0, bl = z0 >= x0 ? 1 : 0;
        int r11 = sil & (bl ^ 1), r10 = dil & (sil ^ 1), r9 = bl & (dil ^ 1);
        int esi = sil | (bl ^ 1), edi = dil | (sil ^ 1), ebx = bl | (dil ^ 1);
        int kk = k & 255;
        int g0 = Perm[(Perm[(Perm[kk] + j) & 255] + i) & 255] & 15;
        int g1 = Perm[(Perm[(Perm[(kk + r9) & 255] + r10 + j) & 255] + r11 + i) & 255] & 15;
        int g2 = Perm[(Perm[(Perm[(kk + ebx) & 255] + edi + j) & 255] + esi + i) & 255] & 15;
        int g3 = Perm[(Perm[(Perm[(kk + 1) & 255] + j + 1) & 255] + i + 1) & 255] & 15;
        var c0 = new Vector4(0, x0, y0, z0);
        var c1 = (c0 - new Vector4(0, r11, r10, r9)) + V1;
        var c2 = (c0 - new Vector4(0, esi, edi, ebx)) + V2;
        var c3 = c0 + V3;
        var d2 = Transpose(c3 * c3, c1 * c1, c2 * c2, c0 * c0);          // lanes c3, c2, c1, c0
        var tt = Vector4.Max(Vector4.Zero, VR - d2);
        var t2 = tt * tt;
        var t4 = t2 * t2;
        var dots = Transpose(c3 * Grad[g3], c1 * Grad[g1], c2 * Grad[g2], c0 * Grad[g0]);
        var n = dots * t4;
        var dt3 = (dots * tt) * t2;
        float noise = ((n.Y + n.X) + (n.W + n.Z)) * 28f;
        var dv = (((new Vector4(dt3.Z) * c1 + new Vector4(dt3.W) * c0) + new Vector4(dt3.Y) * c2) + new Vector4(dt3.X) * c3) * VD;
        var gv = ((new Vector4(t4.W) * Grad[g0] + new Vector4(t4.Z) * Grad[g1]) + new Vector4(t4.Y) * Grad[g2]) + new Vector4(t4.X) * Grad[g3];
        derivatives = (dv + gv) * VS;
        return noise;
    }

    /// <summary>The transpose-add network: per input (l0 + l2) + (l1 + l3), results in order A, C, B, D.</summary>
    private static Vector4 Transpose(Vector4 a, Vector4 b, Vector4 c, Vector4 d) =>
        new(Sum(a), Sum(c), Sum(b), Sum(d));

    private static float Sum(Vector4 v) => (v.X + v.Z) + (v.Y + v.W);

    private static int TruncFloor(float v)
    {
        int i = (int)v;
        return i - (i > v ? 1 : 0);
    }

    /// <summary>max / min as the game (and the port) pick them: the first argument unless the second is larger / smaller.</summary>
    internal static float Max(float a, float b) => b > a ? b : a;
    internal static float Min(float a, float b) => b < a ? b : a;

    internal static float Bits(uint bits) => BitConverter.UInt32BitsToSingle(bits);
}
