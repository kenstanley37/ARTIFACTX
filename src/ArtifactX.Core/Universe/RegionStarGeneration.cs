namespace ArtifactX.Core.Universe;

/// <summary>
/// Galaxy-generation settings the region generator reads, extracted from the player's
/// game files: <c>GcGalaxyGlobals.DefaultGeneration</c> (<c>GcGalaxyGenerationSetupData</c>)
/// and <c>GcGameplayGlobals.GoalGapVoxelDist</c>.
/// </summary>
public sealed record GalaxyGenerationSettings(float BaseGenerationThreshold, float StarGenerationThreshold,
    float BaseTurbulenceGain, float BaseTurbulenceLacunarity, float BaseTurbulenceScale, float SpiralFormChance,
    float GoalGapVoxelDistance);

/// <summary>Which systems a region holds: ordinary systems are 1..Ordinary, purple ones 1001..1000+Purple.</summary>
public sealed record RegionSystemCount(int Ordinary, int Purple);

/// <summary>
/// How many star systems the game generates in a region (NMS build 180836, galaxy-map
/// generator <c>0x141367810</c>). A region holds 122 fixed stars (120 guide stars, a black
/// hole and an Atlas station) plus one star for every cell of a 19x19x19 grid whose
/// derivative-damped fBm density exceeds both <c>BaseGenerationThreshold</c> and
/// <c>StarGenerationThreshold</c> (raised by a fixed 1.2 when the region's spiral roll is
/// below <c>SpiralFormChance</c>). Stars are numbered 1, 2, 3... in emission order; 65 purple
/// systems follow at 1001..1065. Regions inside the galactic centre's goal gap hold none.
/// Emission depends only on density, so cell positions, rotations and the spiral pull do not
/// change the count. The fixed-star loops are still simulated because their random draws
/// decide the spiral roll. Research and exact validation (175 regions read live from the
/// game, read-only): docs/research/REGION_STAR_COUNT.md.
/// </summary>
public static class RegionStarGeneration
{
    /// <summary>Executable constants of build 180836, not game-file data.</summary>
    private const int FixedStars = 122, PurpleSystems = 65, GridCells = 19;
    private const float RegionScale = 0.1f;               // 0x144B2812C
    private const float SpiralThresholdFactor = 1.2f;     // 0x144B298A8
    private const float NearStep = 5f, FarStep = 10f;     // TransitPopulationDistanceRange (0x14132AD38)
    private static readonly float CellOffset = BitConverter.Int32BitsToSingle(unchecked((int)0xC23D7943)); // startup 0x146ED3DC0
    private static readonly float CellSpacing = BitConverter.Int32BitsToSingle(0x40A86BCA);                // 100/19, 0x144B29C04

    /// <param name="x">Region X as stored in an address (12-bit two's complement).</param>
    /// <param name="y">Region Y (8-bit two's complement).</param>
    /// <param name="z">Region Z (12-bit two's complement).</param>
    public static RegionSystemCount Count(int galaxy, int x, int y, int z, GalaxyGenerationSettings settings)
    {
        int sx = SignExtend(x, 12), sy = SignExtend(y, 8), sz = SignExtend(z, 12);
        if (InsideGoalGap(sx, sy, sz, settings.GoalGapVoxelDistance)) return new RegionSystemCount(0, 0);

        uint lo = (uint)((y & 0xFF) << 24 | (z & 0xFFF) << 12 | (x & 0xFFF));
        uint hi = (uint)(galaxy & 0xFF);
        var rng = new UniverseRandom(((ulong)(RegionPathSegments.BitRotateRight16(lo) ^ hi ^ lo) << 32) | (lo == 0 ? 1u : lo));
        rng.Step(); // setup draw (0x141367C66)

        int placed = 0;
        foreach (var segment in RegionPathSegments.Build(galaxy, sx, sy, sz))
        {
            float dx = segment.EndX - segment.StartX, dy = segment.EndY - segment.StartY, dz = segment.EndZ - segment.StartZ;
            float length = MathF.Sqrt(dz * dz + (dy * dy + dx * dx));
            if (length == 0f) continue;
            float stepMin = 1f / (length / NearStep), stepMax = 1f / (length / FarStep);
            if (1f < stepMin) continue;
            float span = stepMax - stepMin, t = stepMin;
            do
            {
                for (int i = 0; i < 5; i++) rng.Step(); // star attributes 0x14136A020
                placed++;
                t += RegionPathSegments.Unit(rng.NextWord()) * span + stepMin;
            } while (1f >= t);
        }
        for (int i = Math.Max(0, FixedStars - placed) * 7; i > 0; i--) rng.Step();

        bool spiral = rng.Next(100) < (int)settings.SpiralFormChance;
        float starThreshold = spiral ? settings.StarGenerationThreshold * SpiralThresholdFactor : settings.StarGenerationThreshold;
        float rx = sx * RegionScale, ry = sy * RegionScale, rz = sz * RegionScale;
        float scale = settings.BaseTurbulenceScale;
        int stars = 0;
        for (int a = 0; a < GridCells; a++)
        {
            float px = a * CellSpacing + CellOffset + rx;
            for (int b = 0; b < GridCells; b++)
            {
                float py = ry + (b * CellSpacing + CellOffset);
                for (int c = 0; c < GridCells; c++)
                {
                    float pz = rz + (c * CellSpacing + CellOffset);
                    float density = GalaxySimplexNoise.Fbm(px * scale, py * scale, pz * scale, 4,
                        settings.BaseTurbulenceLacunarity, settings.BaseTurbulenceGain);
                    if (density > settings.BaseGenerationThreshold && density > starThreshold) stars++;
                }
            }
        }
        return new RegionSystemCount(FixedStars + stars, PurpleSystems);
    }

    /// <summary>Voxel attributes 0x14132ADAB: integer distance from the centre below GoalGapVoxelDist.</summary>
    public static bool InsideGoalGap(int x, int y, int z, float goalGapVoxelDistance)
    {
        double distance = Math.Sqrt(z * z + y * y + x * x);
        return goalGapVoxelDistance > (uint)(long)distance;
    }

    private static int SignExtend(int value, int bits) => (value << (32 - bits)) >> (32 - bits);
}
