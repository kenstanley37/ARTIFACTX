using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>A generated station transform, independent of its selected scene variant.</summary>
public sealed record StationPose(Vector3 Position, Vector3 Facing, ulong Seed);

/// <summary>
/// Ports station placement at NMS build 180836, 0x14164ff80. Inputs must include the
/// incoming layout MWC state and exact generated body centers, not rounded save centers.
/// GenerateForSystem also ports ordinary primary layout; station scene selection remains separate.
/// Verified 2026-10-25 against 100 native emulator poses: exact position, facing and seed
/// in Debug and Release, plus both reference cabinet position hashes.
/// See docs/research/STATION_EQUIPMENT_LOCATIONS.md for the executable trace.
/// </summary>
public static class StationPoseGeneration
{
    /// <summary>
    /// Uses catalog-confirmed ordinary SolarSystem boot behavior. Native 0x14164ead5
    /// tests BootMode against MinimalSolarSystem before placing moons. Other boot/debug
    /// modes and older catalogs remain unsupported by this automatic entry point.
    /// </summary>
    public static StationPose? GenerateForSystem(ulong portalCode, int galaxy, SolarLayoutTables tables)
        => tables.StandardMoonPlacementEnabled is true ? GenerateForSystem(portalCode, galaxy, tables, true) : null;

    /// <summary>
    /// Generates the ordinary system's pose from its portal code and galaxy. The main
    /// generator resets MWC from the packed address before station placement. Only primary
    /// centers are needed when present. This explicit overload remains available for native
    /// reference/debug inputs; the automatic overload uses catalog-confirmed ordinary boot
    /// behavior. Forced layout modes are unsupported.
    /// </summary>
    public static StationPose? GenerateForSystem(ulong portalCode, int galaxy, SolarLayoutTables tables, bool placeMoons)
    {
        // Prime-only systems stay unsupported (not in the native regression set); with primary bodies present the
        // station only searches those, so the prime centres don't change the pose.
        if (SolarLayoutGeneration.ForSystem(portalCode, galaxy, tables, placeMoons) is not { PrimaryCount: > 0 } layout) return null;
        return Generate(layout.Address, true, SolarLayoutGeneration.RandomFor(layout.Address).Seed,
            layout.Centers, layout.Sizes, layout.PrimaryCount, layout.Centers.Length - layout.PrimaryCount, tables);
    }

    /// <summary>
    /// Generates the stored station pose. Primary bodies precede prime bodies. Sizes use
    /// GcPlanetSize enum indices. Returns null for missing inputs or an unverified native
    /// degenerate-vector fallback; never substitutes an assumed orientation.
    /// </summary>
    public static StationPose? Generate(ulong systemSeed, bool seedEnabled, ulong incomingLayoutState,
        IReadOnlyList<Vector3> centers, IReadOnlyList<int> sizes, int primaryCount, int primeCount,
        SolarLayoutTables tables)
    {
        if (primaryCount < 0 || primeCount < 0 || primaryCount > 8 || primeCount > 8 - primaryCount
            || primaryCount + primeCount == 0 || centers.Count < primaryCount + primeCount
            || sizes.Count < primaryCount + primeCount || !float.IsFinite(tables.StationSpawnAvoidRadius)
            || tables.StationSpawnAvoidRadius < 0)
            return null;

        var layout = new UniverseRandom(incomingLayoutState);
        float distance = 5000f + Fraction(layout.NextWord()) * 5000f;
        if ((layout.NextWord() & 1) != 0) distance = -distance;
        var position = new Vector3(0f, distance, 0f);
        int nearest = -1;
        float best = float.MaxValue;
        // Native code searches prime bodies only when no primary was selected.
        int end = primaryCount > 0 ? primaryCount : primaryCount + primeCount;
        for (int i = 0; i < end; i++)
        {
            Vector3 delta = centers[i] - position;
            float squared = (delta.X * delta.X + delta.Y * delta.Y) + delta.Z * delta.Z;
            if (squared < best) { best = squared; nearest = i; }
        }
        if (nearest < 0 || sizes[nearest] is < 0 or > 4) return null;
        if (!Normalize(centers[nearest] - position, out var direction)
            || !Normalize(Vector3.Cross(Vector3.UnitY, direction), out var right)) return null;
        Vector3 up = Vector3.Cross(direction, right);

        float z = 1f - 2f * Fraction(layout.NextWord());
        float radius = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
        float theta = Fraction(layout.NextWord()) * 6.2831854820251465f;
        var sphere = new Vector3(Cos(theta) * radius, z, Sin(theta) * radius);
        int units = sizes[nearest] switch { 0 => 32, 1 => 24, 2 => 16, 3 => 4, _ => 128 };
        if (tables.MassiveSolarSystems) units *= 4;
        // All native radius-unit values are integers with exact power-of-two bounds.
        int bound = 1;
        while (bound < units) bound <<= 1;
        float shell = (bound - Math.Max(1, bound - units)) * 32f * 32f;
        Vector3 target = centers[nearest] + sphere * shell;
        float phi = Fraction(layout.NextWord()) * 6.2831854820251465f;
        Vector3 viewpoint = position + (right * Cos(phi)) * tables.StationSpawnAvoidRadius;
        viewpoint += (up * Sin(phi)) * tables.StationSpawnAvoidRadius;
        if (!Normalize(target - viewpoint, out var facing)) return null;

        uint low = (uint)systemSeed;
        uint carry = ((low >> 16) | (low << 16)) ^ (uint)(systemSeed >> 32) ^ low;
        var appearance = new UniverseRandom(seedEnabled ? ((ulong)carry << 32) | (low == 0 ? 1u : low) : 1UL);
        uint seedLow = appearance.NextWord(), seedHigh = appearance.NextWord();
        return new StationPose(position, facing,
            SystemMultiToolGenerator.Mix64(((ulong)seedHigh << 32) | seedLow));
    }

    private static float Fraction(uint word) => (float)(word * 2.3283064370807974e-10);

    private static bool Normalize(Vector3 vector, out Vector3 normalized)
    {
        normalized = default;
        const float epsilon = 1.52587890625e-05f;
        if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z)
            || (MathF.Abs(vector.X) < epsilon && MathF.Abs(vector.Y) < epsilon && MathF.Abs(vector.Z) < epsilon))
            return false;
        float length = MathF.Sqrt((vector.X * vector.X + vector.Y * vector.Y) + vector.Z * vector.Z);
        normalized = vector * (1f / length);
        return float.IsFinite(normalized.X) && float.IsFinite(normalized.Y) && float.IsFinite(normalized.Z);
    }

    private static float Reduce(float angle, out float sign)
    {
        int turns = (int)(angle * 0.15915493667125702f + (angle < 0f ? -0.5f : 0.5f));
        angle -= turns * 6.2831854820251465f;
        sign = 1f;
        if (angle > 1.5707963705062866f) { angle = 3.1415927410125732f - angle; sign = -1f; }
        else if (angle < -1.5707963705062866f) { angle = -3.1415927410125732f - angle; sign = -1f; }
        return angle;
    }

    internal static float Sin(float angle)
    {
        float x = Reduce(angle, out _), t = x * x;
        float p = 2.752155751295504e-6f - t * 2.3828544470916313e-8f;
        p = p * t - 0.00019840782624669373f;
        p = p * t + 0.008333330042660236f;
        p = p * t - 0.1666666716337204f;
        p = p * t + 1f;
        return p * x;
    }

    internal static float Cos(float angle)
    {
        float x = Reduce(angle, out float sign), t = x * x;
        float p = 2.47604948526714e-5f - t * 2.605161455448979e-7f;
        p = p * t - 0.0013888378161936998f;
        p = p * t + 0.04166663810610771f;
        p = p * t - 0.5f;
        p = p * t + 1f;
        return p * sign;
    }
}
