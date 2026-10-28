using ArtifactX.Core.Universe;

/// <summary>"spiralfind galaxy x y z": spiral regions next to the given one (x/z +-3, hex), found by leaving out the
/// spiral settings so the port returns null for them (the goal gap is far from these), as emulator targets.</summary>
static class SpiralFind
{
    public static void Show(int galaxy, int x, int y, int z, GalaxyGenerationSettings settings, GalaxyPlacementSettings placement)
    {
        for (int dx = -3; dx <= 3; dx++)
            for (int dz = -3; dz <= 3; dz++)
            {
                int rx = (x + dx) & 0xFFF, rz = (z + dz) & 0xFFF;
                if (RegionStarPositions.Stars(galaxy, rx, y, rz, settings, placement with { SpiralTwistMult = float.NaN }) is null)
                    Console.WriteLine($"{galaxy} {rx:X} {y:X} {rz:X}");
            }
    }
}
