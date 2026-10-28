namespace ArtifactX.Core.Universe;

/// <summary>A star the region generator emits: its system index and region-local position (about ±50).</summary>
public readonly record struct RegionStar(int System, float X, float Y, float Z);

/// <summary>
/// The generation-setup fields star placement reads (<c>GcGalaxyGlobals.DefaultGeneration</c>):
/// ConnectionAttractorMax/Min and ConnectionDistortion ranges (x, y), ConnectionDistortionTMult,
/// and InnerFieldScales.z; for spiral regions also SpiralInclusion, SpiralFlex, SpiralTwistMult, SpiralPull and
/// InnerFieldScales.y (catalog format 46; NaN in older catalogs, which then leave spiral regions unplaced).
/// </summary>
public sealed record GalaxyPlacementSettings(float AttractorMaxLow, float AttractorMaxHigh, float AttractorMinLow,
    float AttractorMinHigh, float DistortionLow, float DistortionHigh, float DistortionTMult, float InnerFieldZ,
    float SpiralInclusionLow = float.NaN, float SpiralInclusionHigh = float.NaN, float SpiralFlexLow = float.NaN,
    float SpiralFlexHigh = float.NaN, float SpiralTwistMult = float.NaN, float SpiralPullX = float.NaN,
    float SpiralPullY = float.NaN, float SpiralPullZ = float.NaN, float InnerFieldY = float.NaN)
{
    public bool HasSpiral => !float.IsNaN(SpiralTwistMult);
}

/// <summary>
/// Where the galaxy map places each star of a region (NMS build 180836 <c>0x141367810</c>),
/// as stored in the region slot the game reads: the 122 fixed stars (guide stars, black hole, Atlas
/// station) along the region's path segments, the density-grid stars pulled toward those segments (and,
/// in spiral regions, toward three spiral arms), and the 65 purple systems. Bit-exact against the
/// emulated generator, which matches the live game bit for bit: 604/604 stars read live from Noturbo
/// Spur, plus 4 ordinary and 8 spiral regions across 5 galaxies emulated (2026-10-27;
/// docs/research/REGION_STAR_COUNT.md).
/// </summary>
public static class RegionStarPositions
{
    /// <summary>
    /// Light-years per region unit. Confirmed 2026-10-27 on the galaxy map: from Hocoss-Logar (Noturbo Spur)
    /// the stars 9.61, 10.77, 10.98, 11.04 and 12.18 units away read 38, 43, 43, 44 and 48 ly, which is
    /// units × 4 rounded down for all five.
    /// </summary>
    public const float LightYearsPerUnit = 4f;

    /// <summary>Straight-line distance between two stars of a region, in region units.</summary>
    public static float Distance(RegionStar a, RegionStar b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>The distance as the galaxy map shows it: whole light-years, rounded down.</summary>
    public static int LightYears(RegionStar a, RegionStar b) => (int)(Distance(a, b) * LightYearsPerUnit);

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
    /// Every ordinary star of a region in emission order (systems 1..N), or null for regions inside
    /// the goal gap (no stars) and for spiral regions when the settings predate catalog format 46.
    /// </summary>
    public static IReadOnlyList<RegionStar>? Ordinary(int galaxy, int x, int y, int z,
        GalaxyGenerationSettings settings, GalaxyPlacementSettings placement) =>
        Stars(galaxy, x, y, z, settings, placement)?.Where(s => s.System < 1000).ToList();

    /// <summary>
    /// Every star of a region in emission order: ordinary systems 1..N, then purple systems
    /// 1001..1065. Null where <see cref="Ordinary"/> is.
    /// </summary>
    public static IReadOnlyList<RegionStar>? Stars(int galaxy, int x, int y, int z,
        GalaxyGenerationSettings settings, GalaxyPlacementSettings placement)
    {
        int sx = SignExtend(x, 12), sy = SignExtend(y, 8), sz = SignExtend(z, 12);
        if (RegionStarGeneration.InsideGoalGap(sx, sy, sz, settings.GoalGapVoxelDistance)) return null;
        var (fixedStars, rng, segments) = Fixed(galaxy, x, y, z);
        var stars = new List<RegionStar>(fixedStars);

        // Grid setup (0x141368262..0x1413685D4): eleven draws.
        bool spiral = rng.Next(100) < (int)settings.SpiralFormChance;
        if (spiral && !placement.HasSpiral) return null;
        float armStep = (rng.NextWord() & 1) == 0 ? Pi : HalfPi;                           // [rbp+0x928]
        float inclusion = Lerp(rng, placement.SpiralInclusionLow, placement.SpiralInclusionHigh); // [rbp-0x5C]
        float tilt = RegionPathSegments.Unit(rng.NextWord()) * TwoPi - Pi;
        float turn = RegionPathSegments.Unit(rng.NextWord()) * TwoPi - Pi;
        float twist = RegionPathSegments.Unit(rng.NextWord()) * ThreeHalfPi + HalfPi;        // [rbp-0x54]
        float attractorMax = Lerp(rng, placement.AttractorMaxLow, placement.AttractorMaxHigh);
        float attractorMin = Lerp(rng, placement.AttractorMinLow, placement.AttractorMinHigh);
        rng.Step();                                                                          // base size
        float flex = Lerp(rng, placement.SpiralFlexLow, placement.SpiralFlexHigh);           // [rbp-0x58]
        float distortion = Lerp(rng, placement.DistortionLow, placement.DistortionHigh);
        var arms = spiral ? SpiralFrame(tilt, turn) : default;
        float starThreshold = spiral ? settings.StarGenerationThreshold * 1.2f : settings.StarGenerationThreshold; // 0x144B298A8

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
                    // Base gate 0x14136899E; the star threshold is only tested at 0x141369130, after the
                    // spiral step, so in spiral regions every cell over the base threshold can use arm draws.
                    if (!(density > settings.BaseGenerationThreshold)) continue;
                    bool emit = density > starThreshold;
                    if (!emit && !spiral) continue;

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
                    if (spiral)
                    {
                        // Spiral arms (0x141368BB5..0x14136908E), for stars outside the inclusion field.
                        V unit = new(Centre - a * InverseGrid, Centre - b * InverseGrid, Centre - c * InverseGrid);
                        float fromCentre = unit.X == 0f && unit.Y == 0f && unit.Z == 0f ? 0f : MathF.Sqrt(Dot(unit, unit));
                        float r = Clamp01((CoreRadius - fromCentre) * CoreFalloff);                // [rsp+0x54]
                        float s = placement.InnerFieldY;
                        float band = (GalaxySimplexNoise.BandedFbm(-s * pz, s * py, -s * px, 6, 2f, 0.5f) + 1f) * 0.5f;
                        if (inclusion > band)
                            moved = SpiralPull(rng, moved, r, armStep, twist, flex, arms, placement);
                    }
                    if (!emit) continue;
                    stars.Add(new RegionStar(stars.Count + 1, moved.X, moved.Y, moved.Z));
                    rng.Step(); // highlight roll (0x14136948F)
                    rng.Step(); // star size in the callback (0x141369E40)
                }

        // Map clouds (0x1413695AE..0x141369A24): one draw picks one or two groups; each group
        // takes three draws and Next(21)+4 items of ten draws each. Only the draw count matters here.
        int groups = (int)(rng.NextWord() >> 31) + 1;
        for (int group = 0; group < groups; group++)
        {
            rng.Step(); rng.Step();
            int items = (int)rng.Next(21) + 4;
            for (int i = 0; i < items * 10; i++) rng.Step();
        }

        // Purple systems 1001..1065 (0x141369A80..0x141369BE4): a size draw, three position
        // draws (z, y, x) spread over the whole region, and the callback's size draw.
        for (int k = 0; k < PurpleSystems; k++)
        {
            rng.Step();
            float pz = RegionPathSegments.Unit(rng.NextWord()) * CellSpacing * 19f + 0f + CellOffset;
            float py = RegionPathSegments.Unit(rng.NextWord()) * CellSpacing * 19f + 0f + CellOffset;
            float px = RegionPathSegments.Unit(rng.NextWord()) * CellSpacing * 19f + 0f + CellOffset;
            rng.Step();
            stars.Add(new RegionStar(1001 + k, px, py, pz));
        }
        return stars;
    }

    private static readonly float Pi = BitConverter.Int32BitsToSingle(0x40490FDB), HalfPi = BitConverter.Int32BitsToSingle(0x3FC90FDB),
        TwoPi = BitConverter.Int32BitsToSingle(0x40C90FDB), ThreeHalfPi = BitConverter.Int32BitsToSingle(0x4096CBE4);
    private const float Centre = 0.5f, ArmBound = 50f;                                       // 0x146ED3DB0, 0x144B29D50
    private static readonly float InverseGrid = BitConverter.Int32BitsToSingle(0x3D579436);  // 1/19 at 0x144B27BE8
    private static readonly float CoreRadius = BitConverter.Int32BitsToSingle(0x3F5DB3D0);   // sqrt(3)/2 at 0x144B29744
    private static readonly float CoreFalloff = BitConverter.Int32BitsToSingle(0x3FC51EB8);  // 1.54 at 0x144B29944

    /// <summary>
    /// The spiral's fixed frame (0x14136835F..0x141368D36): a rotation about x by <paramref name="tilt"/>
    /// (rows (1,0,0), (0,c,-s), (0,s,c)) times a rotation about z by <paramref name="turn"/>
    /// (rows (c,-s,0), (s,c,0), (0,0,1)), as row vectors; each lane sums (y + x) + z like the SSE code.
    /// </summary>
    private static (V R0, V R1, V R2) SpiralFrame(float tilt, float turn)
    {
        float c1 = GameTrig.Cos(tilt), s1 = GameTrig.Sin(tilt), c2 = GameTrig.Cos(turn), s2 = GameTrig.Sin(turn);
        V b0 = new(c2, -s2, 0f), b1 = new(s2, c2, 0f), b2 = new(0f, 0f, 1f);
        return (Times(new V(1f, 0f, 0f), b0, b1, b2), Times(new V(0f, c1, -s1), b0, b1, b2), Times(new V(0f, s1, c1), b0, b1, b2));
    }

    /// <summary>Row vector times a 3x3 matrix, lane order (y*row1 + x*row0) + z*row2.</summary>
    private static V Times(V v, V row0, V row1, V row2) => row1.Scale(v.Y) + row0.Scale(v.X) + row2.Scale(v.Z);

    /// <summary>
    /// Pull a star toward its spiral arm (0x141368BC3..0x14136908E). One draw picks the arm (of three);
    /// the arm point sits on a circle of radius (1 - r) * flex at angle arm * armStep + (1 - r) * pi * SpiralTwistMult,
    /// turned about y by (1 - r) * twist and then by the region's spiral frame. A point outside the
    /// region (|coordinate| >= 50) leaves the star where it is; otherwise a second draw lerps
    /// SpiralPull.x .. SpiralPull.y + SpiralPull.z * r for how far it moves toward the point.
    /// </summary>
    private static V SpiralPull(UniverseRandom rng, V star, float r, float armStep, float twist, float flex,
        (V R0, V R1, V R2) frame, GalaxyPlacementSettings placement)
    {
        int arm = (int)(RegionPathSegments.Unit(rng.NextWord()) * 3f);
        float outer = 1f - r;
        float angle = arm * armStep + outer * Pi * placement.SpiralTwistMult;
        V point = new(GameTrig.Sin(angle) * outer * flex, 0f, GameTrig.Cos(angle) * outer * flex);

        float turn = outer * twist;
        float c = GameTrig.Cos(turn), s = GameTrig.Sin(turn);
        V y0 = new(c, 0f, s), y1 = new(0f, 1f, 0f), y2 = new(-s, 0f, c);
        V d0 = Times(frame.R0, y0, y1, y2), d1 = Times(frame.R1, y0, y1, y2), d2 = Times(frame.R2, y0, y1, y2);
        point = d0.Scale(point.X) + new V(0f, 0f, 0f) + d1.Scale(point.Y) + d2.Scale(point.Z);
        if (!(point.X > -ArmBound && ArmBound > point.X && point.Y > -ArmBound && ArmBound > point.Y
            && point.Z > -ArmBound && ArmBound > point.Z)) return star;

        V toPoint = star - point;
        float squared = Dot(toPoint, toPoint), length = 0f;
        if (squared != 0f)
        {
            length = MathF.Sqrt(squared);
            toPoint = toPoint.Scale(1f / length);
        }
        float share = RegionPathSegments.Unit(rng.NextWord()) * (placement.SpiralPullZ * r + placement.SpiralPullY - placement.SpiralPullX)
            + placement.SpiralPullX;
        return star + toPoint.Scale(share * -length);
    }

    /// <summary>maxss with 0 then minss with 1, operand order as in the game.</summary>
    private static float Clamp01(float v)
    {
        float low = 0f > v ? 0f : v;
        return 1f < low ? 1f : low;
    }

    private const int PurpleSystems = 65;

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
