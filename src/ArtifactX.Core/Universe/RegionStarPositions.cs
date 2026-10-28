namespace ArtifactX.Core.Universe;

/// <summary>A star the region generator emits: its system index and region-local position (about ±50).</summary>
public readonly record struct RegionStar(int System, float X, float Y, float Z);

/// <summary>
/// Where the galaxy map places each star of a region (NMS build 180836 <c>0x141367810</c>),
/// as stored in the region slot the game reads. Work in progress: the 122 fixed stars
/// (guide stars, black hole, Atlas station) laid along the region's path segments.
/// Validated against the emulated generator, which matches the live game bit for bit
/// (docs/research/REGION_STAR_COUNT.md).
/// </summary>
public static class RegionStarPositions
{
    private const int FixedStars = 122;
    private const float NearStep = 5f, FarStep = 10f, PerpDistance = 5f; // voxel attributes 0x14132AD38..0x14132AD4A

    private readonly record struct V(float X, float Y, float Z)
    {
        public static V operator -(V a, V b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public V Scale(float s) => new(s * X, s * Y, s * Z);
        /// <summary>SSE dot of xyz: z² + (y² + x²).</summary>
        public float LengthSquared => Z * Z + (Y * Y + X * X);
        /// <summary>The game's cross: a.yzx * b.zxy - a.zxy * b.yzx.</summary>
        public static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    private static readonly V Up = new(0f, 1f, 0f), Forward = new(0f, 0f, 1f);

    /// <summary>The fixed stars of a region, in emission order (systems 1..122).</summary>
    public static IReadOnlyList<RegionStar> FixedStarsOf(int galaxy, int x, int y, int z)
    {
        int sx = SignExtend(x, 12), sy = SignExtend(y, 8), sz = SignExtend(z, 12);
        uint lo = (uint)((y & 0xFF) << 24 | (z & 0xFFF) << 12 | (x & 0xFFF));
        uint hi = (uint)(galaxy & 0xFF);
        var rng = new UniverseRandom(((ulong)(RegionPathSegments.BitRotateRight16(lo) ^ hi ^ lo) << 32) | (lo == 0 ? 1u : lo));
        rng.Step(); // setup draw (0x141367C66)

        var segments = RegionPathSegments.Build(galaxy, sx, sy, sz);
        var stars = new List<RegionStar>();
        foreach (var s in segments)
        {
            V start = new(s.StartX, s.StartY, s.StartZ), end = new(s.EndX, s.EndY, s.EndZ);
            var (dir, length) = Direction(end - start);
            var (a, b) = Basis(dir);
            if (length == 0f) continue;
            float stepMin = 1f / (length / NearStep), stepMax = 1f / (length / FarStep);
            if (1f < stepMin) continue;
            float span = stepMax - stepMin, t = stepMin;
            do
            {
                stars.Add(Jitter(rng, PointOn(start, end, t), a, b, stars.Count + 1));
                t += RegionPathSegments.Unit(rng.NextWord()) * span + stepMin;
            } while (1f >= t);
        }
        while (stars.Count < FixedStars)
        {
            var s = segments[(int)(((ulong)rng.NextWord() * (ulong)segments.Count) >> 32)];
            float t = RegionPathSegments.Unit(rng.NextWord());
            V start = new(s.StartX, s.StartY, s.StartZ), end = new(s.EndX, s.EndY, s.EndZ);
            var (dir, _) = Direction(end - start);
            var (a, b) = Basis(dir);
            stars.Add(Jitter(rng, PointOn(start, end, t), a, b, stars.Count + 1));
        }
        return stars;
    }

    /// <summary>Normalised by multiplying with 1/length, as the SSE code does; zero length stays as is.</summary>
    private static (V Dir, float Length) Direction(V d)
    {
        float lengthSquared = d.LengthSquared;
        if (lengthSquared == 0f) return (d, 0f);
        float length = MathF.Sqrt(lengthSquared);
        return (d.Scale(1f / length), length);
    }

    /// <summary>Two vectors across the segment (0x141367EA5..0x141367F2E): A = d x up, B = d x A.</summary>
    private static (V A, V B) Basis(V dir)
    {
        V a = V.Cross(dir, Up);
        if (0f > a.LengthSquared) a = V.Cross(dir, Forward);
        return (a, V.Cross(dir, a));
    }

    private static V PointOn(V start, V end, float t)
    {
        if (0f >= t) return start;
        if (t >= 1f) return end;
        var (dir, length) = Direction(end - start);
        return start + dir.Scale(length * t);
    }

    /// <summary>0x14136A020: offset across the segment, then two size draws (5 draws in all).</summary>
    private static RegionStar Jitter(UniverseRandom rng, V point, V a, V b, int system)
    {
        float r1 = RegionPathSegments.Unit(rng.NextWord()) * PerpDistance + 0f;
        float r2 = PerpDistance - r1;
        if ((rng.NextWord() & 1) != 0) r1 *= -1f;
        if ((rng.NextWord() & 1) != 0) r2 *= -1f;
        rng.Step(); rng.Step(); // star size fields
        V p = a.Scale(r1) + point + b.Scale(r2);
        return new RegionStar(system, p.X, p.Y, p.Z);
    }

    private static int SignExtend(int value, int bits) => (value << (32 - bits)) >> (32 - bits);
}
