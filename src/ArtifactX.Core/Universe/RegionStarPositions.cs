namespace ArtifactX.Core.Universe;

/// <summary>A star the region generator emits: its system index and region-local position (about ±50).</summary>
public readonly record struct RegionStar(int System, float X, float Y, float Z);

/// <summary>
/// The generation-setup fields star placement reads (<c>GcGalaxyGlobals.DefaultGeneration</c>):
/// ConnectionAttractorMax/Min and ConnectionDistortion ranges (x, y), ConnectionDistortionTMult,
/// and InnerFieldScales.z.
/// </summary>
public sealed record GalaxyPlacementSettings(float AttractorMaxLow, float AttractorMaxHigh, float AttractorMinLow,
    float AttractorMinHigh, float DistortionLow, float DistortionHigh, float DistortionTMult, float InnerFieldZ);

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
    public static IReadOnlyList<RegionStar> FixedStarsOf(int galaxy, int x, int y, int z) => Fixed(galaxy, x, y, z).Stars;

    /// <summary>
    /// Every ordinary star of a region in emission order (systems 1..N), or null for a region the
    /// port does not cover yet: spiral regions (their spiral pull is not ported) and regions inside
    /// the goal gap (no stars).
    /// </summary>
    public static IReadOnlyList<RegionStar>? Ordinary(int galaxy, int x, int y, int z,
        GalaxyGenerationSettings settings, GalaxyPlacementSettings placement)
    {
        int sx = SignExtend(x, 12), sy = SignExtend(y, 8), sz = SignExtend(z, 12);
        if (RegionStarGeneration.InsideGoalGap(sx, sy, sz, settings.GoalGapVoxelDistance)) return null;
        var (fixedStars, rng, segments) = Fixed(galaxy, x, y, z);
        var stars = new List<RegionStar>(fixedStars);

        // Grid setup (0x141368262..0x1413685D4): eleven draws.
        bool spiral = rng.Next(100) < (int)settings.SpiralFormChance;
        if (spiral) return null;
        for (int i = 0; i < 5; i++) rng.Step();                      // direction, inclusion, two angles, [rbp-0x54]
        float attractorMax = Lerp(rng, placement.AttractorMaxLow, placement.AttractorMaxHigh);
        float attractorMin = Lerp(rng, placement.AttractorMinLow, placement.AttractorMinHigh);
        rng.Step(); rng.Step();                                        // base size, spiral flex
        float distortion = Lerp(rng, placement.DistortionLow, placement.DistortionHigh);

        float rx = sx * 0.1f, ry = sy * 0.1f, rz = sz * 0.1f;
        float scale = settings.BaseTurbulenceScale, fieldScale = placement.InnerFieldZ;
        for (int a = 0; a < Grid; a++)
            for (int b = 0; b < Grid; b++)
                for (int c = 0; c < Grid; c++)
                {
                    V p = new(a * CellSpacing + 0f + CellOffset, b * CellSpacing + 0f + CellOffset, c * CellSpacing + 0f + CellOffset);
                    float px = p.X + rx, py = ry + p.Y, pz = rz + p.Z;
                    float density = GalaxySimplexNoise.Fbm(px * scale, py * scale, pz * scale, 4,
                        settings.BaseTurbulenceLacunarity, settings.BaseTurbulenceGain);
                    if (!(density > settings.BaseGenerationThreshold) || !(density > settings.StarGenerationThreshold)) continue;

                    // Nearest point on the region's path segments (0x1413687D8..0x1413688E7).
                    float best = float.MaxValue, along = 0f;
                    V segStart = default, segEnd = default, nearest = default;
                    foreach (var s in segments)
                    {
                        V start = new(s.StartX, s.StartY, s.StartZ), end = new(s.EndX, s.EndY, s.EndZ);
                        V d = end - start, rel = p - start;
                        float t = Dot(rel, d);
                        V closest;
                        if (0f >= t) { closest = start; t = 0f; }
                        else
                        {
                            float l2 = Dot(d, d);
                            if (t >= l2) { closest = end; t = 1f; }
                            else { t /= l2; closest = start + d.Scale(t); }
                        }
                        V off = closest - p;
                        float dist = MathF.Sqrt(Dot(off, off));
                        if (best > dist) { best = dist; segStart = start; segEnd = end; nearest = closest; along = t; }
                    }

                    // Distorted attractor and pull (0x1413688ED..0x141368BA8).
                    V dir = segEnd - segStart;
                    V dirn = dir.Scale(1f / MathF.Sqrt(Dot(dir, dir)));
                    float n = GalaxySimplexNoise.Noise2(placement.DistortionTMult * along, rx);
                    V attractor = V.Cross(dirn, Up).Scale(n * distortion) + nearest;
                    V toStar = p - attractor;
                    float length = MathF.Sqrt(Dot(toStar, toStar));
                    float field = (GalaxySimplexNoise.Noise(-fieldScale * px, fieldScale * pz, -fieldScale * py, out _, out _, out _) + 1f) * 0.5f;
                    float pull = ((attractorMax - attractorMin) * field + attractorMin) * length;
                    V moved = p - toStar.Scale(1f / length).Scale(pull);
                    stars.Add(new RegionStar(stars.Count + 1, moved.X, moved.Y, moved.Z));
                }
        return stars;
    }

    private const int Grid = 19;
    private static readonly float CellOffset = BitConverter.Int32BitsToSingle(unchecked((int)0xC23D7943));
    private static readonly float CellSpacing = BitConverter.Int32BitsToSingle(0x40A86BCA);

    /// <summary>SSE dot of xyz: z + (y + x) of the lane products.</summary>
    private static float Dot(V a, V b) => a.Z * b.Z + (a.Y * b.Y + a.X * b.X);

    private static float Lerp(UniverseRandom rng, float low, float high) => RegionPathSegments.Unit(rng.NextWord()) * (high - low) + low;

    private static (List<RegionStar> Stars, UniverseRandom Rng, IReadOnlyList<RegionPathSegment> Segments) Fixed(int galaxy, int x, int y, int z)
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
        return (stars, rng, segments);
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
