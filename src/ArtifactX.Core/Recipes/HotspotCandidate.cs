using System.Numerics;
using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Recipes;

/// <summary>One generated proposal before spacing rejection; direction is planet-relative.</summary>
public sealed record HotspotCandidate(Vector3 Direction, float Latitude, float Longitude, HotspotAttributeRoll Attributes)
{
    /// <summary>Candidate construction in native 0x1412F6580, inspected 2026-11-01.
    /// Spacing comes from the hotspot table; range includes its native ten-unit margin.
    /// This does not establish whether a proposal survives the pole's spacing pass.</summary>
    public static HotspotCandidate Generate(ulong address, float radius, int poleA, int poleB, float spacing,
        ulong state, IReadOnlyList<HotspotCategory> categories)
    {
        if (!float.IsFinite(radius) || radius <= 0 || !float.IsFinite(spacing) || spacing <= 0
            || radius * MathF.PI / spacing < 0.5f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var attributes = HotspotAttributes.Roll(state, categories);
        float divisions = (int)(radius * MathF.PI / spacing + 0.5f);
        float latitude = poleA * MathF.PI / divisions;
        float circumference = radius * 6.2831855f;
        float ring = GameTrig.Sin(latitude) * circumference;
        float longitude = ring > 0 ? poleB * spacing * 6.2831855f / ring : 0;
        float halfWidth = spacing * 0.5f - (attributes.Range + 10f);
        float latitudeLimit = halfWidth / radius;
        float longitudeLimit = ring > 0 ? halfWidth * 6.2831855f / ring : 6.2831855f;
        var random = new UniverseRandom(state);
        random.Step(); // Type.
        random.Step(); // Range.
        float latitudeDraw = (float)(random.NextWord() * (1.0 / 4294967295.0));
        float longitudeDraw = (float)(random.NextWord() * (1.0 / 4294967295.0));
        float latitudeJitter = latitudeDraw * (latitudeLimit - -latitudeLimit);
        latitudeJitter += -latitudeLimit;
        latitude += latitudeJitter;
        float longitudeJitter = longitudeDraw * (longitudeLimit - -longitudeLimit);
        longitudeJitter += -longitudeLimit;
        longitude = longitudeJitter + longitude;
        return new(HotspotOrientation.Rotate(HotspotOrientation.Direction(address), latitude, longitude),
            latitude, longitude, attributes);
    }
}
