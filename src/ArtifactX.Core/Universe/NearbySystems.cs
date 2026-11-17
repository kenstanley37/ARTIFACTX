namespace ArtifactX.Core.Universe;

/// <summary>One star system near a starting point: its packed address, how many regions away it is, and whether it is
/// a purple system (system index above 1000, reachable only with purple-star access).</summary>
public readonly record struct NearbySystem(ulong SystemAddress, int RegionsAway, bool Purple);

/// <summary>
/// Every star system in the regions around a starting system, from the game's own region generator
/// (<see cref="RegionStarGeneration.Count(int, int, int, int, GalaxyGenerationSettings)"/>: ordinary systems 1..N,
/// purple ones 1001..1000+P). A region is a 400 ly cube holding a few hundred systems, so a radius of 1 region (the
/// start's region and the 26 around it) is roughly 8,000 systems. The start can be any system: where the save is, a
/// typed portal address, or one read by the mobile app's glyph scanner.
/// </summary>
public static class NearbySystems
{
    /// <summary>The systems within <paramref name="regionRadius"/> regions of <paramref name="start"/> (a sphere of
    /// regions, nearest regions first), in the start's galaxy.</summary>
    public static IEnumerable<NearbySystem> Around(ulong start, int regionRadius, GalaxyGenerationSettings settings, bool includePurple = true)
    {
        int galaxy = (int)((start >> 32) & 0xFF);
        int x0 = Signed(start & 0xFFF, 12), z0 = Signed((start >> 12) & 0xFFF, 12), y0 = Signed((start >> 24) & 0xFF, 8);
        var offsets = new List<(int Dx, int Dy, int Dz, int Steps)>();
        for (int dx = -regionRadius; dx <= regionRadius; dx++)
            for (int dy = -regionRadius; dy <= regionRadius; dy++)
                for (int dz = -regionRadius; dz <= regionRadius; dz++)
                {
                    // A sphere of regions; every region touching the start's counts at radius 1.
                    int steps = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
                    if (dx * dx + dy * dy + dz * dz > regionRadius * regionRadius * 3) continue;
                    offsets.Add((dx, dy, dz, steps));
                }

        foreach (var (dx, dy, dz, steps) in offsets.OrderBy(o => o.Dx * o.Dx + o.Dy * o.Dy + o.Dz * o.Dz))
        {
            int x = x0 + dx, y = y0 + dy, z = z0 + dz;
            if (x is < -2048 or > 2047 || y is < -128 or > 127 || z is < -2048 or > 2047) continue;
            ulong region = ((ulong)galaxy << 32) | ((ulong)(uint)y & 0xFF) << 24 | ((ulong)(uint)z & 0xFFF) << 12 | ((ulong)(uint)x & 0xFFF);
            var count = RegionStarGeneration.Count(region, settings);
            for (int s = 1; s <= count.Ordinary; s++)
                yield return new NearbySystem(region | ((ulong)s << 40), steps, false);
            if (!includePurple) continue;
            for (int s = 1; s <= count.Purple; s++)
                yield return new NearbySystem(region | ((ulong)(1000 + s) << 40), steps, true);
        }
    }

    private static int Signed(ulong value, int bits) => (int)((long)(value << (64 - bits)) >> (64 - bits));
}
