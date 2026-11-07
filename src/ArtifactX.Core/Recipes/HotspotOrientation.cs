using System.Numerics;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Recipes;

/// <summary>Address-derived spherical reference direction, native 0x1413524B0
/// inspected 2026-11-01. Only the low 52 address bits participate. This is an
/// orientation input, not a hotspot location. The separate rotation routine's
/// reference vector initializer at 0x140033520 sets (0,0,1,1).</summary>
public static class HotspotOrientation
{
    /// <summary>Inverse spherical angles from native 0x1412F55A0. Degenerate
    /// pole/tangent inputs are rejected rather than emitting non-finite locations.</summary>
    public static (float Radius, float Latitude, float Longitude) Inverse(Vector3 reference, Vector3 point)
    {
        static float Dot(Vector3 a, Vector3 b)
        {
            float xy = a.X * b.X + a.Y * b.Y;
            return xy + a.Z * b.Z;
        }
        static Vector3 Normal(Vector3 v) => v * (1f / MathF.Sqrt(Dot(v, v)));
        float radius = MathF.Sqrt(Dot(point, point));
        if (!float.IsFinite(radius) || radius <= 0) throw new ArgumentException("A finite, nonzero point is required.", nameof(point));
        float latitude = GameTrig.Acos(Dot(point * (1f / radius), reference));
        var baseline = Normal(Vector3.Cross(reference, Vector3.UnitZ));
        var tangent = Normal(Vector3.Cross(Vector3.Cross(reference, point), reference));
        float longitude = GameTrig.Acos(Dot(tangent, baseline));
        if (Dot(Vector3.Cross(baseline, tangent), reference) > 0) longitude = 6.2831855f - longitude;
        if (!float.IsFinite(latitude) || !float.IsFinite(longitude)) throw new ArgumentException("Spherical tangent is degenerate.", nameof(point));
        return (radius, latitude, longitude);
    }

    /// <summary>Spherical rotation equivalent to native 0x1412F59A0. Float
    /// operation ordering differs in Numerics quaternion operations; 256 native
    /// vectors on 2026-11-01 had a maximum direction error of 2.93E-7, not bit
    /// equivalence. Inputs are angular offsets in radians, not degrees.</summary>
    public static Vector3 Rotate(Vector3 reference, float latitudeRadians, float longitudeRadians, float scale = 1f)
    {
        var tangent = Vector3.Cross(Vector3.Cross(reference, Vector3.UnitZ), reference);
        if (!float.IsFinite(tangent.LengthSquared()) || tangent.LengthSquared() <= 0
            || !float.IsFinite(latitudeRadians) || !float.IsFinite(longitudeRadians) || !float.IsFinite(scale))
            throw new ArgumentException("A finite reference with a nonzero spherical tangent and finite offsets is required.");
        var axis = Vector3.Normalize(tangent);
        Vector3 Apply(Vector3 value, Vector3 around, float radians)
        {
            float angle = -radians * 0.5f;
            var q = new Quaternion(around * GameTrig.Sin(angle), GameTrig.Cos(angle));
            var transformed = Quaternion.Multiply(Quaternion.Multiply(q, new Quaternion(value, 0f)), Quaternion.Inverse(q));
            return new(transformed.X, transformed.Y, transformed.Z);
        }
        return Apply(Apply(reference, axis, latitudeRadians), reference, longitudeRadians) * scale;
    }

    public static Vector3 Direction(ulong address)
    {
        address &= 0xFFFFFFFFFFFFFUL;
        uint low = (uint)address;
        uint high = (uint)(address >> 32);
        uint carry = BitOperations.RotateRight(low, 16) ^ high ^ low;
        var random = new UniverseRandom(((ulong)carry << 32) | (low == 0 ? 1U : low));
        float fraction = (float)(random.NextWord() * (1.0 / 4294967295.0));
        float y = 1f - (fraction + fraction);
        float square = y * y;
        float radial = MathF.Sqrt(MathF.Max(0f, 1f - square));
        float angle = (float)(random.NextWord() * (1.0 / 4294967295.0));
        angle *= 6.2831855f;
        return new(GameTrig.Cos(angle) * radial, y, GameTrig.Sin(angle) * radial);
    }
}
