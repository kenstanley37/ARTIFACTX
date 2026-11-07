namespace ArtifactX.Core.Universe;

/// <summary>A galactic path segment inside a region, in region units (the region spans ±50).</summary>
public readonly record struct RegionPathSegment(float StartX, float StartY, float StartZ, float EndX, float EndY, float EndZ);

/// <summary>
/// The galaxy map's path segments through one region (NMS build 180836 <c>0x14136A930</c>).
/// Each of the six faces gets a point from a hash of the face's shared coordinates, so a
/// neighbouring region computes the same point; face points closer than 1.0 are linked, a
/// centre point (from the region's own hash) takes every other link, and each edge is
/// emitted once. Guide stars are laid along these segments. Verified bit-exact against
/// the emulated routine for 60 regions (scripts/research/region-generator/segport.py).
/// </summary>
public static class RegionPathSegments
{
    /// <param name="x">Region X (12-bit, signed).</param>
    /// <param name="y">Region Y (8-bit, signed).</param>
    /// <param name="z">Region Z (12-bit, signed).</param>
    public static IReadOnlyList<RegionPathSegment> Build(int galaxy, int x, int y, int z)
    {
        var points = new (float X, float Y, float Z)[7];
        var links = new int[7, 6];
        for (int p = 0; p < 7; p++)
            for (int f = 0; f < 6; f++) links[p, f] = -2;

        for (int face = 0; face < 6; face++)
        {
            uint h = FaceHash(face, x, y, z);
            var stream = new UniverseRandom(((ulong)(BitRotateRight16(h) ^ h) << 32) | (h == 0 ? 1u : h));
            float u = Unit(stream.NextWord()), v = Unit(stream.NextWord());
            links[face, face] = -1;
            points[face] = face switch
            {
                0 => (u, v, -1f),
                1 => (u, v, 1f),
                2 => (u, -1f, v),
                3 => (u, 1f, v),
                4 => (-1f, u, v),
                _ => (1f, u, v),
            };
        }
        for (int i = 0; i < 6; i++)
            for (int j = i + 1; j < 6; j++)
            {
                float dx = points[i].X - points[j].X, dy = points[i].Y - points[j].Y, dz = points[i].Z - points[j].Z;
                if (MathF.Sqrt(dz * dz + (dy * dy + dx * dx)) <= 1f)
                {
                    links[i, j] = j;
                    links[j, i] = i;
                }
            }

        ulong packed = (ulong)(uint)(x & 0xFFF) | (ulong)(uint)(z & 0xFFF) << 12 | (ulong)(uint)(y & 0xFF) << 24 | (ulong)(uint)(galaxy & 0xFF) << 32;
        ulong hash = Mix64(packed);
        uint lo = (uint)hash, hi = (uint)(hash >> 32);
        var centre = new UniverseRandom(((ulong)(BitRotateRight16(lo) ^ hi ^ lo) << 32) | (lo == 0 ? 1u : lo));
        uint w1 = centre.NextWord(), w2 = centre.NextWord(), w3 = centre.NextWord();
        const int Centre = 6;
        points[Centre] = (Unit(w1) - 0.5f, Unit(w2) - 0.5f, Unit(w3) - 0.5f);

        var segments = new List<RegionPathSegment>();
        var emitted = new HashSet<(int, int)>();
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
            {
                int m = links[i, j];
                if (m == -2) m = Centre;
                if (m == -1 || emitted.Contains((m, i)) || emitted.Contains((i, m))) continue;
                emitted.Add((m, i));
                emitted.Add((i, m));
                segments.Add(new RegionPathSegment(points[i].X * 50f, points[i].Y * 50f, points[i].Z * 50f,
                    points[m].X * 50f, points[m].Y * 50f, points[m].Z * 50f));
            }
        return segments;
    }

    /// <summary>A region-stream draw as the game reads it: (double)word * (1 / (2^32 - 1)), narrowed.</summary>
    internal static float Unit(uint word) => (float)(word * 2.3283064370807974e-10);

    internal static uint BitRotateRight16(uint v) => (v >> 16) | (v << 16);

    private static ulong Mix64(ulong v)
    {
        v = ((v >> 33) ^ v) * 0x64DD81482CBD31D7;
        v = ((v >> 33) ^ v) * 0xE36AA5C613612997;
        return (v >> 33) ^ v;
    }

    /// <summary>The face's shared coordinates hashed with the routine at 0x14136AACF..0x14136AB62.</summary>
    private static uint FaceHash(int face, int x, int y, int z)
    {
        (int r9, int a, int d) = face switch
        {
            0 => (z - 1, x, y),
            1 => (z, x, y),
            2 => (y - 1, z, x),
            3 => (y, z, x),
            4 => (x - 1, z, y),
            _ => (x, z, y),
        };
        uint seed = (uint)(d ^ a);
        int shifted = unchecked(r9 - int.MinValue);
        ulong r8 = r9 >= 0 ? (ulong)(long)shifted : (ulong)(uint)(shifted & 0x7FFFFFFF);
        uint c = (uint)(r8 & 0xFFFF) + seed;
        uint h = ((uint)r8 >> 5 & 0x7FFF800) ^ (c << 16) ^ c;
        uint high = (uint)(r8 >> 32);
        h = h + (h >> 11) + (high & 0xFFFF);
        uint h2 = (high >> 5 & 0x7FFF800) ^ (h << 16) ^ h;
        c = h2 + (h2 >> 11);
        c ^= c << 3; c += c >> 5;
        c ^= c << 4; c += c >> 17;
        c ^= c << 25; c += c >> 6;
        return c;
    }
}
