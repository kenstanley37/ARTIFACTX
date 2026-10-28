namespace ArtifactX.Core.Universe;

/// <summary>
/// The game's own float sine and cosine (NMS build 180836: cosine <c>0x140206D80</c>, sine
/// <c>0x140206E60</c>), which the region generator uses for its spiral rotations. Both reduce the
/// angle to [-pi, pi] with a truncated multiple of 2pi, fold it into [-pi/2, pi/2] and evaluate a short
/// polynomial with the constants at <c>0x144B271B8..0x144B28680</c>. Ported operation for operation so
/// the results match bit for bit; MathF.Sin/Cos differ in the last bits.
/// </summary>
public static class GameTrig
{
    private static readonly float Pi = Bits(0x40490FDB), TwoPi = Bits(0x40C90FDB), HalfPi = Bits(0x3FC90FDB);
    private static readonly float InverseTwoPi = Bits(0x3E22F983);
    private static readonly float C0 = Bits(0x348BDD11), C1 = Bits(0x37CFB4C2), C2 = Bits(0x3AB609AA), C3 = Bits(0x3D2AAAA3);
    private static readonly float S0 = Bits(0x32CCAF86), S1 = Bits(0x3638B1AD), S2 = Bits(0x39500BB2), S3 = Bits(0x3C088885),
        S4 = Bits(0x3E2AAAAB);

    public static float Cos(float x)
    {
        float r = Reduce(x), sign = 1f;
        if (r > HalfPi) { r = Pi - r; sign = -1f; }
        else if (-HalfPi > r) { r = -Pi - r; sign = -1f; }
        float y = r * r;
        float p = C1 - y * C0;
        p = p * y - C2;
        p = p * y + C3;
        p = p * y - 0.5f;
        p = p * y + 1f;
        return p * sign;
    }

    public static float Sin(float x)
    {
        float r = Reduce(x);
        if (r > HalfPi) r = Pi - r;
        else if (-HalfPi > r) r = -Pi - r;
        float y = r * r;
        float p = S1 - y * S0;
        p = p * y - S2;
        p = p * y + S3;
        p = p * y - S4;
        p = p * y + 1f;
        return p * r;
    }

    /// <summary>x - trunc(x / 2pi +- 0.5) * 2pi, with the 64-bit truncation (cvttss2si rax).</summary>
    private static float Reduce(float x)
    {
        float q = x * InverseTwoPi;
        q = x < 0f || float.IsNaN(x) ? q - 0.5f : q + 0.5f;
        long k = float.IsNaN(q) || q >= 9.223372E+18f || q < -9.223372E+18f ? long.MinValue : (long)q;
        return x - (float)k * TwoPi;
    }

    private static float Bits(int bits) => BitConverter.Int32BitsToSingle(bits);
}
