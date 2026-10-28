using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Primary-body placement from NMS build 180836, 0x14164da40, including the moon
/// sphere distribution at 0x1405408b0. Prime-body placement is deliberately excluded.
/// The station's nearest-body search uses primary bodies whenever present.
/// Verified 2026-10-25: all 1,088 primary centers across 287 native emulator systems
/// matched exactly in Debug and Release. That set did not contain size-4 bodies.
/// </summary>
public static class SolarLayoutGeneration
{
    /// <summary>An ordinary system's primary bodies: packed address, native body sizes and exact centres (body 1 first).</summary>
    public sealed record SystemLayout(ulong Address, int[] Sizes, Vector3[] Centers);

    /// <summary>
    /// The primary-body layout of an ordinary system from its portal code and galaxy: native sizes and moon hosts
    /// from the system generator, then <see cref="PrimaryCenters"/>. Null for gas-giant systems (no size-4 bodies in
    /// the native regression set), forced layout modes and other unsupported inputs. Prime bodies are not included.
    /// The centres round down to the save's <c>6f=.aHM</c> in four save systems checked 2026-10-27.
    /// </summary>
    public static SystemLayout? ForSystem(ulong portalCode, int galaxy, SolarLayoutTables tables, bool placeMoons)
    {
        if (galaxy is < 0 or > 255 || ((portalCode >> 32) & 0xFFF) == 0
            || tables.GenerateForcedNumberPlanets != 0 || tables.GenerateMaximumSolarSystem)
            return null;
        var attributes = SystemGenerator.Attributes(portalCode, galaxy);
        if (attributes.GasGiant) return null;
        var bodies = SystemGenerator.Bodies(portalCode, galaxy);
        int count = attributes.PlanetCount;
        if (count is < 1 or > 8 || bodies.Hosts.Length < count) return null;
        var sizes = new int[count];
        var hosts = new int[count];
        int sizeIndex = 0;
        for (int i = 0; i < count; i++)
        {
            hosts[i] = bodies.Hosts[i] > 0 ? bodies.Hosts[i] - 1 : -1;
            if (hosts[i] >= 0) sizes[i] = 3;
            else
            {
                if (sizeIndex >= bodies.Sizes.Length) return null;
                sizes[i] = bodies.Sizes[sizeIndex++];
            }
        }
        ulong address = ((portalCode >> 32) & 0xFFF) << 40
            | (ulong)(uint)galaxy << 32 | (portalCode & 0xFFFFFFFF);
        var centers = PrimaryCenters(address, sizes, hosts, tables, placeMoons);
        return centers is null ? null : new SystemLayout(address, sizes, centers);
    }

    /// <summary>
    /// Exact primary centers from packed system address and native body sizes/zero-based
    /// moon hosts (-1 for planets). Moon placement must be supplied from the native gate
    /// at 0x14526da3c; its semantic field mapping remains unverified. Requires extracted
    /// layout globals; returns null for unsupported inputs.
    /// </summary>
    public static Vector3[]? PrimaryCenters(ulong address, IReadOnlyList<int> sizes,
        IReadOnlyList<int> hosts, SolarLayoutTables tables, bool placeMoons)
    {
        int count = sizes.Count;
        if (count is < 1 or > 8 || hosts.Count != count || sizes.Any(s => s is < 0 or > 4)
            || tables.SolarSystemSize.Length != 3 || tables.PerPlanetElevationChangeDegrees.Length != 2
            || tables.SolarSystemSize.Any(x => !float.IsFinite(x))
            || tables.PerPlanetElevationChangeDegrees.Any(x => !float.IsFinite(x))
            || !float.IsFinite(tables.SolarSystemMaximumRadius)
            || !float.IsFinite(tables.SolarSystemMaximumRadiusMassive)) return null;
        var rng = RandomFor(address);
        float angle = Fraction(rng.NextWord()) * 360f - 180f;
        float[] radii = sizes.Select(s => ShellRadius(s, tables.MassiveSolarSystems)).ToArray();
        var moons = new int[count];
        int planets = 0;
        for (int i = 0; i < count; i++)
        {
            if (hosts[i] == -1) planets++;
            if (sizes[i] != 3) continue;
            if (hosts[i] < 0 || hosts[i] >= i || sizes[hosts[i]] == 3) return null;
            moons[hosts[i]]++;
        }
        if (planets == 0 || sizes[0] == 3) return null;
        float distance = radii[0] * 2f;
        float step = 360f / Math.Max(planets - 1, 1);
        var centers = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            if (sizes[i] == 3) continue;
            float lowerStep = step * 0.5f;
            float increment = Fraction(rng.NextWord()) * (step * 1.5f - lowerStep) + lowerStep;
            float ratio = MathF.Min(1f, MathF.Max(0f, tables.SolarSystemSize[1] * 0.5f - radii[i] * 2f) / distance);
            float allowed = Asin(ratio) * 57.295780181884766f;
            angle += increment;
            float lower = MathF.Max(-allowed, tables.PerPlanetElevationChangeDegrees[0]);
            float upper = MathF.Min(allowed, tables.PerPlanetElevationChangeDegrees[1]);
            float elevation = -(Fraction(rng.NextWord()) * (upper - lower) + lower) * 0.01745329238474369f;
            float azimuth = angle * 0.01745329238474369f;
            float cosElevation = StationPoseGeneration.Cos(elevation);
            centers[i] = new Vector3(
                MathF.Floor((StationPoseGeneration.Sin(azimuth) * cosElevation) * distance),
                MathF.Floor(StationPoseGeneration.Sin(elevation) * distance),
                MathF.Floor((StationPoseGeneration.Cos(azimuth) * cosElevation) * distance));
            float maxRadius = tables.MassiveSolarSystems ? tables.SolarSystemMaximumRadiusMassive : tables.SolarSystemMaximumRadius;
            float minSpacing = radii[i] * 3f;
            float width = (maxRadius - distance) / planets - minSpacing;
            float spacing = width * Fraction(rng.NextWord()) + minSpacing;
            spacing += minSpacing;
            distance += spacing;
        }
        var offsets = new Vector3[count][];
        for (int i = 0; i < count; i++)
            if (moons[i] > 0) offsets[i] = MoonDirections(moons[i], rng.NextWord());
        if (placeMoons)
            for (int i = 0; i < count; i++)
            {
                if (sizes[i] != 3) continue;
                int host = hosts[i];
                Vector3 offset = offsets[host][--moons[host]];
                float separation = radii[host] * 1.5f + radii[i] * 2f;
                Vector3 center = offset * separation + centers[host];
                centers[i] = new Vector3(MathF.Floor(center.X), MathF.Floor(center.Y), MathF.Floor(center.Z));
            }
        return centers;
    }

    internal static UniverseRandom RandomFor(ulong seed)
    {
        uint low = (uint)seed;
        uint carry = ((low >> 16) | (low << 16)) ^ (uint)(seed >> 32) ^ low;
        return new UniverseRandom(((ulong)carry << 32) | (low == 0 ? 1u : low));
    }

    private static float Fraction(uint word) => (float)(word * 2.3283064370807974e-10);

    private static float ShellRadius(int size, bool massive)
    {
        int units = size switch { 0 => 32, 1 => 24, 2 => 16, 3 => 4, _ => 128 };
        if (massive) units *= 4;
        int bound = 1;
        while (bound < units) bound <<= 1;
        return (bound - Math.Max(1, bound - units)) * 32f * 32f;
    }

    private static Vector3[] MoonDirections(int count, uint rotation)
    {
        float step = 2f / count;
        float goldenAngle = (3f - MathF.Sqrt(5f)) * 3.1415927410125732f;
        var result = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float y = (i * step - 1f) + step * 0.5f;
            float radius = MathF.Sqrt(1f - y * y);
            float angle = (unchecked((uint)i + rotation) % (uint)count) * goldenAngle;
            result[i] = new Vector3(StationPoseGeneration.Cos(angle) * radius, y,
                StationPoseGeneration.Sin(angle) * radius);
        }
        return result;
    }

    private static float Asin(float value)
    {
        float x = MathF.Abs(value);
        float p = x * 0.0007179649546742439f - 0.004116097930818796f;
        p = p * x + 0.011272900737822056f;
        p = p * x - 0.02094927802681923f;
        p = p * x + 0.03268376365303993f;
        p = p * x - 0.050625279545784f;
        p = p * x + 0.08903469890356064f;
        p = p * x - 0.21460144221782684f;
        p = p * x + 1.5707963705062866f;
        float acos = MathF.Sqrt(1f - x) * p;
        if (value < 0f) acos = 3.1415927410125732f - acos;
        return 1.5707963705062866f - acos;
    }
}
