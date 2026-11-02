using System.Numerics;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Recipes;

/// <summary>Four-pole neighborhood in native 0x1412F4AC0, inspected 2026-11-01.
/// The caller supplies the generated planet radius, not the point's height above
/// the sphere. The longitude seam deliberately wraps a non-positive lower index
/// to the final pole while retaining the upper index of one.</summary>
public static class HotspotNeighborhood
{
    /// <summary>Filtered nearest-hotspot selection at native 0x1412F4EB0.
    /// Distance removes the component along the hotspot's radial direction,
    /// so terrain height is excluded. Selection itself does not enforce range.</summary>
    public static (HotspotCandidate Candidate, float Distance)? Closest(ulong address, Vector3 point, float radius,
        int allowedFlags, HotspotPoleRules rules, IReadOnlyList<HotspotCategory> categories)
    {
        (HotspotCandidate Candidate, float Distance)? best = null;
        foreach (var candidate in Around(address, point, radius, rules, categories))
        {
            if ((candidate.Attributes.Flag & allowedFlags) == 0) continue;
            var delta = point - candidate.Direction * radius;
            float radial = delta.X * candidate.Direction.X + delta.Y * candidate.Direction.Y;
            radial += delta.Z * candidate.Direction.Z;
            float square = delta.X * delta.X + delta.Y * delta.Y;
            square += delta.Z * delta.Z;
            float distance = MathF.Sqrt(MathF.Max(0, square - radial * radial));
            if (best is null || distance < best.Value.Distance) best = (candidate, distance);
        }
        return best;
    }

    public static IReadOnlyList<HotspotCandidate> Around(ulong address, Vector3 point, float radius,
        HotspotPoleRules rules, IReadOnlyList<HotspotCategory> categories)
    {
        if (!float.IsFinite(radius) || radius <= 0 || !float.IsFinite(rules.Spacing) || rules.Spacing <= 0
            || radius * MathF.PI / rules.Spacing < 1)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var angles = HotspotOrientation.Inverse(HotspotOrientation.Direction(address), point);
        int divisions = (int)(radius * MathF.PI / rules.Spacing + 0.5f);
        int latitude = (int)(divisions * angles.Latitude / MathF.PI);
        int lower = latitude + 1 <= divisions ? latitude : divisions - 1;
        int upper = latitude + 1 <= divisions ? latitude + 1 : divisions;
        float circumference = radius * 6.2831855f;
        (int Lower, int Upper) Longitudes(int pole)
        {
            float ring = GameTrig.Sin(pole * MathF.PI / divisions) * circumference;
            float scaled = ring * angles.Longitude;
            scaled /= 6.2831855f;
            scaled /= rules.Spacing;
            int index = (int)scaled;
            int next = index + 1;
            if (index <= 0) index = (int)(ring / rules.Spacing);
            return (index, next);
        }
        var first = Longitudes(lower);
        var second = Longitudes(upper);
        var result = new List<HotspotCandidate>();
        result.AddRange(HotspotPole.Generate(address, radius, lower, first.Lower, rules, categories));
        result.AddRange(HotspotPole.Generate(address, radius, lower, first.Upper, rules, categories));
        result.AddRange(HotspotPole.Generate(address, radius, upper, second.Lower, rules, categories));
        result.AddRange(HotspotPole.Generate(address, radius, upper, second.Upper, rules, categories));
        return result;
    }
}
